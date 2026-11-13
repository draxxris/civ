using Ariel.Content;
using Ariel.Game;

namespace Ariel.Presentation;

/// <summary>Turns ids found in ledgers into text a player can read.</summary>
public static class Labels
{
    public static string Humanize(string id)
    {
        int colon = id.LastIndexOf(':');
        var words = (colon >= 0 ? id[(colon + 1)..] : id).Replace('_', ' ').Replace('.', ' ');
        return words.Length == 0 ? id : char.ToUpperInvariant(words[0]) + words[1..];
    }

    /// <summary>The name of a ledger source: a building, civic, trait, tile terrain, or a humanized id.</summary>
    public static string Source(Simulation sim, string source)
    {
        foreach (var provider in sim.Content.Services.Get<ILedgerSourceLabel>())
            if (provider.Label(sim, source) is { } label) return label;
        foreach (var name in new[] { Lookup<Content.Schema.BuildingDef>(sim, source), Lookup<Content.Schema.CivicDef>(sim, source),
                     Lookup<Content.Schema.TraitDef>(sim, source), Lookup<Content.Schema.SpecialistDef>(sim, source),
                     Lookup<Content.Schema.BonusDef>(sim, source), Lookup<Content.Schema.FeatureDef>(sim, source),
                     Lookup<Content.Schema.TechDef>(sim, source) })
            if (name != null) return name;
        return Humanize(source);
    }

    private static string? Lookup<T>(Simulation sim, string id) where T : ContentDef =>
        sim.Content.TryGet<T>(id, out var def) ? def.Name : null;

    public static string Signed(long value) => value > 0 ? $"+{value}" : value.ToString();

    /// <summary>Content text without its markup: colour and link tags in brackets and placeholders in braces.</summary>
    public static string Plain(string text) => System.Text.RegularExpressions.Regex.Replace(text, @"\{[^}]*\}|\[[^\]]*\]", "");

    /// <summary>The name of a definition of type <typeparamref name="T"/>, or the humanized id when there is none.</summary>
    public static string Name<T>(ContentDb content, string id) where T : ContentDef =>
        content.TryGet<T>(id, out var def) ? def.Name : Humanize(id);

    /// <summary>A player as the local player reads it: "You" for <paramref name="local"/>, else the player's name.</summary>
    public static string Player(Simulation sim, int player, int local) => player == local ? "You"
        : sim.State.Players.FirstOrDefault(p => p.Id == player)?.Name ?? $"Player {player}";

    /// <summary>A team as its members' names, lowest player id first.</summary>
    public static string Team(Simulation sim, Team team, int local) =>
        string.Join(", ", team.Members.OrderBy(m => m).Select(m => Player(sim, m, local)));

    public static string YieldText(long food, long production, long commerce, long culture = 0)
    {
        var parts = new List<string>();
        if (food != 0) parts.Add($"{Signed(food)} food");
        if (production != 0) parts.Add($"{Signed(production)} production");
        if (commerce != 0) parts.Add($"{Signed(commerce)} commerce");
        if (culture != 0) parts.Add($"{Signed(culture)} culture");
        return string.Join(", ", parts);
    }
}
