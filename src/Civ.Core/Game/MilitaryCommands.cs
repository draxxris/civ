namespace Civ.Game;

public sealed class MoveUnitPathCommand : GameCommand
{
    public int Unit { get; init; }
    public Hex[] Path { get; init; } = [];
    public override string? Validate(Simulation sim) => sim.Content.Services.Get<IMovementService>().Single().ValidatePath(sim, Player, Unit, Path);
    public override void Apply(Simulation sim) => sim.Content.Services.Get<IMovementService>().Single().Move(sim, Unit, Path);
}
public sealed class LoadUnitCommand : GameCommand
{
    public int Unit { get; init; }
    public int Transport { get; init; }
    public override string? Validate(Simulation sim) => sim.Content.Services.Get<IUnitService>().Single().ValidateLoad(sim, Player, Unit, Transport);
    public override void Apply(Simulation sim) => sim.Content.Services.Get<IUnitService>().Single().Load(sim, Unit, Transport);
}
public sealed class UnloadUnitCommand : GameCommand
{
    public int Unit { get; init; }
    public Hex? Destination { get; init; }
    public override string? Validate(Simulation sim) => sim.Content.Services.Get<IUnitService>().Single().ValidateUnload(sim, Player, Unit, Destination);
    public override void Apply(Simulation sim) => sim.Content.Services.Get<IUnitService>().Single().Unload(sim, Unit, Destination);
}
public sealed class AirMissionCommand : GameCommand
{
    public int Unit { get; init; }
    public AirMissionKind Kind { get; init; }
    public Hex Target { get; init; }
    public override string? Validate(Simulation sim) => sim.Content.Services.Get<IAirMissionService>().Single().Validate(sim, Player, Unit, Kind, Target);
    public override void Apply(Simulation sim) => sim.Content.Services.Get<IAirMissionService>().Single().Execute(sim, Unit, Kind, Target);
}
public sealed class BombardCityCommand : GameCommand
{
    public int Unit { get; init; }
    public Hex Target { get; init; }
    public override string? Validate(Simulation sim) => sim.Content.Services.Get<ICombatService>().Single().ValidateBombard(sim, Player, Unit, Target);
    public override void Apply(Simulation sim) => sim.Content.Services.Get<ICombatService>().Single().BombardCity(sim, Unit, Target);
}
public sealed class RangedStrikeCommand : GameCommand
{
    public int Unit { get; init; }
    public Hex Target { get; init; }
    public override string? Validate(Simulation sim) => sim.Content.Services.Get<ICombatService>().Single().ValidateRangedStrike(sim, Player, Unit, Target);
    public override void Apply(Simulation sim) => sim.Content.Services.Get<ICombatService>().Single().RangedStrike(sim, Unit, Target);
}
/// <summary>Shared command transport for independent unit actions.</summary>
public abstract class MilitaryUnitCommand : GameCommand
{
    public int Unit { get; init; }
    protected abstract UnitAction Action { get; }
    protected abstract string Argument { get; }
    public override string? Validate(Simulation sim) => sim.Content.Services.Get<IUnitService>().Single().Validate(sim, Player, Unit, Action, Argument);
    public override void Apply(Simulation sim) => sim.Content.Services.Get<IUnitService>().Single().Execute(sim, Unit, Action, Argument);
}
public sealed class PromoteMilitaryUnitCommand : MilitaryUnitCommand
{
    public string Promotion { get; init; } = "";
    protected override UnitAction Action => UnitAction.Promote;
    protected override string Argument => Promotion;
}
public sealed class SetUnitActivityCommand : MilitaryUnitCommand
{
    public UnitActivity Activity { get; init; }
    protected override UnitAction Action => UnitAction.Activity;
    protected override string Argument => Activity.ToString();
}
public sealed class PillageCommand : MilitaryUnitCommand
{
    protected override UnitAction Action => UnitAction.Pillage;
    protected override string Argument => "";
}
public sealed class UpgradeUnitCommand : MilitaryUnitCommand
{
    public string Target { get; init; } = "";
    protected override UnitAction Action => UnitAction.Upgrade;
    protected override string Argument => Target;
}
public sealed class DisbandUnitCommand : MilitaryUnitCommand
{
    protected override UnitAction Action => UnitAction.Disband;
    protected override string Argument => "";
}
public sealed class RazeCityCommand : GameCommand
{
    public int City { get; init; }
    public override string? Validate(Simulation sim) => sim.Content.Services.Get<ICityCaptureService>().Single().ValidateRaze(sim, City, Player);
    public override void Apply(Simulation sim) => sim.Content.Services.Get<ICityCaptureService>().Single().Raze(sim, City);
}
