using Ariel.Content;
using Ariel.Content.Schema;
using Ariel.Game;
using Ariel.Mods.Religion;

namespace Ariel.Mods.Votes;

/// <summary>Who sits on a council and how many votes each holds (CvPlayer::getVotes, isVotingMember, isFullMember),
/// who may stand in its elections (CvGame::isTeamVoteEligible) and which resolutions it may hold
/// (CvGame::isValidVoteSelection, canDoResolution). Every answer is read from the game; nothing here changes state.</summary>
internal static class CouncilRules
{
    /// <summary>Candidates an election offers beyond the teams a building makes eligible (TEAM_VOTE_MIN_CANDIDATES).</summary>
    private const int DefaultMinCandidates = 4;
    /// <summary>Sessions a secretary serves before standing again (DIPLO_VOTE_SECRETARY_GENERAL_INTERVAL).</summary>
    private const int DefaultSecretaryTerm = 4;

    internal static VotesGameState Read(Simulation sim) => sim.State.Extensions.Get<VotesGameState>(sim.Content) ?? new();
    internal static VotesGameState Edit(Simulation sim)
    {
        var state = sim.State.Extensions.Get<VotesGameState>(sim.Content);
        if (state is null) sim.State.Extensions.Set(sim.Content, state = new VotesGameState());
        return state;
    }
    internal static CouncilState? Council(Simulation sim, string source) => Read(sim).Councils.GetValueOrDefault(source);

    internal static int MinCandidates(Simulation sim) => Constant(sim, "TEAM_VOTE_MIN_CANDIDATES", DefaultMinCandidates);
    internal static int SecretaryTerm(Simulation sim) => Constant(sim, "DIPLO_VOTE_SECRETARY_GENERAL_INTERVAL", DefaultSecretaryTerm);
    private static int Constant(Simulation sim, string key, int fallback) =>
        sim.Content.Get<RuleConstantsDef>().All.Single().Raw.TryGetValue(key, out var text) && int.TryParse(text, out int value)
            ? value : fallback;

    /// <summary>Turns between sessions: the council's interval scaled like training times.</summary>
    internal static int Interval(Simulation sim, string source) => Math.Max(1,
        sim.Content.Get<VoteSourceDef>(source).Interval * sim.Content.Get<GameSpeedDef>(sim.Settings.GameSpeed).TrainPercent / 100);

    /// <summary>The buildings that seat the council, by building id.</summary>
    private static IEnumerable<string> SeatBuildings(Simulation sim, string source) => sim.Content.Get<BuildingVotesDef>().All
        .Where(b => b.VoteSource == source).Select(b => b.Target);
    /// <summary>The cities holding a building that seats the council, in id order.</summary>
    internal static IEnumerable<City> Seats(Simulation sim, string source)
    {
        var buildings = SeatBuildings(sim, source).ToHashSet(StringComparer.Ordinal);
        return sim.State.Cities.Where(c => c.Buildings.Any(buildings.Contains)).OrderBy(c => c.Id);
    }
    /// <summary>A council sits while a building seats it and, for a religious one, while its religion can be counted.</summary>
    internal static bool Sits(Simulation sim, string source) =>
        Council(sim, source) is { } council && (council.Religion is null || Religions(sim) is not null) && Seats(sim, source).Any();

    /// <summary>The councils that sit, in id order.</summary>
    internal static IEnumerable<(string Source, CouncilState State)> Sitting(Simulation sim) => Read(sim).Councils
        .Where(c => Sits(sim, c.Key)).Select(c => (c.Key, c.Value));

    internal static IReligionService? Religions(Simulation sim) => sim.Service<IReligionService>();
    /// <summary>Whether a council may still decide the game: the diplomatic victory is on and no one has won yet.</summary>
    internal static bool VictoryOpen(Simulation sim) => sim.Rules().Victory is var victory
        && !victory.Status(sim).IsGameOver && victory.IsDecidedBy(sim, CouncilVictory.ConditionId);

    /// <summary>A civilization still in the game: not the barbarians, and holding a city or a unit.</summary>
    internal static bool Alive(Simulation sim, int player) => PlayerRules.IsNation(sim, player) && PlayerRules.IsAlive(sim, player);
    internal static IEnumerable<int> Civilizations(Simulation sim) =>
        sim.State.Players.Select(p => p.Id).Where(p => Alive(sim, p)).Order();

    private static bool HasReligion(Simulation sim, City city, string religion) =>
        Religions(sim)?.Present(sim, city.Id).Contains(religion) == true;
    private static string? StateReligion(Simulation sim, int player) => Religions(sim)?.StateReligion(sim, player);

