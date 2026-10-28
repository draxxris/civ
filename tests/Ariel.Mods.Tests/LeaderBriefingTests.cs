using Ariel.Client;
using Ariel.Content;
using Xunit;
using S = Ariel.Content.Schema;

namespace Ariel.Mods.Tests;

/// <summary>The read-only leader section names every trait, unique and starting technology of a leader the setup screen
/// offers, resolves the content it replaces, and repeats the trait benefits that live on the buildings and units a trait
/// speeds up, so no shipped leader is described with the wrong or an incomplete text.</summary>
public sealed class LeaderBriefingTests(LoadedMods mods) : IClassFixture<LoadedMods>
{
    [Fact]
    public void Every_offered_leader_has_its_traits_uniques_technologies_and_linked_bonuses()
    {
        var catalog = mods.Catalog;
        var content = catalog.Content;
        var buildings = content.Get<S.BuildingDef>().All;
        var units = content.Get<S.UnitDef>().All;
        Assert.NotEmpty(catalog.Leaders);
        foreach (var leader in catalog.Leaders)
        {
            var sections = LeaderBriefing.Build(catalog, leader);
            Assert.DoesNotContain(sections, section => section.Groups.Count == 0);
            foreach (var line in sections.SelectMany(section => section.Groups).SelectMany(group => group.Lines))
            {
                Assert.False(string.IsNullOrWhiteSpace(line));
                // Combat types already read as collectives, so the text must not say "Melee Units units".
                Assert.DoesNotContain(" Units units", line, StringComparison.OrdinalIgnoreCase);
            }

            var traitIds = content.Get<S.LeaderDef>(leader.Id).Traits.Where(trait => trait.Value).Select(trait => trait.Key).ToArray();
            var traits = Assert.Single(sections, section => section.Heading == "Traits").Groups;
            Assert.Equal(leader.Traits.Order(StringComparer.Ordinal), traits.Select(group => group.Title!).Order(StringComparer.Ordinal));
            var traitText = traits.SelectMany(group => group.Lines).ToList();
            if (buildings.Any(building => Linked(building.TraitProductionModifiers, traitIds)) || units.Any(unit => Linked(unit.TraitProductionModifiers, traitIds)))
                Assert.Contains(traitText, line => line.Contains("% production of", StringComparison.Ordinal));
            if (buildings.Any(building => Linked(building.TraitHappinessChanges, traitIds)))
                Assert.Contains(traitText, line => line.Contains(" happiness from ", StringComparison.Ordinal));

            var civ = content.Get<S.CivilizationDef>(leader.CivilizationId);
            var uniqueUnits = Assert.Single(sections, section => section.Heading == "Unique unit").Groups.Single().Lines;
            AssertReplaces<S.UnitDef>(content, civ.UnitOverrides, uniqueUnits);
            var uniqueBuildings = Assert.Single(sections, section => section.Heading == "Unique building").Groups.Single().Lines;
            AssertReplaces<S.BuildingDef>(content, civ.BuildingOverrides, uniqueBuildings);

            var techs = Assert.Single(Assert.Single(sections, section => section.Heading == "Starting technologies").Groups.Single().Lines);
            var expected = civ.StartingTechs.Where(tech => tech.Value).Select(tech => content.Get<S.TechDef>(tech.Key).Name)
                .Order(StringComparer.Ordinal);
            Assert.Equal(string.Join(", ", expected), techs);
        }
    }

    private static bool Linked(IReadOnlyDictionary<string, int> amounts, IReadOnlyCollection<string> traits) =>
        amounts.Any(pair => pair.Value != 0 && traits.Contains(pair.Key));

    /// <summary>A unique line reads "Unique (replaces Original)", both resolved to display names, one per replacement.</summary>
    private static void AssertReplaces<T>(ContentDb content, IReadOnlyDictionary<string, string?> overrides, IReadOnlyList<string> lines)
        where T : ContentDef
    {
        var replaced = overrides.Where(pair => pair.Value is { } unique && unique != pair.Key).ToList();
        Assert.Equal(replaced.Count, lines.Count);
        foreach (var pair in replaced)
            Assert.Contains($"{content.Get<T>(pair.Value!).Name} (replaces {content.Get<T>(pair.Key).Name})", lines);
    }
}
