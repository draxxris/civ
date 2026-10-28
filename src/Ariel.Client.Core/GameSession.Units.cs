using Ariel.Game;
using Schema = Ariel.Content.Schema;

namespace Ariel.Client;

/// <summary>Odds of the combat a move would start, as shown before committing.</summary>
public sealed record AttackPreview(int DefenderId, string DefenderName, int WinPercent, int DamageToAttacker, int DamageToDefender);

/// <summary>
/// What a right click would do: the route, per-step arrival turns (1 is this turn), and the fight at its end if any.
/// StepTurns aligns with Path and uses the pathfinder's estimates.
/// </summary>
public sealed record MovePreview(IReadOnlyList<Hex> Path, IReadOnlyList<int> StepTurns, AttackPreview? Attack)
{
    /// <summary>The estimated arrival turn at the destination, relative to this turn (1).</summary>
    public int Turns => StepTurns.Count == 0 ? 1 : StepTurns[^1];
    public string Summary => Attack is { } a
        ? $"Attack {a.DefenderName}: {a.WinPercent}% to win"
        : Turns <= 1 ? "Arrives this turn" : $"{Turns} turns";
}

/// <summary>One button in the unit panel. A null <see cref="Command"/> means unavailable, with the reason in <see cref="Disabled"/>.</summary>
public sealed record UnitActionInfo(string Id, string Label, GameCommand? Command, string? Disabled = null, string Tooltip = "");

public sealed record UnitPanel(
    int Id, string Name, string Stats, string Moves, int Health, string Experience, string Activity,
    IReadOnlyList<string> Promotions, IReadOnlyList<UnitActionInfo> Actions);

/// <summary>An earned promotion as the stacked-unit list draws it: the id names the art, the name is the tooltip.</summary>
public sealed record UnitRank(string Id, string Name);

