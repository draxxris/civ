using Civ.Game;
using Schema = Civ.Content.Schema;

namespace Civ.Client;

/// <summary>Odds of the combat a move would start, as shown before committing.</summary>
public sealed record AttackPreview(int DefenderId, string DefenderName, int WinPercent, int DamageToAttacker, int DamageToDefender);

/// <summary>What a right click on a tile would do: the route, how many turns it takes, and the fight at its end if any.</summary>
public sealed record MovePreview(IReadOnlyList<Hex> Path, int Turns, AttackPreview? Attack)
{
    public string Summary => Attack is { } a
        ? $"Attack {a.DefenderName}: {a.WinPercent}% to win"
        : Turns <= 1 ? "Arrives this turn" : $"{Turns} turns";
}

/// <summary>One button in the unit panel. A null <see cref="Command"/> means unavailable, with the reason in <see cref="Disabled"/>.</summary>
public sealed record UnitActionInfo(string Id, string Label, GameCommand? Command, string? Disabled = null, string Tooltip = "");

public sealed record UnitPanel(
    int Id, string Name, string Stats, string Moves, int Health, string Experience, string Activity,
    IReadOnlyList<string> Promotions, IReadOnlyList<UnitActionInfo> Actions);

public sealed partial class GameSession
{
    public int GoldenAgeTurnsRemaining => OptionalService<IGreatPeopleService>()?.GoldenAgeTurnsRemaining(Sim, Human) ?? 0;

    public Schema.UnitDef DefOf(Unit unit) => Sim.Content.Get<Schema.UnitDef>(unit.Type);
    /// <summary>Route and combat odds for sending the selected unit to a tile, or null with no unit or route.</summary>
    public MovePreview? PreviewAt(Hex hex)
    {
        var unit = Unit;
        if (unit == null || !Sim.State.Grid.TryTile(hex, out _)) return null;
        hex = Sim.State.Grid.Normalize(hex);
        var defender = Service<ICombatService>().BestDefender(Sim, unit.Id, hex);
        var path = Service<IPathfindingService>().FindPath(Sim, unit.Id, hex, attack: defender != null);
        if (path == null || path.Steps.Count == 0) return null;
        return new(path.Steps, TurnsFor(unit, path.Cost), defender is int d ? AttackOdds(unit, d) : null);
    }

    private int TurnsFor(Unit unit, long cost)
    {
        long perTurn = Math.Max(1, Service<IUnitService>().MaximumMoves(Sim, unit.Id));
        long left = Math.Max(0, unit.MovesLeft);
        return cost <= left ? 1 : 1 + (int)((cost - left + perTurn - 1) / perTurn);
    }

    private AttackPreview AttackOdds(Unit attacker, int defenderId)
    {
        var p = Service<ICombatService>().Preview(Sim, attacker.Id, defenderId);
        var defender = Sim.State.UnitById(defenderId);
        return new(defenderId, DefOf(defender).Name, (p.SuccessOdds + 5000) / 10000, p.DamageToAttacker, p.DamageToDefender);
    }

    public UnitPanel? DescribeUnit(Unit? unit)
    {
        if (unit == null) return null;
        var def = DefOf(unit);
        var state = unit.Extensions.Get<MilitaryUnitState>(Sim.Content);
        var units = Service<IUnitService>();
        string xp = $"{unit.Experience}/{units.ExperienceNeeded(Sim, unit.Id)} XP, level {state?.Level ?? 1}";
        string stats = def.Strength > 0 ? $"Strength {def.Strength}" : "Non-combat";
        var promotions = unit.Promotions.Select(p => Sim.Content.Get<Schema.PromotionDef>(p).Name).ToList();
        return new(unit.Id, def.Name, stats, MovesText(unit, units.MaximumMoves(Sim, unit.Id)), unit.Health, xp,
            (state?.Activity ?? UnitActivity.Awake).ToString(), promotions, UnitActions(unit, def));
    }

    private string MovesText(Unit unit, int maximum)
    {
        double den = MoveDenominator;
        return $"{unit.MovesLeft / den:0.#}/{maximum / den:0.#}";
    }

