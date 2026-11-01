using Ariel.Content.Schema;
using Ariel.Game;

namespace Ariel.Mods.Ai;

/// <summary>Chooses wars by what can be won, prepares the army at the border before declaring, and ends wars by how
/// they are going rather than by how long they have lasted.</summary>
internal sealed class WarPlanner(AiContext ctx, ArmyPlan army)
{
    private const int CheckInterval = 5, MinimumTurn = 45;
    internal const int PreparationTurns = 5, MaximumPreparationTurns = 16, StagedSoldiers = 3, StagingRadius = 3;
    internal const int LongWarTurns = 40, WinningScore = 150, LosingScore = -150;

    public void Plan()
    {
        // A treaty signed this turn also blocks new wars this turn, so one turn never holds both peace and war.
        if (MakePeace()) return;
        if (AiIntent.Read(ctx).War is { } preparation) { Prepare(preparation); return; }
        if (ctx.Turn < MinimumTurn || (ctx.Turn + ctx.PlayerId) % CheckInterval != 0 || ctx.AtWarWithPlayers) return;
        if (army.Soldiers < 5 + ctx.Cities.Count) return;
        var target = ChooseTarget();
        if (target is { } victim && Roll(victim)) ctx.Issue(new PrepareWarCommand { Player = ctx.PlayerId, Target = victim });
    }

    /// <summary>Keep building and moving the army until enough soldiers stand at the border city, then declare. Give up
    /// when the target is no longer a good choice or preparation has taken too long.</summary>
    private void Prepare(WarPreparation preparation)
    {
        int age = ctx.Turn - preparation.StartedTurn;
        bool still = !ctx.AtWarWithPlayers && Candidate(preparation.Target) && Favourable(preparation.Target);
        if (!still || age > MaximumPreparationTurns)
        {
            ctx.Issue(new PrepareWarCommand { Player = ctx.PlayerId });
            return;
        }
        if (age < PreparationTurns || Staged(preparation.Target) < StagedSoldiers) return;
        if (ctx.Issue(new DeclareWarCommand { Player = ctx.PlayerId, Target = preparation.Target }))
            ctx.Issue(new PrepareWarCommand { Player = ctx.PlayerId });
    }

    /// <summary>The own city closest to the target's nearest known city: where the army gathers before a declaration.</summary>
    public City? BorderCity(int target)
    {
        var theirs = KnownCities(target).ToList();
        if (theirs.Count == 0) return null;
        return ctx.Cities.OrderBy(c => theirs.Min(t => ctx.Grid.Distance(c.Position, t.Position))).ThenBy(c => c.Id).FirstOrDefault();
    }

    /// <summary>Soldiers within reach of the border city.</summary>
    public int Staged(int target) => BorderCity(target) is { } border
        ? ctx.Units.Count(u => ctx.Role(u) is UnitRole.Melee or UnitRole.Siege && u.Health > 0
            && ctx.Grid.Distance(u.Position, border.Position) <= StagingRadius) - army.DesiredGarrison(ctx.ViewOf(border))
        : 0;

    /// <summary>Higher MaxWarRand means a more reluctant leader. Overwhelming advantage increases the urgency.</summary>
    private bool Roll(int target)
    {
        long theirs = Math.Max(1, AiContext.Power(ctx.Sim, target));
        int advantage = (int)Math.Clamp(AiContext.Power(ctx.Sim, ctx.PlayerId) / theirs, 1, 8);
        int delay = Math.Max(2, (ctx.Leader?.MaxWarRand ?? 100) / (8 * advantage));
        return ctx.Sim.Rng.Next(delay) == 0;
    }

    private int Need => 150 + 8 * (ctx.Leader?.BasePeaceWeight ?? 3);

    private long Ratio(int player) => AiContext.Power(ctx.Sim, ctx.PlayerId) * 100 / Math.Max(1, AiContext.Power(ctx.Sim, player));

    /// <summary>Still strong enough, with a little slack so preparation does not flicker on and off.</summary>
    private bool Favourable(int player) => AttitudeNeed(player) is { } shift && Ratio(player) >= Need + shift + AllyShift(player) - 20;

    /// <summary>Among rivals weak enough to attack, the one whose nearby cities are worth the most.</summary>
    public int? ChooseTarget()
    {
        int? best = null; long bestScore = long.MinValue;
        foreach (var other in ctx.Sim.State.Players.Where(p => Candidate(p.Id)).OrderBy(p => p.Id))
        {
            if (AttitudeNeed(other.Id) is not { } shift) continue;
            long ratio = Ratio(other.Id);
            long need = Need + shift + AllyShift(other.Id);
            if (ratio < need) continue;
            long score = TargetValue(other.Id) + Math.Min(300, ratio - need) / 2;
            if (score <= bestScore) continue;
            best = other.Id; bestScore = score;
        }
        return best;
    }

    /// <summary>What the target's three nearest known cities are worth: population, wonders, resources in reach,
    /// and holy cities, less their distance.</summary>
    public long TargetValue(int player)
    {
        var religion = ctx.Sim.Content.Services.Get<IReligionService>().SingleOrDefault();
        var holy = religion is null ? new HashSet<int>() : ctx.Sim.Content.Get<ReligionDef>().All
            .Select(r => religion.HolyCity(ctx.Sim, r.Id)).OfType<int>().ToHashSet();
        return KnownCities(player).Select(c => (City: c, Distance: ctx.Cities.Min(own => ctx.Grid.Distance(own.Position, c.Position))))
            .OrderBy(x => x.Distance).ThenBy(x => x.City.Id).Take(3)
            .Sum(x => CityValue(x.City, holy) - 6L * x.Distance);
    }

