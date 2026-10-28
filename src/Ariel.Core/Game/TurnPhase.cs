using Ariel.Content;

namespace Ariel.Game;

/// <summary>Systems in the player interval run all phases for one player before the next player.</summary>
public interface IPlayerTurnSystem : ITurnSystem
{
    /// <summary>Run this phase for a single player; cities and units must use ascending entity ids.</summary>
    void Run(Simulation sim, int player);
    void ITurnSystem.Run(Simulation sim)
    {
        foreach (var player in sim.State.Players.OrderBy(p => p.Id)) Run(sim, player.Id);
    }
}
/// <summary>Shared phase slots. Within a slot ordinal system ids decide execution order. See docs/DESIGN.md.</summary>
public static class TurnPhase
{
    /// <summary>Begin the completed-turn boundary.</summary>
    public const int GameBegin = 0;
    /// <summary>Global property calculations.</summary>
    public const int Properties = 100;
    /// <summary>Deal validation and settlement.</summary>
    public const int Deals = 200;
    /// <summary>Shared team maintenance.</summary>
    public const int Teams = 300;
    /// <summary>Player timers and caches; start of the nested player interval.</summary>
    public const int PlayerBegin = 1000;
    /// <summary>Work assignments before settling commerce.</summary>
    public const int AssignWork = 1100;
    /// <summary>Treasury settlement.</summary>
    public const int Gold = 2000;
    /// <summary>Research settlement and discovery.</summary>
    public const int Research = 2100;
    /// <summary>Espionage allocation.</summary>
    public const int Espionage = 2200;
    /// <summary>Taxes, resource consumption and inflation.</summary>
    public const int Economy = 2300;
    /// <summary>Prepare all owned cities and refresh eligibility.</summary>
    public const int CityBegin = 3000;
    /// <summary>City food, growth and starvation.</summary>
    public const int CityGrowth = 4000;
    /// <summary>City culture accumulation.</summary>
    public const int CityCulture = 4100;
    /// <summary>Plot culture and border changes.</summary>
    public const int CityPlotCulture = 4200;
    /// <summary>Automatic buildings after growth and border changes.</summary>
    public const int CityAutoBuildings = 4300;
    /// <summary>Production eligibility, selection and work.</summary>
    public const int CityProduction = 5000;
    /// <summary>Commit completion effects in stable entity order.</summary>
    public const int CityCompletion = 5100;
    /// <summary>Decay, religion, great people and city post-processing.</summary>
    public const int CityPostProduction = 6000;
    /// <summary>Remaining player maintenance and notifications.</summary>
    public const int PlayerEnd = 7000;
    /// <summary>Unit maintenance and worker progress.</summary>
    public const int Units = 8000;
    /// <summary>Global map updates; end of the nested player interval.</summary>
    public const int Map = 9000;
    /// <summary>Barbarians and generalized spawns.</summary>
    public const int Spawns = 9100;
    /// <summary>Environment, holy cities and headquarters.</summary>
    public const int Environment = 9200;
    /// <summary>Recompute team sight after mutations.</summary>
    public const int Visibility = 9500;
    /// <summary>Pre-end hooks before TurnEnded and counter increment.</summary>
    public const int GameEnd = 9900;
}
