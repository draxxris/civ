using Ariel.Content;
using Ariel.Content.Schema;
using Ariel.Game;

namespace Ariel.Mods.Corporations;

/// <summary>What a civic means to corporations. Any mod may attach one to a civic, its own or another's; a civic
/// without one leaves corporations alone. Read with <c>ContentDb.Attachment&lt;CivicCorporationsDef&gt;(civic)</c>.</summary>
public sealed class CivicCorporationsDef : ContentAttachment<CivicDef>, IDescribedContent
{
    /// <summary>No corporation operates in the cities of a player running the civic.</summary>
    public bool NoCorporations { get; init; }
    /// <summary>Only corporations headquartered in the player's own team operate in its cities.</summary>
    public bool NoForeignCorporations { get; init; }
    /// <summary>Percent change to corporation maintenance, the player's <c>corporations:maintenance_percent</c>.</summary>
    public int MaintenancePercent { get; init; }
    /// <summary>The same change when the realistic corporations game option is on; it replaces
    /// <see cref="MaintenancePercent"/>.</summary>
    public int RealisticMaintenancePercent { get; init; }

    public IEnumerable<string> Describe(ContentDb content)
    {
        if (NoCorporations) yield return "No corporations";
        if (NoForeignCorporations) yield return "No foreign corporations";
        if (MaintenancePercent != 0) yield return $"{MaintenancePercent:+#;-#}% corporation maintenance";
        if (RealisticMaintenancePercent != 0) yield return $"{RealisticMaintenancePercent:+#;-#}% corporation maintenance with realistic corporations";
    }
}
