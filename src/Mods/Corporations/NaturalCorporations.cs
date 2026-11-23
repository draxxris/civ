using Ariel.Content;
using Ariel.Content.Schema;
using Ariel.Game;

namespace Ariel.Mods.Corporations;

/// <summary>Guilds and corporations that found, spread and close by themselves: the Realistic Guilds/Corporations mode
/// (<see cref="CorporationMode"/>). Every query here is read-only and draws nothing; only <see cref="Run"/>, the
/// <c>corporations:natural</c> turn system, draws, in a stable order.
/// <list type="bullet">
/// <item>Founding. Each unfounded corporation, in id order, is tried for each nation, in player id order, that can
/// found it (<see cref="FoundingSite"/>): it knows the founding building's technologies, the corporation is not obsolete
/// for it, its civics allow corporations, it is not waiting out a cooldown, and one of its cities can use a copy of a
/// resource the corporation consumes (a guild that consumes none needs its <see cref="CorporationDef.FoundingBuildingClasses"/>
/// instead). Such a nation founds it with probability 1 / M a turn, M being <see cref="CorporationRulesDef.FoundingMeanTurns"/>
/// scaled to the pace: a geometric wait with mean M. The first success founds it, so it has one founder.</item>
/// <item>Spread, the source's <c>CvCity::doCorporation</c> with the nation's supply in place of the player's
/// influence. See <see cref="SpreadOdds"/>.</item>
/// <item>Decay. A branch outside the headquarters whose city can use no copy of its resources closes after
/// <see cref="CorporationRulesDef.BranchDecayTurns"/>; a headquarters whose nation has no access at all for
/// <see cref="CorporationRulesDef.HeadquartersDecayTurns"/> dissolves the corporation, which that nation may found again
/// only after <see cref="CorporationRulesDef.FoundingCooldownTurns"/>. Each wait is a timer (Timers); the condition is
/// checked again when it runs out.</item>
/// </list></summary>
internal sealed class NaturalCorporations(CorporationService service)
{
    internal const string BranchDecayKind = "corporations:branch_decay";
    internal const string HeadquartersDecayKind = "corporations:headquarters_decay";
    internal const string CooldownKind = "corporations:founding_cooldown";

    internal static CorporationRulesDef Rules(ContentDb content) => content.Get<CorporationRulesDef>().All.Single();

    /// <summary>The source's rule constants the spread reads. A content check refuses a load without them.</summary>
    internal static readonly string[] SourceConstants =
        ["CORPORATION_SPREAD_RAND", "CORPORATION_SPREAD_DISTANCE_DIVISOR", "CORPORATION_RESOURCE_BASE_INFLUENCE", "FREE_TRADE_CORPORATION_SPREAD_MOD"];

    internal static int SourceConstant(ContentDb content, string key) =>
        int.Parse(content.Get<RuleConstantsDef>().All.Single().Raw[key], System.Globalization.CultureInfo.InvariantCulture);

    // ---------------------------------------------------------------- founding

    /// <summary>The city the player's nation would found the corporation in now, or null when it cannot found it.
    /// The city is the one that can use the most copies of the corporation's resources, then the most populous, then
    /// the lowest id: the headquarters sits where the trade is richest, and the order never depends on the RNG.
    /// <paramref name="ignoreCivics"/> leaves the player's civics out, for the computer player weighing a ban.</summary>
    internal City? FoundingSite(Simulation sim, int player, CorporationDef def, bool ignoreCivics = false, Pass? pass = null)
    {
        if (service.IsFounded(sim, def.Id) || !PlayerRules.IsNation(sim, player)) return null;
        if (!KnowsFoundingTechs(sim, player, def) || CorporationService.Obsolete(sim, player, def)) return null;
        if (!ignoreCivics && CorporationService.BansCorporations(sim, player)) return null;
        if (sim.Rules().Timers.IsActive(sim, CooldownKind, TimerHolder.OfPlayer(player), def.Id)) return null;
        var cities = sim.State.Cities.Where(c => c.Owner == player).OrderBy(c => c.Id).ToArray();
        if (cities.Length == 0) return null;
        if (def.PrereqBonuses.Length == 0 && !HoldsFoundingBuildings(sim, cities[0], def)) return null;
        City? best = null;
        long bestCopies = -1;
        foreach (var city in cities)
        {
            long copies = def.PrereqBonuses.Length == 0 ? 0 : CorporationService.Consumed(def, Counts(sim, city, pass));
            if (def.PrereqBonuses.Length > 0 && copies <= 0) continue;
            if (service.HeadquartersConflict(sim, city.Id, def) is not null) continue;
            if (best is null || copies > bestCopies || (copies == bestCopies && city.Population > best.Population))
            {
                best = city; bestCopies = copies;
            }
        }
        return best;
    }