/// <summary>One row of the left pane's stacked-unit list: the art id, the name, the health and the ranks earned so far.</summary>
public sealed record StackedUnit(int Id, string Type, string Name, int Health, IReadOnlyList<UnitRank> Ranks);

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
        return new(path.Steps, path.StepTurns, defender is int d ? AttackOdds(unit, d) : null);
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

    /// <summary>
    /// The stack on the selected unit's tile: my units there in the id order <see cref="SelectAt"/> cycles through them,
    /// or nothing when the tile holds a single one, which needs no list beside its detail. A row carries the little a
    /// stack row draws rather than a whole <see cref="UnitPanel"/>, whose actions would scan the map once per unit.
    /// </summary>
    public IReadOnlyList<StackedUnit> UnitStack()
    {
        if (Unit is not { } selected) return [];
        var units = Sim.State.UnitsAt(selected.Position).Where(u => u.Owner == Human).OrderBy(u => u.Id).ToList();
        if (units.Count < 2) return [];
        return units.Select(unit => new StackedUnit(unit.Id, unit.Type, DefOf(unit).Name, unit.Health,
            unit.Promotions.Select(p => new UnitRank(p, Sim.Content.Get<Schema.PromotionDef>(p).Name)).ToList())).ToList();
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
        foreach (var activity in new[] { UnitActivity.Fortify, UnitActivity.Sleep, UnitActivity.Awake, UnitActivity.Intercept, UnitActivity.Blockade })
            actions.Add(Action(activity.ToString().ToLowerInvariant(), activity == UnitActivity.Awake ? "Wake" : activity.ToString(),
                new SetUnitActivityCommand { Player = Human, Unit = unit.Id, Activity = activity }));
        if (def.Pillage) actions.Add(Action("pillage", "Pillage", new PillageCommand { Player = Human, Unit = unit.Id }));
        actions.AddRange(ArtilleryActions(unit, def));
        actions.AddRange(AirActions(unit, def));
        actions.AddRange(CargoActions(unit));
        actions.AddRange(PromotionActions(unit));
        actions.AddRange(ReligionActions(unit));
        actions.AddRange(GreatPeopleActions(unit));
        actions.AddRange(CorporationActions(unit));
        actions.AddRange(SpyActions(unit));
        return actions.Where(a => a.Command != null || a.Id == "found").ToList();
    }

    private IEnumerable<UnitActionInfo> AirActions(Unit unit, Schema.UnitDef def)
    {
        var air = OptionalService<IAirMissionService>();
        if (air is null || air.AirRange(Sim, unit.Id) <= 0) yield break;
        var origin = Sim.State.Grid.TileAt(unit.Position);
        foreach (var tile in Sim.State.Grid.Range(origin, air.AirRange(Sim, unit.Id) * 2).OrderBy(t => t.Value))
        {
            var target = Sim.State.Grid.HexOf(tile);
            foreach (var kind in Enum.GetValues<AirMissionKind>())
            {
                string label = kind switch
                {
                    AirMissionKind.Rebase => "Rebase",
                    AirMissionKind.Recon => "Recon",
                    AirMissionKind.AirStrike => "Air strike",
                    _ => "Air bomb",
                };
                string name = Sim.State.CityAt(target) is { } city && Sim.State.Plots[tile.Value].Revealed.Contains(Me.TeamId)
                    ? city.Name : target.ToString();
                var action = Action($"air_mission:{kind}:{tile.Value}", $"{label}: {name}",
                    new AirMissionCommand { Player = Human, Unit = unit.Id, Kind = kind, Target = target });
                if (action.Command is not null) yield return action;
            }
        }
    }

    private IEnumerable<UnitActionInfo> CargoActions(Unit unit)
    {
        foreach (var transport in Sim.State.UnitsAt(unit.Position).OrderBy(u => u.Id))
            yield return Action($"load:{transport.Id}", $"Load: {DefOf(transport).Name}",
                new LoadUnitCommand { Player = Human, Unit = unit.Id, Transport = transport.Id });
        yield return Action("unload", "Unload", new UnloadUnitCommand { Player = Human, Unit = unit.Id });
        foreach (var tile in Sim.State.Grid.Neighbors(Sim.State.Grid.TileAt(unit.Position)).OrderBy(t => t.Value))
        {
            var target = Sim.State.Grid.HexOf(tile);
            yield return Action($"unload:{tile.Value}", $"Unload: {target}",
                new UnloadUnitCommand { Player = Human, Unit = unit.Id, Destination = target });
        }
    }

    private IEnumerable<UnitActionInfo> ArtilleryActions(Unit unit, Schema.UnitDef def)
    {
        int range = Math.Max(1, def.BombRange);
        var origin = Sim.State.Grid.TileAt(unit.Position);
        foreach (var tile in Sim.State.Grid.Range(origin, range).OrderBy(t => t.Value))
        {
            var target = Sim.State.Grid.HexOf(tile);
            string name = Sim.State.CityAt(target)?.Name ?? target.ToString();
            var bombard = Action($"bombard:{tile.Value}", $"Bombard: {name}", new BombardCityCommand { Player = Human, Unit = unit.Id, Target = target });
            if (bombard.Command is not null) yield return bombard;
            if (def.BombRange <= 0) continue;
            var ranged = Action($"ranged_strike:{tile.Value}", $"Ranged strike: {name}", new RangedStrikeCommand { Player = Human, Unit = unit.Id, Target = target });
            if (ranged.Command is not null) yield return ranged;
        }
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
                "lead_unit" => new LeadGreatPersonCommand { Player = Human, UnitId = unit.Id, Target = action.Target },
                "construct_building" => new ConstructGreatPersonCommand { Player = Human, UnitId = unit.Id, Target = action.Target },
                "trade_mission" => new TradeGreatPersonCommand { Player = Human, UnitId = unit.Id, Target = action.Target },
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
                "lead_unit" => "Attach leader",
                "construct_building" => "Construct building",
                "trade_mission" => "Trade mission",
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
