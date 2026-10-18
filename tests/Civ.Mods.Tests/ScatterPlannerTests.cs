using Civ.Client;
using Civ.Client.Terrain;
using Civ.Game;
using Xunit;

namespace Civ.Mods.Tests;

public sealed class ScatterPlannerTests(LoadedMods mods) : IClassFixture<LoadedMods>
{
    private readonly GameCatalog _catalog = mods.Catalog;
    private readonly TerrainPalette _palette = new(mods.Catalog);
    private readonly ScatterPlanner _planner = new(mods.Catalog);
    private static readonly double Sqrt3 = Math.Sqrt(3);

    // Local contracts need several chunk rows/columns, not the statistical density fixture's 32 rows.
    private TerrainSnapshot Map(string? right = "world:forest", int width = 16, int height = 16)
    {
        var session = TerrainTestMaps.RevealedSession(mods.Content, width, height);
        for (int row = 0; row < height; row++)
            for (int col = 0; col < width; col++)
                session.Sim.State.Plots[row * width + col].Feature = col < width / 2 ? "world:forest" : right;
        return TerrainSnapshot.From(session, _palette.LayerOf, _palette.FeatureGroundOf);
    }

    private static TerrainChunk Whole(TerrainSnapshot s) => new(0, 0, 0, 0, s.Width, -2, s.Height + 1);

    private IReadOnlyList<ScatterInstance> Plan(TerrainSnapshot snapshot, int seed = 17, bool warp = false, int[]? extras = null)
    {
        var field = new TerrainField(snapshot, seed, new TerrainTuning { WarpAmplitude = warp ? 0.3 : 0, FlatNoise = 0 });
        return _planner.Plan(Whole(snapshot), snapshot, field, _palette, extras);
    }

    [Fact]
    public void Seed_reproduces_every_instance_but_a_different_seed_changes_the_scatter()
    {
        var a = Plan(Map("world:jungle"), warp: true);
        Assert.NotEmpty(a);
        Assert.Equal(a, Plan(Map("world:jungle"), warp: true));
        Assert.NotEqual(a.ToArray(), Plan(Map("world:jungle"), seed: 18, warp: true).ToArray());
        Assert.All(a, prop => Assert.InRange(prop.ModelIndex, 0, _catalog.ScatterModels(prop.Set).Count - 1));
    }

    // Equal-area strips across 14 independent shared edges avoid drawing conclusions from a handful of points.
    // Even-row tile centres have x = column * sqrt(3); their shared edge is x = 11.5 * sqrt(3).
    private static bool InStrip(ScatterInstance prop, double offset, double halfWidth = 0.2)
    {
        if (Math.Abs(prop.X - (11.5 * Sqrt3 + offset)) >= halfWidth) return false;
        for (int row = 2; row < 30; row += 2)
            if (Math.Abs(prop.Z - row * 1.5) < 0.35) return true;
        return false;
    }

    private IReadOnlyList<ScatterInstance> BorderPlan(string? right = "world:forest")
    {
        var snapshot = Map(right, width: 24, height: 32);
        var field = new TerrainField(snapshot, 17, new TerrainTuning { WarpAmplitude = 0, FlatNoise = 0 });
        // Contains every point in all 14 strips, including both interior controls. Keep the full snapshot
        // so blend contributors are unchanged; global scatter hashing does not depend on chunk bounds.
        return _planner.Plan(new(0, 0, 0, 10, 13, 1, 29), snapshot, field, _palette);
    }

    [Fact]
    public void Adjacent_forests_fill_the_shared_edge_at_the_interior_density()
    {
        var props = BorderPlan();
        int edge = props.Count(p => InStrip(p, 0));
        int interior = props.Count(p => InStrip(p, -Sqrt3 / 2));
        Assert.True(interior > 40, $"Interior sample too small: {interior}");
        Assert.InRange(edge / (double)interior, 0.7, 1.3);
    }

    [Fact]
    public void Forest_thins_from_interior_to_border_to_open_plains()
    {
        var props = BorderPlan(null);
        int interior = props.Count(p => InStrip(p, -Sqrt3 / 2));
        int border = props.Count(p => InStrip(p, 0));
        int plains = props.Count(p => InStrip(p, Sqrt3 / 2));
        Assert.True(interior > border && border > plains, $"Densities: {interior}, {border}, {plains}");
        Assert.Equal(0, plains);
    }

    [Fact]
    public void Forest_and_jungle_mix_species_within_point_three_of_their_shared_edge()
    {
        var edge = BorderPlan("world:jungle").Where(p => InStrip(p, 0, 0.3)).ToArray();
        Assert.Contains(edge, p => p.Set == "forest");
        Assert.Contains(edge, p => p.Set == "jungle");
    }

    [Fact]
    public void Hidden_content_never_changes_scatter_and_cloud_covered_points_have_no_props()
    {
        var session = TerrainTestMaps.RevealedSession(mods.Content, 16, 12);
        foreach (var plot in session.Sim.State.Plots) plot.Feature = "world:forest";
        var hidden = session.Sim.State.Plots[6 * 16 + 8];
        hidden.Revealed = new TeamMask();
        hidden.Visible = new TeamMask();
        var palette = new TerrainPalette(_catalog);
        var before = TerrainSnapshot.From(session, palette.LayerOf, palette.FeatureGroundOf);
        var props = Plan(before, warp: true);
        Assert.NotEmpty(props);
        Assert.DoesNotContain(props, p => p.Tile == 6 * 16 + 8);
        // Hidden tile's cloud triangle reaches its revealed neighbours, not just its containing hex.
        var centre = HexGrid.FromOffset(8, 6).ToPixel(1);
        Assert.DoesNotContain(props, p => Math.Abs(p.X - centre.X) < 0.7 && Math.Abs(p.Z - centre.Y) < 0.5);
        hidden.Feature = "world:jungle";
        hidden.Kind = PlotKind.Hills;
        hidden.Terrain = "world:snow";
        Assert.Equal(props, Plan(TerrainSnapshot.From(session, palette.LayerOf, palette.FeatureGroundOf), warp: true));
    }