    internal static bool KnowsFoundingTechs(Simulation sim, int player, CorporationDef def) =>
        CorporationIndex.Of(sim.Content).FoundingTechs.GetValueOrDefault(def.Id, []).All(t => sim.KnowsTech(player, t));

    /// <summary>Whether the city's owner holds the buildings a guild that consumes no resource needs to be founded and
    /// to keep its Master Guild, counted across the empire by the requirement evaluator, as the Master Guild's own
    /// requirement counts them.</summary>
    internal static bool HoldsFoundingBuildings(Simulation sim, City city, CorporationDef def) =>
        def.FoundingBuildingClasses.OrderBy(p => p.Key, StringComparer.Ordinal).All(p => sim.Content.Requirements
            .Evaluate(new Requirement.Leaf("building-class", p.Key, p.Value, 1), new RequirementContext(sim, city.Owner, city)).Success);

    /// <summary>Whether the nation still has what founded the corporation: a city that can use a copy of a resource it
    /// consumes, or the buildings of a guild that consumes none. Its civics do not matter.</summary>
    internal static bool HasAccess(Simulation sim, int player, CorporationDef def, Pass? pass = null)
    {
        var cities = sim.State.Cities.Where(c => c.Owner == player).OrderBy(c => c.Id).ToArray();
        if (cities.Length == 0) return false;
        if (def.PrereqBonuses.Length == 0) return HoldsFoundingBuildings(sim, cities[0], def);
        return cities.Any(c => CorporationService.Consumed(def, Counts(sim, c, pass)) > 0);
    }

    // ---------------------------------------------------------------- spread

    /// <summary>The odds that the city takes a branch of the corporation this turn on its own. Zero when it cannot take
    /// one: it is no nation's, has the branch, the corporation is unfounded or obsolete for the owner, the owner's civics
    /// ban it (all corporations, or foreign ones when the headquarters is in another team), the city can use no copy of
    /// a resource it consumes, or a competitor's headquarters is there.
    /// <para>Otherwise, as the source's <c>CvCity::doCorporation</c>: each city with an active branch reaches out with
    /// its influence (100, plus each copy of the consumed resources it can use, plus
    /// <c>CORPORATION_RESOURCE_BASE_INFLUENCE</c> shared among the consumed resources it has; divided by
    /// <see cref="CorporationRulesDef.CompetitorInfluenceDivisor"/> for each active competitor there) times
    /// <see cref="CorporationDef.NaturalSpreadPercent"/>, raised by <c>FREE_TRADE_CORPORATION_SPREAD_MOD</c> across a
    /// free-trade border, and divided by max(1, <c>CORPORATION_SPREAD_DISTANCE_DIVISOR</c> x distance / the map's longest
    /// distance - <see cref="CorporationRulesDef.SpreadDistanceFloor"/>); the strongest reach counts. That is raised by
    /// <see cref="CorporationRulesDef.SupplyPercentPerCopy"/> for each copy the city can use (at most
    /// <see cref="CorporationRulesDef.SupplyCopiesCap"/>), which stands for the owner's supply where the source uses its
    /// influence, and divided by 1 + corporations already there / <see cref="CorporationRulesDef.CrowdingDivisor"/>. The
    /// draw is out of <c>CORPORATION_SPREAD_RAND</c> scaled to the pace.</para></summary>
    internal CorporationSpreadOdds SpreadOdds(Simulation sim, City city, CorporationDef def, Pass? pass = null)
    {
        pass ??= new Pass(sim, service);
        var rules = pass.Rules;
        int range = pass.Range;
        if (!CanReceive(sim, city, def, pass)) return new(0, range);
        int maxDistance = pass.MaximumDistance;
        int divisorPerTile = pass.DistanceDivisor;
        int freeTrade = pass.FreeTradePercent;
        int team = sim.TeamOf(city.Owner).Id;
        long threshold = 0;
        foreach (var holder in pass.Holders(def))
        {
            long spread = (long)pass.Influence(holder, def) * def.NaturalSpreadPercent / 100;
            int holderTeam = sim.TeamOf(holder.Owner).Id;
            if (holderTeam != team && sim.Rules().Relations.FreeTrade(sim, holderTeam, team)) spread = spread * (100 + freeTrade) / 100;
            if (spread <= 0) continue;
            int distance = sim.State.Grid.Distance(city.Position, holder.Position);
            long divisor = Math.Max(1, (long)divisorPerTile * distance / maxDistance - rules.SpreadDistanceFloor);
            threshold = Math.Max(threshold, spread / divisor);
        }
        long copies = Math.Min(rules.SupplyCopiesCap, CorporationService.Consumed(def, pass.Counts(city)));
        threshold = threshold * (100 + rules.SupplyPercentPerCopy * copies) / 100;
        threshold /= 1 + service.Present(sim, city.Id).Count / rules.CrowdingDivisor;
        return new((int)Math.Min(int.MaxValue, threshold), range);
    }