    /// <summary>CvPlayer::getVotes. A member's weight is its citizens, or for a religious council its citizens in cities
    /// of the religion. A vote may count cities or civilizations instead; a member whose state religion is the council's
    /// gets the vote's bonus.</summary>
    internal static int Votes(Simulation sim, int player, string source, string? vote = null)
    {
        if (Council(sim, source) is not { } council || !Alive(sim, player)) return 0;
        var religion = council.Religion;
        var cities = sim.State.Cities.Where(c => c.Owner == player && (religion is null || HasReligion(sim, c, religion)));
        if (vote is null) return cities.Sum(c => c.Population);
        var def = sim.Content.Get<VoteDef>(vote);
        if (!def.HeldBy(source)) return 0;
        int votes = def.CivVoting ? (cities.Any() || religion is null ? 1 : 0)
            : def.CityVoting ? cities.Count() : cities.Sum(c => c.Population);
        if (religion is not null && StateReligion(sim, player) == religion) votes = votes * (100 + def.StateReligionVotePercent) / 100;
        return votes;
    }

    internal static bool IsVotingMember(Simulation sim, int player, string source) => Votes(sim, player, source) > 0;

    /// <summary>A voting member that keeps the council's religion as its state religion and runs its civic, and has not
    /// defied it since it last obeyed.</summary>
    internal static bool IsFullMember(Simulation sim, int player, string source)
    {
        if (Council(sim, source) is not { } council || !IsVotingMember(sim, player, source)) return false;
        if (council.Religion is { } religion && StateReligion(sim, player) != religion) return false;
        if (sim.Content.Get<VoteSourceDef>(source).Civic is { } civic && !sim.State.PlayerById(player).Civics.ContainsValue(civic))
            return false;
        return !council.Defiant.Contains(player);
    }

    /// <summary>The team's living civilizations.</summary>
    internal static IEnumerable<int> Members(Simulation sim, int team) =>
        sim.State.TeamById(team).Members.Where(p => Alive(sim, p)).Order();
    internal static bool TeamVotes(Simulation sim, int team, string source) => Members(sim, team).Any(p => IsVotingMember(sim, p, source));
    /// <summary>CvTeam::isFullMember: every living member is one.</summary>
    internal static bool TeamIsFull(Simulation sim, int team, string source) =>
        Members(sim, team).Any() && Members(sim, team).All(p => IsFullMember(sim, p, source));
    internal static int TeamVoteCount(Simulation sim, int team, string source, string? vote = null) =>
        Members(sim, team).Sum(p => Votes(sim, p, source, vote));
    /// <summary>Teams with a living civilization, in id order.</summary>
    internal static IEnumerable<int> Teams(Simulation sim) => Civilizations(sim).Select(p => sim.State.PlayerById(p).TeamId).Distinct().Order();
    internal static IEnumerable<int> VotingTeams(Simulation sim, string source) => Teams(sim).Where(t => TeamVotes(sim, t, source));
    /// <summary>The team's lowest-id living member: whom the team's attitude and agreements are asked through.</summary>
    internal static int Lead(Simulation sim, int team) => Members(sim, team).First();

    internal static int Possible(Simulation sim, string source, string vote) => Civilizations(sim).Sum(p => Votes(sim, p, source, vote));
    internal static int Required(Simulation sim, string source, string vote) =>
        Possible(sim, source, vote) * sim.Content.Get<VoteDef>(vote).PopulationThreshold / 100;

    /// <summary>CvGame::isTeamVoteEligible: every team owning a seat stands; the strongest full-member teams fill the
    /// rest of the ballot up to the minimum number of candidates, ties going to the lower team id.</summary>
    internal static IReadOnlyList<int> Candidates(Simulation sim, string source)
    {
        var eligible = SeatBuildings(sim, source).Where(b => sim.Content.Get<BuildingDef>(b).ForceTeamVoteEligible).ToHashSet(StringComparer.Ordinal);
        var forced = Seats(sim, source).Where(c => c.Buildings.Any(eligible.Contains) && Alive(sim, c.Owner))
            .Select(c => sim.State.PlayerById(c.Owner).TeamId).ToHashSet();
        int extra = MinCandidates(sim) - forced.Count;
        var others = Teams(sim).Where(t => !forced.Contains(t) && TeamIsFull(sim, t, source))
            .OrderByDescending(t => TeamVoteCount(sim, t, source)).ThenBy(t => t).Take(Math.Max(0, extra));
        return forced.Concat(others).Order().ToArray();
    }

    /// <summary>Whether the council may hold this vote now. An election stands when it has candidates; a victory vote
    /// also needs every civilization to vote and no team to hold the needed votes alone. A resolution must change
    /// something and name a player it can bind.</summary>
    internal static string? Invalid(Simulation sim, string source, Resolution resolution)
    {
        if (Council(sim, source) is not { } council || !Sits(sim, source)) return "the council does not sit";
        if (!sim.Content.TryGet<VoteDef>(resolution.Vote, out var def) || !def.HeldBy(source)) return "the council holds no such vote";
        if (!def.IsSupported) return "this vote is not held in this game";
        if (VotingTeams(sim, source).Count() < def.MinVoters) return "too few members to vote";
        if (def.IsTargeted != (resolution.Target >= 0)) return def.IsTargeted ? "name a player" : "this vote names no player";
        if (def.IsElection) return InvalidElection(sim, source, def);
        if (def.IsStanding && council.Standing.Contains(def.Id)) return "already in force";
        if (def.OpenBorders) return InvalidAgreement(sim, source, DiplomaticAgreement.OpenBorders);
        if (def.DefensivePact) return InvalidAgreement(sim, source, DiplomaticAgreement.DefensivePact);
        if (def.IsTargeted) return InvalidTarget(sim, source, council, def, resolution.Target);
        return null;
    }

