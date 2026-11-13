using System.Globalization;
using Ariel.Content;
using Ariel.Game;
using S = Ariel.Content.Schema;

namespace Ariel.Client;

/// <summary>What a definition does, as lines a player can read, gathered without knowing the effects in advance: the
/// data-driven modifiers it carries, the typed rules of its own kind, and what every loaded mod attached to it says
/// about itself. A mod's new modifier key reads through its <see cref="ModifierKeyDef"/> label and a mod's attachment
/// through <see cref="IDescribedContent"/>, so neither needs a change here.</summary>
public static class ContentDescription
{
    public static IReadOnlyList<string> Of(ContentDb content, ContentDef definition)
    {
        var lines = new List<string>();
        foreach (var (effect, _) in ContentReferences.Find<ModifierEffect>(definition, definition.Id))
            if (Effect(content, effect) is { } line) lines.Add(line);
        if (definition is S.CivicDef civic) lines.AddRange(Civic(content, civic));
        if (definition is IDescribedContent described) lines.AddRange(described.Describe(content));
        foreach (var attachment in content.AttachmentsOf(definition.Id).OfType<IDescribedContent>())
            lines.AddRange(attachment.Describe(content));
        return lines;
    }

    /// <summary>One modifier as text, or null for a zero amount, which changes nothing.</summary>
    public static string? Effect(ContentDb content, ModifierEffect effect)
    {
        if (effect.Amount == 0) return null;
        var (unit, label) = Typed(effect.Key) ?? Declared(content, effect.Key);
        if (effect.Target is { } target)
            label = label.Replace("{target}", content.Find(target)?.Name is { Length: > 0 } name ? name : Labels.Humanize(target), StringComparison.Ordinal);
        return unit switch
        {
            ModifierUnit.Flag => label,
            ModifierUnit.Percent => $"{Labels.Signed(effect.Amount)}% {label}",
            ModifierUnit.Hundredths => $"{(effect.Amount > 0 ? "+" : "")}{(effect.Amount / 100m).ToString("0.##", CultureInfo.InvariantCulture)} {label}",
            _ => $"{Labels.Signed(effect.Amount)} {label}",
        };
    }

    private static (ModifierUnit, string)? Typed(string key)
    {
        if (!ModifierSink.IsTypedKey(key)) return null;
        var parts = key.Split('.');
        return (parts[1] == "percent" ? ModifierUnit.Percent : ModifierUnit.Flat, parts[2]);
    }

    private static (ModifierUnit, string) Declared(ContentDb content, string key) =>
        content.TryGet<ModifierKeyDef>(key, out var declared) && declared.Name.Length > 0
            ? (declared.Unit, declared.Name) : (ModifierUnit.Flat, Labels.Humanize(key));

    /// <summary>The typed civic rules that read more than a calculation's subject, so they are fields rather than
    /// <see cref="S.CivicDef.Effects"/>. Rules the base game does not apply yet are left out.</summary>
    private static IEnumerable<string> Civic(ContentDb content, S.CivicDef civic)
    {
        foreach (var line in Vector("in the capital", civic.CapitalYieldModifiers.Food, civic.CapitalYieldModifiers.Production,
                     civic.CapitalYieldModifiers.Commerce, civic.CapitalCommerceModifiers.Gold, civic.CapitalCommerceModifiers.Research,
                     civic.CapitalCommerceModifiers.Culture, civic.CapitalCommerceModifiers.Espionage, percent: true))
            yield return line;
        foreach (var line in Vector("from each specialist", 0, 0, 0, civic.SpecialistExtraCommerces.Gold,
                     civic.SpecialistExtraCommerces.Research, civic.SpecialistExtraCommerces.Culture,
                     civic.SpecialistExtraCommerces.Espionage, percent: false))
            yield return line;
        foreach (var (building, amount) in civic.BuildingHappinessChanges.OrderBy(p => p.Key, StringComparer.Ordinal))
            if (amount != 0) yield return $"{Labels.Signed(amount)} happiness from {Name(content, building)}";
        foreach (var (building, amount) in civic.BuildingHealthChanges.OrderBy(p => p.Key, StringComparer.Ordinal))
            if (amount != 0) yield return $"{Labels.Signed(amount)} health from {Name(content, building)}";
        if (civic.LargestCityHappiness != 0) yield return $"{Labels.Signed(civic.LargestCityHappiness)} happiness in the largest cities";
        if (civic.MaxConscript != 0) yield return "Can draft units";
        if (civic.NoCapitalUnhappiness) yield return "No unhappiness in the capital";
        if (civic.FixedBorders) yield return "Fixed borders";
        if (civic.UpgradeAnywhere) yield return "Units can upgrade outside your territory";
        if (civic.Upkeep is { } upkeep) yield return "Upkeep: " + Name(content, upkeep);
    }

    private static IEnumerable<string> Vector(string where, long food, long production, long commerce, long gold, long research,
        long culture, long espionage, bool percent)
    {
        var parts = new (long Amount, string Name)[]
        {
            (food, "food"), (production, "production"), (commerce, "commerce"), (gold, "gold"), (research, "research"),
            (culture, "culture"), (espionage, "espionage"),
        };
        foreach (var (amount, name) in parts)
            if (amount != 0) yield return $"{Labels.Signed(amount)}{(percent ? "%" : "")} {name} {where}";
    }

    private static string Name(ContentDb content, string id) =>
        content.Find(id)?.Name is { Length: > 0 } name ? name : Labels.Humanize(id);
}