    private bool CanReceive(Simulation sim, City city, CorporationDef def, Pass pass)
    {
        if (!PlayerRules.IsNation(sim, city.Owner) || CorporationService.Founding(sim, def.Id) is not { } record) return false;
        if (service.Present(sim, city.Id).Contains(def.Id) || CorporationService.Obsolete(sim, city.Owner, def)) return false;
        if (CorporationService.Blocked(sim, city, CorporationService.HeadquartersTeam(sim, record))) return false;
        if (def.PrereqBonuses.Length > 0 && CorporationService.Consumed(def, pass.Counts(city)) <= 0) return false;
        return service.HeadquartersConflict(sim, city.Id, def) is null;
    }

    /// <summary>A memo for several read-only questions asked of one state.</summary>
    internal Pass BeginPass(Simulation sim) => new(sim, service);

    private static IReadOnlyDictionary<string, long> Counts(Simulation sim, City city, Pass? pass) =>
        pass?.Counts(city) ?? CorporationService.ConsumedCounts(sim, city, sim.Content.Get<CorporationDef>().All.ToArray());

    /// <summary>One turn's memo: each city's usable copies of every consumed resource (one resource query per city), the
    /// cities holding an active branch of each corporation as the pass began, and each holder's influence.</summary>
    internal sealed class Pass(Simulation sim, CorporationService service)
    {
        private readonly CorporationDef[] _all = sim.Content.Get<CorporationDef>().All.OrderBy(c => c.Id, StringComparer.Ordinal).ToArray();
        private readonly Dictionary<int, IReadOnlyDictionary<string, long>> _counts = [];
        private readonly Dictionary<string, City[]> _holders = new(StringComparer.Ordinal);
        private readonly Dictionary<(int, string), int> _influence = [];

        /// <summary>The mode's numbers and the source constants the spread reads, read once per pass.</summary>
        public CorporationRulesDef Rules { get; } = NaturalCorporations.Rules(sim.Content);
        public int Range { get; } = Math.Max(1, (int)((long)SourceConstant(sim.Content, "CORPORATION_SPREAD_RAND") * GamePace.Percent(sim) / 100));
        public int MaximumDistance { get; } = Math.Max(1, sim.State.Grid.MaximumDistance);
        public int DistanceDivisor { get; } = SourceConstant(sim.Content, "CORPORATION_SPREAD_DISTANCE_DIVISOR");
        public int FreeTradePercent { get; } = SourceConstant(sim.Content, "FREE_TRADE_CORPORATION_SPREAD_MOD");
        private readonly int _baseInfluence = SourceConstant(sim.Content, "CORPORATION_RESOURCE_BASE_INFLUENCE");

        public IReadOnlyDictionary<string, long> Counts(City city)
        {
            if (!_counts.TryGetValue(city.Id, out var counts)) _counts[city.Id] = counts = CorporationService.ConsumedCounts(sim, city, _all);
            return counts;
        }

