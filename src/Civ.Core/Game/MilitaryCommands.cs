namespace Civ.Game;

public sealed class MoveUnitPathCommand : GameCommand
{
    public int Unit { get; init; }
    public Hex[] Path { get; init; } = [];
    public override string? Validate(Simulation sim) => sim.Content.Services.Get<IMovementService>().Single().ValidatePath(sim, Player, Unit, Path);
    public override void Apply(Simulation sim) => sim.Content.Services.Get<IMovementService>().Single().Move(sim, Unit, Path);
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