    private static string? InvalidElection(Simulation sim, string source, VoteDef def)
    {
        if (Candidates(sim, source).Count == 0) return "no one may stand";
        if (!def.Victory) return null;
        if (!VictoryOpen(sim)) return "the game cannot be won by vote";
        if (Civilizations(sim).Any(p => !IsVotingMember(sim, p, source))) return "every civilization must vote";
        int required = Required(sim, source, def.Id);
        if (Teams(sim).Any(t => TeamVoteCount(sim, t, source, def.Id) >= required)) return "one team already holds the votes";
        return null;
    }

    /// <summary>Every pair of voting teams without the agreement must be able to sign it, and one pair must lack it.</summary>
    private static string? InvalidAgreement(Simulation sim, string source, DiplomaticAgreement agreement)
    {
        var diplomacy = sim.Rules().Diplomacy;
        var missing = MissingAgreements(sim, source, agreement).ToList();
        if (missing.Count == 0) return "every member already has it";
        return missing.Select(p => diplomacy.ValidateAgreement(sim, Lead(sim, p.First), Lead(sim, p.Second), agreement))
            .FirstOrDefault(e => e is not null);
    }
    internal static IEnumerable<TeamPair> MissingAgreements(Simulation sim, string source, DiplomaticAgreement agreement)
    {
        var diplomacy = sim.Rules().Diplomacy;
        var teams = VotingTeams(sim, source).ToArray();
        for (int i = 0; i < teams.Length; i++)
            for (int j = i + 1; j < teams.Length; j++)
            {
                bool has = agreement == DiplomaticAgreement.OpenBorders
                    ? diplomacy.HasOpenBorders(sim, teams[i], teams[j]) : diplomacy.HasDefensivePact(sim, teams[i], teams[j]);
                if (!has) yield return TeamPair.Of(teams[i], teams[j]);
            }
    }

    private static string? InvalidTarget(Simulation sim, string source, CouncilState council, VoteDef def, int target)
    {
        if (!sim.State.Players.Any(p => p.Id == target) || !Alive(sim, target)) return "no such player";
        int targetTeam = sim.State.PlayerById(target).TeamId;
        var relations = sim.Rules().Relations;
        if (VotingTeams(sim, source).Any(t => !relations.HasMet(sim, t, targetTeam))) return "not every member knows that player";
        bool full = IsFullMember(sim, target, source);
        var fullTeams = Teams(sim).Where(t => t != targetTeam && TeamIsFull(sim, t, source)).ToArray();
        if (def.ForcePeace)
        {
            if (!full) return "only a full member can be made to make peace";
            return VotingTeams(sim, source).Any(t => sim.State.TeamById(t).Wars.Contains(targetTeam)) ? null : "no member is at war with that player";
        }
        if (full) return "a full member cannot be named";
        if (def.ForceNoTrade)
        {
            if (council.Sanctioned.Contains(targetTeam)) return "already under sanctions";
            return fullTeams.Any(t => !sim.State.TeamById(t).Wars.Contains(targetTeam)) ? null : "every member is already at war with that player";
        }
        // ForceWar
        if (targetTeam == council.Secretary) return "the secretary cannot be named";
        if (!fullTeams.Any(t => !sim.State.TeamById(t).Wars.Contains(targetTeam) && relations.ValidateWar(sim, Lead(sim, t), target) is null))
            return "no member can go to war with that player";
        if (!IsVotingMember(sim, target, source) && !fullTeams.Any(t => sim.State.TeamById(t).Wars.Contains(targetTeam)))
            return "a non-member can only be named while it fights a member";
        return null;
    }

    /// <summary>CvPlayer::canDefyResolution: a member other than the secretary may defy a resolution that would bind it.</summary>
    internal static bool MayDefy(Simulation sim, int player, string source, Resolution resolution)
    {
        if (Council(sim, source) is not { } council || !sim.Content.TryGet<VoteDef>(resolution.Vote, out var def)) return false;
        int team = sim.State.PlayerById(player).TeamId;
        if (council.Secretary == team || def.IsElection) return false;
        if (def.OpenBorders || def.DefensivePact)
        {
            var agreement = def.OpenBorders ? DiplomaticAgreement.OpenBorders : DiplomaticAgreement.DefensivePact;
            return MissingAgreements(sim, source, agreement).Any(p => p.First == team || p.Second == team);
        }
        if (def.IsTargeted && resolution.Target >= 0)
        {
            int targetTeam = sim.State.PlayerById(resolution.Target).TeamId;
            bool atWar = sim.State.TeamById(team).Wars.Contains(targetTeam);
            if (def.ForcePeace) return targetTeam == team || atWar;
            if (def.ForceWar) return !atWar;
            return true;
        }
        return true;
    }
}
