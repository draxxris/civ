using Ariel.Content;
using Ariel.Content.Schema;
using Ariel.Modding;
using Ariel.Mods.Espionage;
using Xunit;

namespace Ariel.Mods.Tests;

/// <summary>The Espionage mod is self-contained: the base game and the main mods load, and say nothing of espionage,
/// without it. What the base keeps that is about spies is listed in docs/ESPIONAGE.md ("Integration").</summary>
public sealed class EspionageAbsenceTests(LoadedMods mods) : IClassFixture<LoadedMods>
{
    [Fact]
    public void Without_the_mod_no_rules_service_commerce_type_or_turn_system_is_espionage()
    {
        var content = mods.Content;
        Assert.DoesNotContain("espionage", content.LoadedMods);
        Assert.Null(content.Services.Find<IEspionageService>());
        Assert.False(content.Commerce.TryGet(EspionageCommerce.Key, out _));
        Assert.Equal(["gold", "research", "culture"], content.Commerce.Select(t => t.Key));
        Assert.DoesNotContain(content.TurnSystems.All, s => s.Id.StartsWith("espionage:", StringComparison.Ordinal));
        Assert.DoesNotContain(content.Get<ProcessDef>().All, p => p.Id.StartsWith("espionage:", StringComparison.Ordinal));
        Assert.DoesNotContain(content.Get<UnitDef>().All, u => u.Id.StartsWith("espionage:", StringComparison.Ordinal));
        Assert.Equal(0, content.Get<BuildingDef>("cities:palace").CommerceChanges.Espionage);
    }

    [Fact]
    public void No_data_file_of_the_base_game_or_a_main_mod_names_the_mods_content()
    {
        var files = mods.Sources.Select(s => Path.Combine(s.Root, "data")).Where(Directory.Exists)
            .SelectMany(d => Directory.EnumerateFiles(d, "*.json", SearchOption.AllDirectories)).ToArray();
        Assert.NotEmpty(files);
        Assert.Empty(files.Where(f => File.ReadAllText(f).Contains("\"espionage:", StringComparison.Ordinal))
            .Select(f => Path.GetRelativePath(AppContext.BaseDirectory, f)));
    }

    [Fact]
    public void With_the_mod_its_amendments_add_espionage_to_base_rows()
    {
        var content = ModLoader.Build(mods.Sources
            .Append(ModLoader.FromDirectory(Path.Combine(AppContext.BaseDirectory, "optional-mods", "Espionage"))));
        Assert.Equal(4, content.Get<BuildingDef>("cities:palace").CommerceChanges.Espionage);
        Assert.Equal(content.Get<BuildingDef>("cities:palace").CommerceChanges.Gold, mods.Content.Get<BuildingDef>("cities:palace").CommerceChanges.Gold);
        Assert.Contains(content.Get<UnitDef>().All, u => u.Id == "espionage:spy");
        Assert.True(content.Get<SpecialistDef>("espionage:specialist_spy").Commerces.Espionage > 0);
    }
}
