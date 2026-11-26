using Ariel.Client;
using Ariel.Client.Terrain;
using Ariel.Game;
using Xunit;

namespace Ariel.Mods.Tests;

/// <summary>The slope and enclosure controls, one case per arrangement, all built through SetCliff so the heights, the
/// presence bits and the derived lifts are what the game would hold. Directions: E=0 NE=1 NW=2 W=3 SW=4 SE=5.</summary>
public sealed class CliffSlopeRulesTests(LoadedMods mods) : IClassFixture<LoadedMods>
{
    private Simulation Flat() => Simulation.CreateScenario(mods.Content, GameFactory.Settings(), new(12, 8, false),
        Enumerable.Range(0, 96).Select(_ => new Plot { Terrain = "world:grass" }).ToArray());

    /// <summary>Each case raises cliffs as "col,row:directions" groups (the tile the cliff is set on, then the edges)
    /// and expects the report to be empty or to hold a line containing <paramref name="report"/>.</summary>
    [Theory]
    [InlineData("one cliff stands alone", "4,3:0", null)]
    [InlineData("two cliffs facing opposite ways on one tile may share the plateau between them", "4,3:03", null)]
    [InlineData("two walls on neighbouring edges of one tile are fine", "4,3:05", null)]
    [InlineData("two mounds three tiles apart keep their slopes to themselves", "3,3:0 6,3:0", null)]
    [InlineData("a mass walled on every side has no way down", "4,3:012345", "walled in")]
    [InlineData("three raised tiles round one vertex are walled in", "4,3:2345 5,3:0145 5,2:0123", "walled in")]
    [InlineData("a two-tile bluff leaves its slope no room before the next face", "4,3:4 5,3:4", "meets a cliff face")]
    [InlineData("a ridge walled along one side leaves its slope no room", "4,3:45 5,3:4 6,3:1245", "meets a cliff face")]
    // 3,3 is the high side and drops east; 5,3 is the high side and drops west, onto that same low tile.
    [InlineData("two mounds facing each other across one low tile are too close", "3,3:0 5,3:3", "too close")]
    public void Cliff_arrangements_report_what_breaks_the_slope_rules(string arrangement, string cliffs, string? report)
    {
        var sim = Flat();
        foreach (var group in cliffs.Split(' '))
        {
            var (tile, directions) = (group.Split(':')[0].Split(','), group.Split(':')[1]);
            foreach (char d in directions)
                sim.State.SetCliff(sim.State.Grid.TileAt(HexGrid.FromOffset(int.Parse(tile[0]), int.Parse(tile[1]))), d - '0', true);
        }
        var lines = sim.Rules().MapGeometry.Report(sim);
        if (report is null) Assert.True(lines.Count == 0, $"{arrangement}: {string.Join("; ", lines)}");
        else Assert.True(lines.Any(line => line.Contains(report)), $"{arrangement}: no line says \"{report}\" in [{string.Join("; ", lines)}]");
    }
}