    [Theory]
    [InlineData(ScatterPlanner.ClearCentre)]
    [InlineData(ScatterPlanner.ClearTile)]
    public void Occupied_centres_and_cleared_tiles_have_no_props_in_the_clear_radius(int flag)
    {
        var snapshot = Map();
        int tile = 10 * snapshot.Width + 10;
        var (x, z) = snapshot.Grid.HexOf(new TileId(tile)).ToPixel(1);
        bool AtCentre(ScatterInstance p) => (p.X - x) * (p.X - x) + (p.Z - z) * (p.Z - z) < 0.35 * 0.35;
        Assert.Contains(Plan(snapshot), AtCentre);
        var extras = new int[snapshot.Grid.Count];
        extras[tile] = flag;
        var props = Plan(snapshot, extras: extras);
        Assert.DoesNotContain(props, AtCentre);
        Assert.Contains(props, p => p.Tile == tile + 1);
        if (flag == ScatterPlanner.ClearTile) Assert.DoesNotContain(props, p => p.Tile == tile);
    }

    [Fact]
    public void Chunk_partition_and_wrap_copies_preserve_candidates_without_gaps_or_duplicates()
    {
        var snapshot = Map("world:jungle");
        var field = new TerrainField(snapshot, 31);
        var planner = new ScatterPlanner(_catalog);
        var palette = new TerrainPalette(_catalog);
        var chunks = new TerrainChunks(snapshot.Grid.Spec);
        var all = planner.Plan(Whole(snapshot), snapshot, field, palette);
        var partitioned = chunks.Chunks.SelectMany(c => planner.Plan(c, snapshot, field, palette)).ToArray();
        Assert.NotEmpty(all);
        Assert.Equal(all.Count, partitioned.Distinct().Count());
        Assert.True(all.ToHashSet().SetEquals(partitioned));
        // A shifted whole-period chunk hashes the same periodic cells and resolves the same tiles/species.
        var shifted = Whole(snapshot) with { UnitX0 = snapshot.Width, UnitX1 = 2 * snapshot.Width };
        var repeated = planner.Plan(shifted, snapshot, field, palette);
        Assert.Equal(all.Count, repeated.Count);
        for (int i = 0; i < all.Count; i++)
        {
            Assert.Equal(all[i].Set, repeated[i].Set);
            Assert.Equal(all[i].ModelIndex, repeated[i].ModelIndex);
            Assert.Equal(all[i].Tile, repeated[i].Tile);
            Assert.InRange(Math.Abs(repeated[i].X - all[i].X - snapshot.Period), 0, 1e-5);
            Assert.Equal(all[i].Z, repeated[i].Z);
            Assert.Equal(all[i].Y, repeated[i].Y, 5);
        }
    }

    [Fact]

    public void Rivers_and_water_exclude_props_while_hills_keep_trees_without_rocks()
    {
        var snapshot = Map();
        Array.Fill(snapshot.Relief, ReliefClass.Hills);
        TerrainTestMaps.SetRiver(snapshot, 10 * snapshot.Width + 10, 0);
        int water = 12 * snapshot.Width + 10;
        snapshot.Relief[water] = ReliefClass.ShallowWater;
        var field = new TerrainField(snapshot, 17, new TerrainTuning { WarpAmplitude = 0 });
        var props = _planner.Plan(Whole(snapshot), snapshot, field, _palette);
        Assert.Contains(props, p => p.Set == "forest");
        Assert.DoesNotContain(props, p => p.Set == "rocks");
        Assert.All(props, p =>
        {
            Assert.True(field.HeightAt(p.X, p.Z) > 0.02);
            Assert.Equal(0, field.Rivers.Carve(p.X, p.Z));
        });
        var (x, z) = snapshot.Grid.HexOf(new TileId(water)).ToPixel(1);
        Assert.DoesNotContain(props, p => Math.Abs(p.X - x) < 0.3 && Math.Abs(p.Z - z) < 0.3);
    }

    [Fact]
    public void Clearing_and_chopping_dirty_nearby_chunks_but_fog_does_not()
    {
        var snapshot = Map();
        var chunks = new TerrainChunks(snapshot.Grid.Spec);
        var extras = new int[snapshot.Grid.Count];
        var before = chunks.Signatures(snapshot, extras);
        snapshot.Sight[10 * snapshot.Width + 10] = Sight.Fogged;
        Assert.Empty(TerrainChunks.DirtyChunks(before, chunks.Signatures(snapshot, extras)));
        extras[10 * snapshot.Width + 10] = ScatterPlanner.ClearCentre;
        var dirty = TerrainChunks.DirtyChunks(before, chunks.Signatures(snapshot, extras));
        Assert.NotEmpty(dirty);
        Assert.True(dirty.Count < chunks.Chunks.Count / 2);
        before = chunks.Signatures(snapshot, extras);
        snapshot.Feature[10 * snapshot.Width + 10] = null;
        Assert.NotEmpty(TerrainChunks.DirtyChunks(before, chunks.Signatures(snapshot, extras)));
    }
}
