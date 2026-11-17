using Ariel.Content.Schema;
using Ariel.Game;

namespace Ariel.Ai;

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

    /// <summary>Soldiers within reach of the border city, beyond the ones it keeps as its garrison.</summary>
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

    /// <summary>The power advantage (percent) this leader wants before any war, from its love of peace.</summary>
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

    /// <summary>What the target's three nearest known cities are worth (<see cref="CitySubject"/>): population,
    /// wonders, resources in reach and what valuers add, less their distance.</summary>
    public long TargetValue(int player) =>
        KnownCities(player).Select(c => (City: c, Distance: ctx.Cities.Min(own => ctx.Grid.Distance(own.Position, c.Position))))
            .OrderBy(x => x.Distance).ThenBy(x => x.City.Id).Take(3)
            .Sum(x => CityValue(x.City) - 6L * x.Distance);

    private long CityValue(City city)
    {
        long value = 10L * city.Population;
        value += 25L * city.Buildings.Count(b => CityBuildingRules.IsWonder(ctx.Sim.Content.Get<BuildingDef>(b)));
        value += 8L * ctx.Grid.Range(ctx.Tile(city.Position), 2).Count(t => ctx.Sim.State.Plots[t.Value].Bonus is not null
            && ctx.Sim.State.Plots[t.Value].Revealed.Contains(ctx.Team.Id));
        return value + ctx.Adjustment(new CitySubject(city));
    }

    /// <summary>A rival already at war with one of our allies or with a player this leader is Friendly with is a target
    /// worth joining against: the bar drops by 30 points.</summary>
    public int AllyShift(int player)
    {
        var relations = ctx.Sim.Rules().Relations;
        int theirTeam = ctx.Sim.State.PlayerById(player).TeamId;
        bool allyAtWar = ctx.Sim.State.Players.Any(p => p.TeamId != ctx.Self.TeamId && p.TeamId != theirTeam
            && !PlayerRules.IsBarbarian(ctx.Sim, p.Id) && ctx.Sim.State.TeamById(p.TeamId).Wars.Contains(theirTeam)
            && (relations.Allied(ctx.Sim, ctx.Self.TeamId, p.TeamId)
                || relations.DispositionToward(ctx.Sim, ctx.PlayerId, p.Id) == Disposition.Friendly));
        return allyAtWar ? -30 : 0;
    }

    /// <summary>Extra power advantage (percent) wanted before attacking a player, from how this leader regards it:
    /// a grudge lowers the bar, goodwill raises it, and a friend or an ally is never attacked.</summary>
    public int? AttitudeNeed(int player)
    {
        var relations = ctx.Sim.Rules().Relations;
        if (relations.Allied(ctx.Sim, ctx.Self.TeamId, ctx.Sim.State.PlayerById(player).TeamId)) return null;
        return relations.DispositionToward(ctx.Sim, ctx.PlayerId, player) switch
        {
            Disposition.Furious => -40,
            Disposition.Annoyed => -20,
            Disposition.Pleased => 40,
            Disposition.Friendly => null,
            _ => 0,
        };
    }

    /// <summary>The player's cities on a landmass we have cities on, on plots our team has revealed.</summary>
    private IEnumerable<City> KnownCities(int player)
    {
        var areas = ctx.Cities.Select(c => ctx.Sim.State.PlotAt(c.Position).AreaId).ToHashSet();
        return ctx.Sim.State.Cities.Where(c => c.Owner == player && areas.Contains(ctx.Sim.State.PlotAt(c.Position).AreaId)
            && ctx.Sim.State.PlotAt(c.Position).Revealed.Contains(ctx.Team.Id));
    }

    private bool Candidate(int player)
    {
        if (player == ctx.PlayerId || PlayerRules.IsBarbarian(ctx.Sim, player) || ctx.Sim.State.PlayerById(player).TeamId == ctx.Self.TeamId) return false;
        if (ctx.Cities.Count == 0) return false;
        var theirs = KnownCities(player).ToList();
        return theirs.Count > 0 && theirs.Min(c => ctx.Cities.Min(own => ctx.Grid.Distance(own.Position, c.Position))) <= 14;
    }

    /// <summary>Peace with a team when the war is being lost (in power or in results), or when a long war has stopped
    /// paying: after <see cref="LongWarTurns"/> turns only a war whose score keeps growing goes on. Returns true when a
    /// treaty was signed.</summary>
    private bool MakePeace()
    {
        bool signed = false;
        foreach (var teamId in ctx.Team.Wars.ToList())
        {
            var enemies = ctx.Sim.State.TeamById(teamId).Members.Where(m => !PlayerRules.IsBarbarian(ctx.Sim, m)).ToList();
            if (enemies.Count == 0) continue;
            if (ctx.WantsPeace(teamId) && ctx.Issue(new MakePeaceCommand { Player = ctx.PlayerId, Target = enemies[0] })) signed = true;
        }
        return signed;
    }

    /// <summary>Whether a war with this balance of power, score and length should end.</summary>
    public static bool WantsPeace(long mine, long theirs, int score, int length)
    {
        bool outgunned = mine * 100 < theirs * 85;
        bool beaten = score <= LosingScore;
        // The longer the war, the more it must be paying to go on.
        bool stalled = length > LongWarTurns && score < (long)WinningScore * length / LongWarTurns;
        return outgunned || beaten || stalled;
    }
}
