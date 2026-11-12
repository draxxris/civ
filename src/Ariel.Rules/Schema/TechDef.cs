using System.Collections.Immutable;

namespace Ariel.Content.Schema;

/// <summary>Tech content. All authoritative numbers are integers.</summary>
public sealed class TechDef : GameContentDef
{
    public CommerceVector CommerceFlexible { get; init; } = default;
    [ContentReference(typeof(DomainDef), ReferencePart.Keys)]
    public ImmutableDictionary<string, int> DomainExtraMoves { get; init; } = ImmutableDictionary<string, int>.Empty;
    [ContentReference(typeof(UnitDef), ReferencePart.Values)]
    public string? FirstFreeUnit { get; init; } = null;
    public ImmutableDictionary<string, int> Flavors { get; init; } = ImmutableDictionary<string, int>.Empty;
    public string Quote { get; init; } = "";
    [ContentReference(typeof(TechDef), ReferencePart.Values)]
    public ImmutableArray<string> ArrowTargets { get; init; } = [];
    [ContentReference(typeof(TerrainDef), ReferencePart.Keys)]
    public ImmutableDictionary<string, bool> TradeTerrains { get; init; } = ImmutableDictionary<string, bool>.Empty;
    [ContentReference(typeof(UnitDef), ReferencePart.Keys)]
    public ImmutableDictionary<string, int> UnitClassStrengthChanges { get; init; } = ImmutableDictionary<string, int>.Empty;
    public bool AllowsDesertFarming { get; init; } = false;
    public bool BridgeBuilding { get; init; } = false;
    public bool CanFoundOnPeaks { get; init; } = false;
    public bool CanPassPeaks { get; init; } = false;
    public bool UnlocksAirBombing { get; init; } = false;
    public bool UnlocksPrecisionBombing { get; init; } = false;
    public bool DefensivePactTrading { get; init; } = false;
    public bool Disable { get; init; } = false;
    public bool EmbassyTrading { get; init; } = false;
    public bool ExtendMovementLimits { get; init; } = false;
    public bool ExtraWaterSeeFrom { get; init; } = false;
    public bool GoldTrading { get; init; } = false;
    public bool GoodyTech { get; init; } = false;
    public bool IgnoreIrrigation { get; init; } = false;
    public bool Irrigation { get; init; } = false;
    public bool MapCentering { get; init; } = false;
    public bool MapTrading { get; init; } = false;
    public bool MapVisible { get; init; } = false;
    public bool OpenBordersTrading { get; init; } = false;
    public bool PermanentAllianceTrading { get; init; } = false;
    public bool RebaseAnywhere { get; init; } = false;
    public bool RemoveMovementLimits { get; init; } = false;
    public bool Repeat { get; init; } = false;
    public bool RiverTrade { get; init; } = false;
    public bool TechTrading { get; init; } = false;
    public bool Trade { get; init; } = false;
    public bool VassalTrading { get; init; } = false;
    public bool WaterWork { get; init; } = false;
    public int AiTradeModifier { get; init; } = 0;
    public int AiWeight { get; init; } = 0;
    public int AdvancedStartCost { get; init; } = 0;
    public int AdvancedStartCostIncrease { get; init; } = 0;
    public int Asset { get; init; } = 0;
    public int Cost { get; init; } = 0;
    public int FeatureProductionModifier { get; init; } = 0;
    public int FirstFreeTechs { get; init; } = 0;
    public int GridX { get; init; } = 0;
    public int GridY { get; init; } = 0;
    public int Happiness { get; init; } = 0;
    public int Health { get; init; } = 0;
    public int Power { get; init; } = 0;
    public int TradeRoutes { get; init; } = 0;
    public int TradeMissionModifier { get; init; } = 0;
    public int WorkerSpeedModifier { get; init; } = 0;
}
