using Civ.Content;
using S = Civ.Content.Schema;

namespace Civ.Mods.ScriptedWonders;

/// <summary>Hex adaptations of completion, city-turn and victory callbacks.</summary>
public sealed class WonderScriptDef : ContentDef
{
    [ContentReference(typeof(S.BuildingDef))]
    public string Building { get; init; } = "";
    [ContentReference(typeof(S.UnitDef))]
    public string? Unit { get; init; }
    public int IntervalTurns { get; init; }
    public int CompletionUnits { get; init; }
    public int CompletionCulture { get; init; }
    [ContentReference(typeof(S.BuildingDef))]
    public string? EmpireFreeBuilding { get; init; }
    public int UpgradeDiscount { get; init; }
    public int VictoryExperience { get; init; }

    public int BarbarianBorderPenalty { get; init; }
}
