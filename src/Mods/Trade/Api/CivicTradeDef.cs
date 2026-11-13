using Ariel.Content;
using Ariel.Content.Schema;
using Ariel.Game;

namespace Ariel.Mods.Trade;

/// <summary>What a civic means to trade routes. Any mod may attach one to a civic, its own or another's. Read with
/// <c>ContentDb.Attachment&lt;CivicTradeDef&gt;(civic)</c>. The connectedness fields and
/// <see cref="ForcesOpenTradeRoutesWithWeakerPlayers"/> are kept for the deferred connectedness and forced-trade rules;
/// nothing reads them yet.</summary>
public sealed class CivicTradeDef : ContentAttachment<CivicDef>, IDescribedContent
{
    /// <summary>The player's cities trade with no other team, and no other team trades with them.</summary>
    public bool NoForeignTrade { get; init; }
    /// <summary>Extra trade routes in each city.</summary>
    public int Routes { get; init; }
    /// <summary>Percent profit on routes to another team.</summary>
    public int ForeignProfitPercent { get; init; }
    /// <summary>Percent profit on routes to a city whose owner runs the same civic.</summary>
    public int SharedCivicProfitPercent { get; init; }
    /// <summary>Percent of route profit each yield receives, added to the yields' own trade modifiers.</summary>
    public YieldVector YieldPercent { get; init; }
    public bool ForcesOpenTradeRoutesWithWeakerPlayers { get; init; }
    public int DomesticConnectednessPercent { get; init; }
    public int ForeignConnectednessPercent { get; init; }
    public int SeizedForeignConnectednessPercent { get; init; }

    public IEnumerable<string> Describe(ContentDb content)
    {
        if (NoForeignTrade) yield return "No foreign trade routes";
        if (Routes != 0) yield return $"{Routes:+#;-#} trade routes in each city";
        if (ForeignProfitPercent != 0) yield return $"{ForeignProfitPercent:+#;-#}% foreign trade route profit";
        if (SharedCivicProfitPercent != 0) yield return $"{SharedCivicProfitPercent:+#;-#}% trade route profit with players running this civic";
        if (YieldPercent.Food != 0) yield return $"{YieldPercent.Food:+#;-#}% of trade route profit as food";
        if (YieldPercent.Production != 0) yield return $"{YieldPercent.Production:+#;-#}% of trade route profit as production";
        if (YieldPercent.Commerce != 0) yield return $"{YieldPercent.Commerce:+#;-#}% of trade route profit as commerce";
    }
}
