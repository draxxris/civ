using Ariel.Game;
using Ariel.Modding;

namespace Ariel.Mods.AirCombat;

/// <summary>Air missions: rebasing, reconnaissance, air strikes and bombing, and the interception that can stop them.
/// The base game keeps what aircraft are and where they may stand (bases, carriers, landing when a carrier sinks), the
/// intercept activity and the reconnaissance sight it draws; this mod owns the missions, how likely a unit intercepts,
/// the unit panel's mission orders, and how the computer player builds and flies aircraft.</summary>
public sealed class AirCombatMod : IMod
{
    public void Register(IModContext context)
    {
        var service = new AirMissionService();
        context.RegisterService<IAirMissionService>("missions", service);
        context.RegisterService<IAirOperations>("operations", service);
        context.RegisterCommand<AirMissionCommand>("air_mission");
        context.RegisterService<IUnitActionSource>("air_missions", new AirActions(service));
        context.RegisterService<Ariel.Ai.IAiPlanner>("planner", new AirAiPlanner());
        context.RegisterService<Ariel.Ai.IProductionWishes>("aircraft", new AircraftWishes());
    }
}

/// <summary>Every mission an aircraft could fly, to every tile within its rebasing reach, as the unit panel lists them.</summary>
internal sealed class AirActions(IAirMissionService air) : IUnitActionSource
{
    public IEnumerable<UnitActionOffer> Offers(Simulation sim, int player, Unit unit)
    {
        if (air.AirRange(sim, unit.Id) <= 0) yield break;
        var grid = sim.State.Grid;
        int team = sim.TeamOf(player).Id;
        foreach (var tile in grid.Range(grid.TileAt(unit.Position), air.AirRange(sim, unit.Id) * 2).OrderBy(t => t.Value))
        {
            var target = grid.HexOf(tile);
            string name = sim.State.CityAt(target) is { } city && sim.State.Plots[tile.Value].Revealed.Contains(team)
                ? city.Name : target.ToString();
            foreach (var kind in Enum.GetValues<AirMissionKind>())
            {
                string label = kind switch
                {
                    AirMissionKind.Rebase => "Rebase",
                    AirMissionKind.Recon => "Recon",
                    AirMissionKind.AirStrike => "Air strike",
                    _ => "Air bomb",
                };
                yield return new($"air_mission:{kind}:{tile.Value}", $"{label}: {name}",
                    new AirMissionCommand { Player = player, Unit = unit.Id, Kind = kind, Target = target });
            }
        }
    }
}
