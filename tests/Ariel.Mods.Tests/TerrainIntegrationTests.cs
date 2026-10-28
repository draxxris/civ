using System.Numerics;
using System.Runtime.InteropServices;
using Ariel.Client;
using Ariel.Client.Terrain;
using Ariel.Game;
using Xunit;
using Schema = Ariel.Content.Schema;

namespace Ariel.Mods.Tests;

/// <summary>Terrain contracts at the persistence, local-team visibility, edit and picking boundaries.</summary>
public sealed class TerrainIntegrationTests(LoadedMods mods) : IClassFixture<LoadedMods>
{
    private readonly GameCatalog _catalog = mods.Catalog;

    private GameSession Generated(bool reveal = false, bool compact = false)
    {
        // The compact world still uses the real generator and normal player setup.
        var sim = compact
            ? new Simulation(mods.Content, GameFactory.Settings() with
                { Seed = 42, WorldSize = "foundation:worldsize_duel" })
            : GameFactory.Create(mods.Content, seed: 42);
        var session = new GameSession(sim);
        if (reveal)
            foreach (var plot in session.Sim.State.Plots) plot.Revealed.Add(session.Me.TeamId);
        return session;
    }

    private TerrainSnapshot Snapshot(GameSession session)
    {
        var palette = new TerrainPalette(_catalog);
        return TerrainSnapshot.From(session, palette.LayerOf, palette.FeatureGroundOf);
    }

    private static TerrainField Field(GameSession session, TerrainSnapshot snapshot)
    {
        ulong seed = session.Sim.Settings.Seed;
        return new(snapshot, unchecked((int)(seed ^ (seed >> 32))));
    }

    [Fact]
    public void Saving_and_loading_a_generated_world_preserves_known_terrain_and_surface_heights()
    {
        var session = Generated();
        // Include fogged land and water, while retaining unexplored plots and current sight.
        for (int i = 0; i < session.Sim.State.Plots.Length; i += 2)
            session.Sim.State.Plots[i].Revealed.Add(session.Me.TeamId);
        var before = Snapshot(session);
        var restored = new GameSession(SaveGame.Load(mods.Content, session.SaveToJson()));
        var after = Snapshot(restored);
        Assert.Equal(before.Grid.Spec, after.Grid.Spec);
        Assert.Equal(before.Layer, after.Layer);
        Assert.Equal(before.Relief, after.Relief);
        Assert.Equal(before.Rivers, after.Rivers);
        Assert.Equal(before.FeatureGround, after.FeatureGround);
        Assert.Equal(before.Owner, after.Owner);
        Assert.Equal(before.Sight, after.Sight);
        Assert.Equal(before.Terrain, after.Terrain);
        Assert.Equal(before.Feature, after.Feature);
        var a = Field(session, before);
        var b = Field(restored, after);
        foreach (var tile in before.Grid.Tiles)
        {
            var (x, z) = before.Grid.HexOf(tile).ToPixel(1);
            foreach (double offset in new[] { 0, 0.37, -0.61 })
            {
                Assert.Equal(a.HeightAt(x + offset, z + offset), b.HeightAt(x + offset, z + offset));
                Assert.Equal(a.SurfaceHeightAt(x + offset, z + offset), b.SurfaceHeightAt(x + offset, z + offset));
            }
        }
    }

