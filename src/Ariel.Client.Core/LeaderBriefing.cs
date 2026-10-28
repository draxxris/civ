using System.Collections.Immutable;
using Ariel.Content;
using Ariel.Game;
using S = Ariel.Content.Schema;

namespace Ariel.Client;

/// <summary>A titled block of the read-only leader briefing, holding line groups already resolved to display text.</summary>
public sealed record BriefingSection(string Heading, IReadOnlyList<BriefingGroup> Groups);

/// <summary>Lines under one optional title: a trait and its effects, or a plain list such as the uniques.</summary>
public sealed record BriefingGroup(string? Title, IReadOnlyList<string> Lines);

/// <summary>What a leader brings: each trait's effects in plain words, the civilization's unique units and buildings
/// with what they replace, and its starting technologies. Engine free, so the wording can be tested without a renderer.
/// Only effects the rules apply are described; a trait field nothing consumes would otherwise read as a promise.</summary>
public static class LeaderBriefing
{
    /// <summary>The briefing sections for one leader choice, in reading order. The choice carries its civilization:
    /// a leader who can lead two civilizations shows the uniques of the one being picked.</summary>
    public static IReadOnlyList<BriefingSection> Build(GameCatalog catalog, LeaderChoice leader)
    {
        var content = catalog.Content;
        var sections = new List<BriefingSection>();
        var traits = Traits(content, leader.Id);
        if (traits.Count > 0) sections.Add(new("Traits", traits));
        if (!content.TryGet<S.CivilizationDef>(leader.CivilizationId, out var civ)) return sections;
        var units = Overrides<S.UnitDef>(content, civ.UnitOverrides, Name<S.UnitDef>);
        if (units.Count > 0) sections.Add(new(units.Count == 1 ? "Unique unit" : "Unique units", [new(null, units)]));
        var buildings = Overrides<S.BuildingDef>(content, civ.BuildingOverrides, Name<S.BuildingDef>);
        if (buildings.Count > 0) sections.Add(new(buildings.Count == 1 ? "Unique building" : "Unique buildings", [new(null, buildings)]));
        var techs = civ.StartingTechs.Where(tech => tech.Value).Select(tech => Name<S.TechDef>(content, tech.Key))
            .Order(StringComparer.Ordinal).ToList();
        if (techs.Count > 0) sections.Add(new("Starting technologies", [new(null, [string.Join(", ", techs)])]));
        return sections;
    }

    /// <summary>One group per trait: the trait's name over its effects, or the name alone when it has none here.</summary>
    private static List<BriefingGroup> Traits(ContentDb content, string leaderId)
    {
        if (!content.TryGet<S.LeaderDef>(leaderId, out var leader)) return [];
        return leader.Traits.Where(trait => trait.Value).Select(trait =>
            content.TryGet<S.TraitDef>(trait.Key, out var def) ? def : null).Where(def => def is not null)
            .Select(def => def!).OrderBy(def => def.Name, StringComparer.OrdinalIgnoreCase)
            .Select(trait => new BriefingGroup(trait.Name, Effects(content, trait))).ToList();
    }

    /// <summary>A replacement pair as "unique (replaces original)", skipping entries that replace nothing.</summary>
    private static List<string> Overrides<T>(ContentDb content, IReadOnlyDictionary<string, string?> overrides,
        Func<ContentDb, string, string> name) where T : ContentDef =>
        overrides.Where(pair => pair.Value is { } unique && unique != pair.Key).OrderBy(pair => pair.Key, StringComparer.Ordinal)
            .Select(pair => $"{name(content, pair.Value!)} (replaces {name(content, pair.Key)})").ToList();

