using Ariel.Content;
using Ariel.Game;

namespace Ariel.Mods.Corporations;

/// <summary>Which of the two ways guilds and corporations work in a game, chosen by the Realistic Guilds/Corporations
/// game option (on by default).
/// <list type="bullet">
/// <item>Realistic: they are founded and spread by themselves (<c>corporations:natural</c>): founding buildings,
/// executives and paid spreads are refused.</item>
/// <item>Manual (option off): a nation founds one by building its headquarters or Master Guild and spreads it with
/// executives and gold.</item>
/// </list></summary>
public static class CorporationMode
{
    /// <summary>The game option that chooses the realistic mode.</summary>
    public const string Option = "foundation:gameoption_realistic_corporations";

    /// <summary>Whether guilds and corporations found and spread by themselves in this game.</summary>
    public static bool Realistic(Simulation sim) => sim.IsOptionEnabled(Option);

    /// <summary>The option's name as the player reads it, from content.</summary>
    public static string OptionName(ContentDb content) =>
        content.Find(Option)?.Name is { Length: > 0 } name ? name : "Realistic Guilds/Corporations";
}