    [Fact]
    public void Mutating_every_unexplored_plot_cannot_change_any_chunk_mesh_or_scatter()
    {
        var session = Generated(compact: true);
        // A half-explored generated world exercises coasts, features and cloud boundaries.
        var state = session.Sim.State;
        for (int i = 0; i < state.Plots.Length / 2; i++) state.Plots[i].Revealed.Add(session.Me.TeamId);
        var before = Snapshot(session);
        var hidden = state.Grid.Tiles.Where(t => session.SightOf(state.Plots[t.Value]) == Sight.Hidden).ToArray();
        Assert.NotEmpty(hidden);
        foreach (var tile in hidden)
        {
            var plot = state.Plots[tile.Value];
            plot.Kind = tile.Value % 3 == 0 ? PlotKind.Peak : PlotKind.Flat;
            plot.Terrain = tile.Value % 3 == 1 ? "world:ocean" : "world:desert";
            plot.IsLake = tile.Value % 5 == 0;
            plot.Feature = tile.Value % 2 == 0 ? "world:jungle" : "world:forest";
            // Change only the hidden side: the known side of a shared edge remains public information.
            plot.RiverEdges = (byte)((tile.Value * 17 + 11) % 64);
            plot.Improvement = "world:improvement_improvement_mine";
            plot.Owner = 1;
        }
        var after = Snapshot(session);
        var a = Field(session, before);
        var b = Field(session, after);
        var chunks = new TerrainChunks(state.Map);
        var planner = new ScatterPlanner(_catalog);
        var palette = new TerrainPalette(_catalog);
        int instances = 0;
        foreach (var chunk in chunks.Chunks)
        {
            EqualMesh(TerrainMesher.Build(chunk, a), TerrainMesher.Build(chunk, b));
            var props = planner.Plan(chunk, before, a, palette);
            EqualScatter(props, planner.Plan(chunk, after, b, palette));
            instances += props.Count;
        }
        Assert.True(instances > 0, "The known half must exercise scatter, not only empty chunks.");
    }

    [Fact]
    public void Removing_a_fogged_forest_stays_hidden_until_reobserved_then_rebuilds_only_its_neighbourhood()
    {
        // Isolate the edit lifecycle from generation: two distant groves and an isolated forest near a chunk corner.
        // Generated terrain, features and scatter remain covered by the unexplored-mutation test above.
        var session = TerrainTestMaps.RevealedSession(mods.Content);
        int tile = session.Sim.State.Grid.TileAt(HexGrid.FromOffset(11, 6)).Value;
        foreach (var hex in new[] { HexGrid.FromOffset(11, 6), HexGrid.FromOffset(1, 2), HexGrid.FromOffset(20, 12) })
            session.Sim.State.PlotAt(hex).Feature = "world:forest";
        foreach (var plot in session.Sim.State.Plots) plot.Visible.Clear();
        var visibility = session.Sim.Content.Services.Get<IVisibilityService>().Single();
        foreach (var knownTile in session.Sim.State.Grid.Tiles)
            visibility.RevealPlot(session.Sim, knownTile, session.Me.TeamId);
        var before = Snapshot(session);
        var chunks = new TerrainChunks(before.Grid.Spec);
        var field = Field(session, before);
        var planner = new ScatterPlanner(_catalog);
        var palette = new TerrainPalette(_catalog);
        var props = chunks.Chunks.Select(c => planner.Plan(c, before, field, palette)).ToArray();
        var resolutions = chunks.Resolutions(before);
        var meshes = chunks.Chunks.Select(c => TerrainMesher.Build(c, field, chunks.Resolution(c, resolutions))).ToArray();
        Assert.Equal(Sight.Fogged, before.Sight[tile]);
        Assert.Contains(props.SelectMany(p => p), p => p.Tile == tile && p.Set.StartsWith("forest", StringComparison.Ordinal));
        var oldSignatures = chunks.Signatures(before);
        var (x, z) = before.Grid.HexOf(new TileId(tile)).ToPixel(1);
        session.Sim.State.Plots[tile].Feature = null;
        var fogged = Snapshot(session);
        Assert.Equal(Sight.Fogged, fogged.Sight[tile]);
        Assert.Equal("world:forest", fogged.Feature[tile]);
        var foggedSignatures = chunks.Signatures(fogged);
        Assert.Equal(oldSignatures, foggedSignatures);
        Assert.Empty(TerrainChunks.DirtyChunks(oldSignatures, foggedSignatures));
        var foggedField = field.With(fogged);
        foreach (var chunk in chunks.Chunks)
        {
            EqualMesh(meshes[chunk.Id], TerrainMesher.Build(chunk, foggedField, chunks.Resolution(chunk, fogged)));
            EqualScatter(props[chunk.Id], planner.Plan(chunk, fogged, foggedField, palette));
        }
        // An actual observer reacquires sight; do not edit the stored snapshot or visibility masks.
        var observer = new Unit { Id = session.Sim.State.NextUnitId++, Owner = GameSession.Human,
            Type = "military:warrior", Position = before.Grid.HexOf(new TileId(tile)) };
        session.Sim.State.AddUnit(observer);
        visibility.Recompute(session.Sim);
        var after = Snapshot(session);
        Assert.Equal(Sight.Visible, after.Sight[tile]);
        Assert.Null(after.Feature[tile]);
        var nextField = field.With(after);
        var newSignatures = chunks.Signatures(after);
        var dirty = TerrainChunks.DirtyChunks(oldSignatures, newSignatures).ToHashSet();
        Assert.NotEmpty(dirty);
        Assert.Contains(chunks.Chunks.Single(c => c.Contains(x, z)).Id, dirty);
        Assert.True(dirty.Count < chunks.Chunks.Count / 4);
        int distantInstances = 0;
        foreach (var chunk in chunks.Chunks)
        {
            var next = planner.Plan(chunk, after, nextField, palette);
            Assert.DoesNotContain(next, p => p.Tile == tile && p.Set.StartsWith("forest", StringComparison.Ordinal));
            double distance = new[] { x - before.Period, x, x + before.Period }.Min(cx =>
                Math.Sqrt(Math.Pow(Math.Max(Math.Max(chunk.X0 - cx, cx - chunk.X1), 0), 2)
                    + Math.Pow(Math.Max(Math.Max(chunk.Z0 - z, z - chunk.Z1), 0), 2)));
            if (dirty.Contains(chunk.Id)) Assert.InRange(distance, 0, Math.Sqrt(3) + 1);
            else
            {
                Assert.Equal(oldSignatures[chunk.Id], newSignatures[chunk.Id]);
                EqualScatter(props[chunk.Id], next);
                EqualMesh(meshes[chunk.Id], TerrainMesher.Build(chunk, nextField, chunks.Resolution(chunk, after)));
                distantInstances += next.Count;
            }
        }
        Assert.True(distantInstances > 0);
    }