    /// <summary>The trait's effects in the words a player reads, mirroring the modifier contributions the rules make.</summary>
    private static List<string> Effects(ContentDb content, S.TraitDef trait)
    {
        var lines = new List<string>();
        if (trait.UpkeepModifier != 0) lines.Add($"{Labels.Signed(trait.UpkeepModifier)}% civic upkeep");
        if (trait.GreatGeneralRateModifier != 0) lines.Add($"{Labels.Signed(trait.GreatGeneralRateModifier)}% great general emergence");
        if (trait.DomesticGreatGeneralRateModifier != 0)
            lines.Add($"{Labels.Signed(trait.DomesticGreatGeneralRateModifier)}% great general emergence in own territory");
        if (trait.MaximumAnarchyTurns == 0) lines.Add("no anarchy from civic or religious change");
        else if (trait.MaximumAnarchyTurns > 0)
            lines.Add(trait.MaximumAnarchyTurns == 1 ? "anarchy lasts at most one turn" : $"anarchy lasts at most {trait.MaximumAnarchyTurns} turns");
        if (trait.Happiness != 0) lines.Add($"{Labels.Signed(trait.Happiness)} happiness in every city");
        if (trait.Health != 0) lines.Add($"{Labels.Signed(trait.Health)} health in every city");
        foreach (var (name, value) in new[] { ("food", trait.YieldChanges.Food), ("production", trait.YieldChanges.Production), ("commerce", trait.YieldChanges.Commerce) })
            if (value != 0) lines.Add($"{Labels.Signed(value)} {name} in every city");
        foreach (var (name, value) in Commerce(trait.CommerceChanges))
            if (value != 0) lines.Add($"{Labels.Signed(value)} {name} in every city");
        foreach (var (name, value) in Commerce(trait.CommerceModifiers))
            if (value != 0) lines.Add($"{Labels.Signed(value)}% {name} in every city");
        if (trait.GreatPeopleRateModifier != 0) lines.Add($"{Labels.Signed(trait.GreatPeopleRateModifier)}% great people birth rate");
        if (trait.WorldWonderProductionPercent != 0) lines.Add($"{Labels.Signed(trait.WorldWonderProductionPercent)}% world wonder production");
        if (trait.NationalWonderProductionPercent != 0) lines.Add($"{Labels.Signed(trait.NationalWonderProductionPercent)}% national wonder production");
        if (trait.TeamWonderProductionPercent != 0) lines.Add($"{Labels.Signed(trait.TeamWonderProductionPercent)}% team wonder production");
        if (trait.ExtraCultureLevel) lines.Add("one extra culture level in every city");
        if (trait.ExperienceThresholdPercent != 0)
            lines.Add($"{Labels.Signed(trait.ExperienceThresholdPercent)}% experience needed for promotions");
        if (trait.NonStateReligionCommerce) lines.Add("non-state religion buildings keep their commerce");
        foreach (var change in trait.BonusHappinessChanges.OrderBy(pair => pair.Key, StringComparer.Ordinal))
            if (change.Value != 0)
                lines.Add($"{Labels.Signed(change.Value)} happiness from {Name<S.BonusDef>(content, change.Key)}");
        foreach (var grant in trait.CombatTypePromotions.OrderBy(pair => pair.Key, StringComparer.Ordinal))
        {
            var combat = grant.Value.Select(id => Name<S.UnitCombatDef>(content, id)).ToList();
            if (combat.Count > 0)
                lines.Add($"free {Name<S.PromotionDef>(content, grant.Key)} promotion for {Words(combat)}");
        }
        foreach (var (name, value) in new[] { ("food", trait.ExtraYieldThresholds.Food), ("production", trait.ExtraYieldThresholds.Production), ("commerce", trait.ExtraYieldThresholds.Commerce) })
            if (value > 0) lines.Add($"plots worth {value} or more {name} produce an extra {name}");
        foreach (var (name, value) in new[] { ("food", trait.TradeYieldModifiers.Food), ("production", trait.TradeYieldModifiers.Production), ("commerce", trait.TradeYieldModifiers.Commerce) })
            if (value != 0) lines.Add($"{Labels.Signed(value)}% {name} from trade routes");
        // Some trait benefits live on what they speed up or make happy, not on the trait: a building or unit names
        // the trait with a production percent, and a building names it with extra happiness.
        foreach (var (percent, what) in Linked(content, trait.Id, def => def.TraitProductionModifiers))
            lines.Add($"{Labels.Signed(percent)}% production of {Names(what)}");
        foreach (var (happy, what) in Linked(content, trait.Id, def => def.TraitHappinessChanges))
            lines.Add($"{Labels.Signed(happy)} happiness from {Names(what)}");
        return lines;
    }

    /// <summary>Content grouped by the amount it credits to one trait, largest first, for buildings and units alike.</summary>
    private static IEnumerable<(int Amount, List<string> Names)> Linked(ContentDb content, string trait,
        Func<S.BuildingDef, ImmutableDictionary<string, int>> ofBuilding)
    {
        var byAmount = new Dictionary<int, List<string>>();
        Gather(byAmount, content.Get<S.BuildingDef>().All.Select(def => (def.Name, ofBuilding(def))), trait);
        Gather(byAmount, content.Get<S.UnitDef>().All.Select(def => (def.Name, def.TraitProductionModifiers)), trait);
        foreach (var pair in byAmount.OrderByDescending(entry => entry.Key)) yield return (pair.Key, pair.Value);
    }

    private static void Gather(Dictionary<int, List<string>> into, IEnumerable<(string Name, ImmutableDictionary<string, int> Amounts)> defs, string trait)
    {
        foreach (var (name, amounts) in defs)
        {
            if (!amounts.TryGetValue(trait, out int amount) || amount == 0) continue;
            if (!into.TryGetValue(amount, out var names)) into[amount] = names = [];
            if (!names.Contains(name)) names.Add(name);
        }
    }

    /// <summary>Every name of a short list, or four and a count, so one effect stays on a readable line.</summary>
    private static string Names(List<string> names)
    {
        var ordered = names.Order(StringComparer.Ordinal).ToList();
        return ordered.Count <= 5 ? Words(ordered) : $"{string.Join(", ", ordered.Take(4))}, and {ordered.Count - 4} more";
    }

    /// <summary>"a", "a and b", "a, b and c".</summary>
    private static string Words(IReadOnlyList<string> items) => items.Count switch
    {
        0 => "",
        1 => items[0],
        2 => $"{items[0]} and {items[1]}",
        _ => string.Join(", ", items.Take(items.Count - 1)) + " and " + items[^1],
    };

    private static IEnumerable<(string Name, long Value)> Commerce(CommerceVector vector) =>
        [("gold", vector.Gold), ("research", vector.Research), ("culture", vector.Culture), ("espionage", vector.Espionage)];

    private static string Name<T>(ContentDb content, string id) where T : ContentDef =>
        content.TryGet<T>(id, out var def) ? def.Name : Labels.Humanize(id);
}