    public IReadOnlyList<UnitActionInfo> UnitActions(Unit unit, Schema.UnitDef def)
    {
        var actions = new List<UnitActionInfo>();
        if (def.CanFoundCity) actions.Add(Action("found", "Found city", new FoundCityWithSettlerCommand { Player = Human, Settler = unit.Id }));
        actions.AddRange(BuildActions(unit, def));
        foreach (var activity in new[] { UnitActivity.Fortify, UnitActivity.Sleep, UnitActivity.Awake })
            actions.Add(Action(activity.ToString().ToLowerInvariant(), activity == UnitActivity.Awake ? "Wake" : activity.ToString(),
                new SetUnitActivityCommand { Player = Human, Unit = unit.Id, Activity = activity }));
        actions.AddRange(PromotionActions(unit));
        actions.AddRange(ReligionActions(unit));
        actions.AddRange(GreatPeopleActions(unit));
        return actions.Where(a => a.Command != null || a.Id == "found").ToList();
    }

    private IEnumerable<UnitActionInfo> GreatPeopleActions(Unit unit)
    {
        var service = OptionalService<IGreatPeopleService>();
        if (service == null) yield break;
        foreach (var action in service.AvailableActions(Sim, Human, unit.Id))
        {
            string suffix = action.Command[(action.Command.LastIndexOf(':') + 1)..];
            GameCommand? command = suffix switch
            {
                "join_city" => new JoinGreatPersonCommand { Player = Human, UnitId = unit.Id, Target = action.Target },
                "discover_tech" => new DiscoverGreatPersonCommand { Player = Human, UnitId = unit.Id, Target = action.Target },
                "hurry_production" => new HurryGreatPersonCommand { Player = Human, UnitId = unit.Id, Target = action.Target },
                "culture_bomb" => new CultureGreatPersonCommand { Player = Human, UnitId = unit.Id, Target = action.Target },
                "golden_age" => new GoldenAgeGreatPersonCommand { Player = Human, UnitId = unit.Id, Target = action.Target },
                _ => null,
            };
            if (command == null) continue;
            string label = suffix switch
            {
                "join_city" => "Join city",
                "discover_tech" => "Discover technology",
                "hurry_production" => "Hurry production",
                "culture_bomb" => "Great work",
                "golden_age" => "Start golden age",
                _ => suffix,
            };
            yield return Action(action.Command, label, command, action.Amount > 0 ? action.Amount.ToString() : "");
        }
    }

    private UnitActionInfo Action(string id, string label, GameCommand command, string tooltip = "")
    {
        var error = command.Validate(Sim);
        return new(id, label, error == null ? command : null, error, tooltip);
    }

    private IEnumerable<UnitActionInfo> BuildActions(Unit unit, Schema.UnitDef def)
    {
        var builds = OptionalService<IWorkerBuildService>();
        if (builds == null || unit.MovesLeft <= 0) yield break;
        var tile = Sim.State.Grid.TileAt(unit.Position);
        foreach (var id in def.Builds.Where(b => b.Value).Select(b => b.Key).Order(StringComparer.Ordinal))
        {
            var build = Sim.Content.Get<Schema.BuildDef>(id);
            if (builds.Validate(Sim, Human, unit.Id, id) != null) continue;
            int work = builds.RequiredWork(Sim, tile, id);
            yield return new("build:" + id, build.Name, new StartBuildCommand { Player = Human, Unit = unit.Id, Build = id }, null, $"{work} work");
        }
    }

    private IEnumerable<UnitActionInfo> PromotionActions(Unit unit)
    {
        foreach (var promotion in Sim.Content.Get<Schema.PromotionDef>().All.OrderBy(p => p.Id, StringComparer.Ordinal))
        {
            var command = new PromoteMilitaryUnitCommand { Player = Human, Unit = unit.Id, Promotion = promotion.Id };
            if (command.Validate(Sim) == null) yield return new("promote:" + promotion.Id, "Promote: " + promotion.Name, command, null, promotion.Help);
        }
    }

    /// <summary>Run one of the actions from <see cref="UnitActions"/> by id; the unit stays selected unless it was consumed.</summary>
    public void RunAction(string actionId)
    {
        var unit = Unit;
        if (unit == null) return;
        var action = UnitActions(unit, DefOf(unit)).FirstOrDefault(a => a.Id == actionId);
        if (action?.Command == null) { SetMessage(action?.Disabled is { } why ? $"Can't do that: {why}" : "Unavailable"); return; }
        _gotos.Remove(unit.Id);
        if (Submit(action.Command) && !actionId.StartsWith("promote:", StringComparison.Ordinal) && actionId != "found") SelectNextUnit();
    }

    /// <summary>Settlers' best advice: why founding fails here, or null when it works.</summary>
    public string? CannotFoundReason(Unit unit) => new FoundCityWithSettlerCommand { Player = Human, Settler = unit.Id }.Validate(Sim);
}
