using Ariel.Client;
using Xunit;

namespace Ariel.Mods.Tests;

/// <summary>A leader may lead more than one civilization, and the setup screen offers each pairing. What the player
/// picks must be what the world starts as, not the catalog's first entry for that leader.</summary>
public sealed class SetupChoiceTests(LoadedMods mods) : IClassFixture<LoadedMods>
{
    [Fact]
    public void A_leader_who_leads_two_civilizations_starts_with_the_civilization_that_was_picked()
    {
        var catalog = mods.Catalog;
        var shared = catalog.Leaders.GroupBy(choice => choice.Id).Where(group => group.Count() > 1).ToList();
        Assert.NotEmpty(shared);
        foreach (var choice in shared.SelectMany(group => group))
        {
            var options = catalog.DefaultOptions() with
            {
                Leader = choice.Id, Civilization = choice.CivilizationId, Opponents = 0,
            };
            var settings = options.ToSettings(catalog);
            Assert.Equal(choice.Id, settings.Leaders[0]);
            Assert.Equal(choice.CivilizationId, settings.Civilizations[0]);
            Assert.Equal(1, settings.PlayerCount);
        }
    }
}