    [Fact]
    public void Camera_pitch_rays_pick_every_generated_land_tile_centre()
    {
        var session = Generated(reveal: true);
        var snapshot = Snapshot(session);
        var field = Field(session, snapshot);
        double pitch = 62 * Math.PI / 180;
        var direction = new Vector3(0, (float)-Math.Sin(pitch), (float)-Math.Cos(pitch));
        int checkedTiles = 0;
        foreach (var tile in snapshot.Grid.Tiles)
        {
            if (mods.Content.Get<Schema.TerrainDef>(session.Sim.State.Plots[tile.Value].Terrain).IsWater) continue;
            var hex = snapshot.Grid.HexOf(tile);
            var (x, z) = hex.ToPixel(1);
            var aim = new Vector3((float)x, (float)field.SurfaceHeightAt(x, z), (float)z);
            var hit = TerrainRay.Pick(aim - direction * 18, direction, field, includeWaterSurface: true);
            Assert.NotNull(hit);
            Assert.Equal(hex, session.Pick(hit.Value.X, hit.Value.Z));
            checkedTiles++;
        }
        Assert.True(checkedTiles > 1000);
    }

    private static void EqualMesh(TerrainMeshData a, TerrainMeshData b)
    {
        Assert.True(MemoryMarshal.AsBytes(a.Positions.AsSpan()).SequenceEqual(MemoryMarshal.AsBytes(b.Positions.AsSpan())));
        Assert.True(MemoryMarshal.AsBytes(a.Normals.AsSpan()).SequenceEqual(MemoryMarshal.AsBytes(b.Normals.AsSpan())));
        Assert.Equal(a.Indices, b.Indices);
    }

    private static void EqualScatter(IReadOnlyList<ScatterInstance> a, IReadOnlyList<ScatterInstance> b)
    {
        Assert.Equal(a, b);
        static int[] Bits(ScatterInstance p) => new[] { p.X, p.Y, p.Z, p.Yaw, p.Scale }.Select(BitConverter.SingleToInt32Bits).ToArray();
        for (int i = 0; i < a.Count; i++) Assert.Equal(Bits(a[i]), Bits(b[i]));
    }
}