        private bool Active(City city, CorporationDef def) => service.IsOperating(sim, city.Id, def.Id)
            && (def.PrereqBonuses.Length == 0 || CorporationService.Consumed(def, Counts(city)) > 0);

        public IReadOnlyList<City> Holders(CorporationDef def)
        {
            if (!_holders.TryGetValue(def.Id, out var holders))
                _holders[def.Id] = holders = sim.State.Cities.Where(c => service.Present(sim, c.Id).Contains(def.Id)).OrderBy(c => c.Id)
                    .Where(c => Active(c, def)).ToArray();
            return holders;
        }

        /// <summary>The source's <c>CvCity::getCorporationInfluence</c> for a city holding the branch.</summary>
        public int Influence(City city, CorporationDef def)
        {
            if (_influence.TryGetValue((city.Id, def.Id), out int value)) return value;
            var counts = Counts(city);
            long total = CorporationService.Consumed(def, counts);
            long influence = 0;
            if (def.PrereqBonuses.Length == 0 || total > 0)
            {
                influence = 100 + total;
                int consumed = def.PrereqBonuses.Length;
                if (consumed > 0)
                {
                    int share = _baseInfluence / consumed;
                    influence += share * def.PrereqBonuses.Count(b => counts.GetValueOrDefault(b) > 0);
                }
                foreach (var other in service.Present(sim, city.Id).Order(StringComparer.Ordinal))
                {
                    var rival = sim.Content.Get<CorporationDef>(other);
                    if (!CorporationService.Competes(def, rival) || !Active(city, rival)) continue;
                    if (service.Headquarters(sim, other) == city.Id) { influence = 0; break; }
                    influence /= Rules.CompetitorInfluenceDivisor;
                }
            }
            value = (int)Math.Min(int.MaxValue, influence);
            _influence[(city.Id, def.Id)] = value;
            return value;
        }
    }

    // ---------------------------------------------------------------- the turn

    /// <summary>One turn of the realistic mode: decay timers first, then founding, then spread, each in a stable order;
    /// nothing in the manual mode.</summary>
    internal void Run(Simulation sim)
    {
        if (!CorporationMode.Realistic(sim)) return;
        var pass = new Pass(sim, service);
        var all = sim.Content.Get<CorporationDef>().All.OrderBy(c => c.Id, StringComparer.Ordinal).ToArray();
        Decay(sim, all, pass);
        Found(sim, all, pass);
        Spread(sim, all, new Pass(sim, service));
    }

    /// <summary>Starts a decay timer where a branch or a headquarters is without access, and ends it where access came
    /// back. What happens when one runs out is the timer kind's (<see cref="BranchDecay"/>, <see cref="HeadquartersDecay"/>).</summary>
    private void Decay(Simulation sim, IReadOnlyList<CorporationDef> all, Pass pass)
    {
        var timers = sim.Rules().Timers;
        var rules = Rules(sim.Content);
        foreach (var def in all)
        {
            if (service.Headquarters(sim, def.Id) is not { } hq) continue;
            foreach (var city in sim.State.Cities.Where(c => c.Id != hq && service.Present(sim, c.Id).Contains(def.Id)).OrderBy(c => c.Id))
            {
                bool without = def.PrereqBonuses.Length > 0 && CorporationService.Consumed(def, pass.Counts(city)) <= 0;
                Hold(sim, timers, BranchDecayKind, TimerHolder.OfCity(city.Id), def.Id, without, rules.BranchDecayTurns);
            }
            var seat = sim.State.CityById(hq);
            Hold(sim, timers, HeadquartersDecayKind, TimerHolder.OfCity(hq), def.Id, !HasAccess(sim, seat.Owner, def, pass), rules.HeadquartersDecayTurns);
        }
    }

    private static void Hold(Simulation sim, ITimers timers, string kind, TimerHolder holder, string key, bool running, int turns)
    {
        var timer = timers.Find(sim, kind, holder, key);
        if (running && timer is null) timers.Start(sim, new GameTimer { Kind = kind, Key = key, Holder = holder, ExpiresTurn = timers.Expiry(sim, turns) });
        else if (!running && timer is not null) timers.Cancel(sim, timer.Id);
    }