    private long CityValue(City city, HashSet<int> holy)
    {
        long value = 10L * city.Population;
        value += 25L * city.Buildings.Count(b => CityBuildingRules.IsWonder(ctx.Sim.Content.Get<BuildingDef>(b)));
        value += 8L * ctx.Grid.Range(ctx.Tile(city.Position), 2).Count(t => ctx.Sim.State.Plots[t.Value].Bonus is not null
            && ctx.Sim.State.Plots[t.Value].Revealed.Contains(ctx.Team.Id));
        if (holy.Contains(city.Id)) value += 40;
        return value;
    }

    /// <summary>A rival already at war with one of our defensive-pact partners or friends is a target worth joining
    /// against: the bar drops by 30 points.</summary>
    public int AllyShift(int player)
    {
        var diplomacy = ctx.Sim.Content.Services.Get<IDiplomacyService>().SingleOrDefault();
        if (diplomacy is null) return 0;
        int theirTeam = ctx.Sim.State.PlayerById(player).TeamId;
        bool allyAtWar = ctx.Sim.State.Players.Any(p => p.Id != ctx.PlayerId && p.TeamId != ctx.Self.TeamId && p.TeamId != theirTeam
            && !WarRules.IsBarbarian(ctx.Sim, p.Id) && ctx.Sim.State.TeamById(p.TeamId).Wars.Contains(theirTeam)
            && (diplomacy.HasDefensivePact(ctx.Sim, ctx.Self.TeamId, p.TeamId)
                || diplomacy.GetAttitude(ctx.Sim, ctx.PlayerId, p.Id).Tier == AttitudeTier.Friendly));
        return allyAtWar ? -30 : 0;
    }

    /// <summary>Extra power advantage (percent) wanted before attacking a player, from this leader's attitude:
    /// a grudge lowers the bar, goodwill raises it, and a friend or a defensive-pact partner is never attacked.</summary>
    public int? AttitudeNeed(int player)
    {
        var diplomacy = ctx.Sim.Content.Services.Get<IDiplomacyService>().SingleOrDefault();
        if (diplomacy is null) return 0;
        if (diplomacy.HasDefensivePact(ctx.Sim, ctx.Self.TeamId, ctx.Sim.State.PlayerById(player).TeamId)) return null;
        return diplomacy.GetAttitude(ctx.Sim, ctx.PlayerId, player).Tier switch
        {
            AttitudeTier.Furious => -40,
            AttitudeTier.Annoyed => -20,
            AttitudeTier.Pleased => 40,
            AttitudeTier.Friendly => null,
            _ => 0,
        };
    }

    private IEnumerable<City> KnownCities(int player)
    {
        var areas = ctx.Cities.Select(c => ctx.Sim.State.PlotAt(c.Position).AreaId).ToHashSet();
        return ctx.Sim.State.Cities.Where(c => c.Owner == player && areas.Contains(ctx.Sim.State.PlotAt(c.Position).AreaId)
            && ctx.Sim.State.PlotAt(c.Position).Revealed.Contains(ctx.Team.Id));
    }

    private bool Candidate(int player)
    {
        if (player == ctx.PlayerId || WarRules.IsBarbarian(ctx.Sim, player) || ctx.Sim.State.PlayerById(player).TeamId == ctx.Self.TeamId) return false;
        if (ctx.Cities.Count == 0) return false;
        var theirs = KnownCities(player).ToList();
        return theirs.Count > 0 && theirs.Min(c => ctx.Cities.Min(own => ctx.Grid.Distance(own.Position, c.Position))) <= 14;
    }

    /// <summary>Peace with a team when the war is being lost (in power or in results), or when a long war has stopped
    /// paying: after <see cref="LongWarTurns"/> turns only a war whose score keeps growing goes on. Returns true when a treaty was signed.</summary>
    private bool MakePeace()
    {
        long mine = ctx.Team.Members.Where(m => !WarRules.IsBarbarian(ctx.Sim, m)).Sum(m => AiContext.Power(ctx.Sim, m));
        var record = WarRules.Read(ctx.Sim, ctx.Self.TeamId);
        bool signed = false;
        foreach (var teamId in ctx.Team.Wars.ToList())
        {
            var enemies = ctx.Sim.State.TeamById(teamId).Members.Where(m => !WarRules.IsBarbarian(ctx.Sim, m)).ToList();
            if (enemies.Count == 0) continue;
            long theirs = enemies.Sum(m => AiContext.Power(ctx.Sim, m));
            int score = record.WarScore.GetValueOrDefault(teamId);
            int length = ctx.Sim.State.Turn - record.WarStarted.GetValueOrDefault(teamId);
            if (WantsPeace(mine, theirs, score, length)
                && ctx.Issue(new MakePeaceCommand { Player = ctx.PlayerId, Target = enemies[0] })) signed = true;
        }
        return signed;
    }

    public static bool WantsPeace(long mine, long theirs, int score, int length)
    {
        bool outgunned = mine * 100 < theirs * 85;
        bool beaten = score <= LosingScore;
        // The longer the war, the more it must be paying to go on.
        bool stalled = length > LongWarTurns && score < (long)WinningScore * length / LongWarTurns;
        return outgunned || beaten || stalled;
    }
}
