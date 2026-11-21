using Ariel.Content;
using Ariel.Content.Schema;

namespace Ariel.Mods.Trade;

// What base definitions mean to trade routes, kept in this mod's own rows (see "Content attachments" in
// docs/DESIGN.md). A definition without a row means nothing to trade: read it as None, whose values are all zero.

/// <summary>What a building means to trade routes: extra route slots and route profit. Read with
/// <c>ContentDb.Attachment&lt;BuildingTradeDef&gt;(building)</c>.</summary>
public sealed class BuildingTradeDef : ContentAttachment<BuildingDef>
{
    /// <summary>The row of a building nothing attached to.</summary>
    public static readonly BuildingTradeDef None = new();
    /// <summary>Extra routes in the city that runs the building.</summary>
    public int TradeRoutes { get; init; }
    /// <summary>Extra routes in every city of the owner.</summary>
    public int GlobalTradeRoutes { get; init; }
    /// <summary>Extra routes in every coastal city of the owner.</summary>
    public int CoastalTradeRoutes { get; init; }
    /// <summary>Percent profit on the city's routes.</summary>
    public int TradeRouteModifier { get; init; }
    /// <summary>Percent profit on the city's routes to another team.</summary>
    public int ForeignTradeRouteModifier { get; init; }
}

/// <summary>What a technology means to trade routes: extra routes in every city of a team that knows it.</summary>
public sealed class TechTradeDef : ContentAttachment<TechDef>
{
    /// <summary>The row of a technology nothing attached to.</summary>
    public static readonly TechTradeDef None = new();
    public int TradeRoutes { get; init; }
}

/// <summary>What a yield means to trade routes: the percent of route profit it receives.</summary>
public sealed class YieldTradeDef : ContentAttachment<YieldDef>
{
    /// <summary>The row of a yield nothing attached to.</summary>
    public static readonly YieldTradeDef None = new();
    public int TradeModifier { get; init; }
}

/// <summary>What a world size means to trade routes: the percent of route distance that counts towards profit.</summary>
public sealed class WorldSizeTradeDef : ContentAttachment<WorldSizeDef>
{
    /// <summary>The row of a world size nothing attached to.</summary>
    public static readonly WorldSizeTradeDef None = new();
    public int TradeProfitPercent { get; init; }
}

/// <summary>The trade rule constants, attached to the base game's <see cref="RuleConstantsDef"/>.</summary>
public sealed class RuleConstantsTradeDef : ContentAttachment<RuleConstantsDef>
{
    /// <summary>The row when nothing attached to the rule constants.</summary>
    public static readonly RuleConstantsTradeDef None = new();
    /// <summary>Percent of the lesser of partner population and distance that a route earns.</summary>
    public int TradeProfitPercent { get; init; }
}