    private void Found(Simulation sim, IReadOnlyList<CorporationDef> all, Pass pass)
    {
        int mean = GamePace.Scale(sim, Rules(sim.Content).FoundingMeanTurns);
        var players = sim.State.Players.Where(p => PlayerRules.IsNation(sim, p.Id)).Select(p => p.Id).Order().ToArray();
        foreach (var def in all)
        {
            if (service.IsFounded(sim, def.Id)) continue;
            foreach (int player in players)
            {
                if (FoundingSite(sim, player, def, pass: pass) is not { } site) continue;
                if (sim.Rng.Next(mean) != 0) continue;
                service.FoundWithoutBuilding(sim, site, def);
                break;
            }
        }
    }

    /// <summary>At most one new branch per city a turn, as in the source: cities in id order, corporations in id order,
    /// holders as they were when the pass began.</summary>
    private void Spread(Simulation sim, IReadOnlyList<CorporationDef> all, Pass pass)
    {
        var founded = all.Where(d => service.IsFounded(sim, d.Id)).ToArray();
        if (founded.Length == 0) return;
        foreach (var def in founded) pass.Holders(def);
        foreach (var city in sim.State.Cities.OrderBy(c => c.Id).ToArray())
        {
            foreach (var def in founded)
            {
                var odds = SpreadOdds(sim, city, def, pass);
                if (odds.Threshold <= 0 || sim.Rng.Next(odds.Range) >= odds.Threshold) continue;
                CorporationService.Add(sim, city, def);
                sim.Events.Publish(new CorporationSpread(def.Id, city.Id, city.Owner));
                break;
            }
        }
    }

    // ---------------------------------------------------------------- what a timer does when it runs out

    /// <summary>A branch outside the headquarters closes when its city still can use no copy of its resources.</summary>
    internal sealed class BranchDecay(CorporationService service) : TimerKind(BranchDecayKind)
    {
        public override void Ended(Simulation sim, GameTimer timer, TimerEnd reason)
        {
            if (reason != TimerEnd.Expired || !CorporationMode.Realistic(sim) || timer.Holder.City is not int id || sim.State.FindCity(id) is not { } city) return;
            if (!sim.Content.TryGet<CorporationDef>(timer.Key, out var def) || service.Headquarters(sim, def.Id) == city.Id) return;
            if (def.PrereqBonuses.Length > 0 && service.ConsumedBonusCount(sim, city.Id, def.Id) <= 0) CorporationService.Withdraw(sim, city, def.Id);
        }
    }

    /// <summary>The corporation dissolves when its headquarters' nation still has no access; that nation then waits out a
    /// cooldown before it can found it again. A captured headquarters ends the timer, and the next turn weighs the new
    /// owner's access.</summary>
    internal sealed class HeadquartersDecay(CorporationService service) : TimerKind(HeadquartersDecayKind)
    {
        public override void Ended(Simulation sim, GameTimer timer, TimerEnd reason)
        {
            if (reason != TimerEnd.Expired || !CorporationMode.Realistic(sim) || timer.Holder.City is not int id || sim.State.FindCity(id) is not { } city) return;
            if (!sim.Content.TryGet<CorporationDef>(timer.Key, out var def) || service.Headquarters(sim, def.Id) != city.Id) return;
            if (HasAccess(sim, city.Owner, def)) return;
            CorporationService.Dissolve(sim, def.Id, city.Id, city.Owner);
            int cooldown = Rules(sim.Content).FoundingCooldownTurns;
            if (cooldown > 0)
            {
                var timers = sim.Rules().Timers;
                timers.Start(sim, new GameTimer { Kind = CooldownKind, Key = def.Id, Holder = TimerHolder.OfPlayer(city.Owner), ExpiresTurn = timers.Expiry(sim, cooldown) });
            }
        }
    }
}

/// <summary>The realistic mode's turn (<see cref="NaturalCorporations.Run"/>), with the holy cities and headquarters.</summary>
internal sealed class NaturalCorporationTurn(NaturalCorporations natural) : ITurnSystem
{
    public string Id => "corporations:natural";
    public int Order => TurnPhase.Environment;
    public void Run(Simulation sim) => natural.Run(sim);
}
