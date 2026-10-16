using System.Numerics;
using System.Runtime.InteropServices;
using Civ.Client;
using Civ.Client.Terrain;
using Civ.Game;
using Xunit;
using Schema = Civ.Content.Schema;

namespace Civ.Mods.Tests;

/// <summary>Generated-world contracts at the persistence, local-team visibility, edit and picking boundaries.</summary>
public sealed class TerrainIntegrationTests(LoadedMods mods) : IClassFixture<LoadedMods>
{
    private readonly GameCatalog _catalog = new(mods.Sources);

    private GameSession Generated(bool reveal = false)
    {
        var session = new GameSession(GameFactory.Create(mods.Content, seed: 42));
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
        var session = Generated();
        // A half-explored generated world exercises many coasts, features and cloud boundaries.
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
    public void Removing_a_generated_forest_rebuilds_only_its_neighbourhood_and_preserves_distant_props()
    {
        var session = Generated(reveal: true);
        var before = Snapshot(session);
        var chunks = new TerrainChunks(before.Grid.Spec);
        var field = Field(session, before);
        var planner = new ScatterPlanner(_catalog);
        var palette = new TerrainPalette(_catalog);
        var props = chunks.Chunks.Select(c => planner.Plan(c, before, field, palette)).ToArray();
        // An isolated forest makes complete tile clearing observable without neighbouring trees blending onto it.
        int tile = before.Grid.Tiles.First(t => before.Feature[t.Value] == "world:forest"
            && props.Any(p => p.Any(v => v.Tile == t.Value && v.Set.StartsWith("forest", StringComparison.Ordinal)))
            && Enumerable.Range(0, 6).All(d => !before.TryTile(before.Grid.HexOf(t).Neighbor(d), out int n)
                || before.Feature[n] is not ("world:forest" or "world:jungle"))).Value;
        var (x, z) = before.Grid.HexOf(new TileId(tile)).ToPixel(1);
        session.Sim.State.Plots[tile].Feature = null;
        var after = Snapshot(session);
        var nextField = field.With(after);
        var oldSignatures = chunks.Signatures(before);
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
                EqualMesh(TerrainMesher.Build(chunk, field, chunks.Resolution(chunk, before)),
                    TerrainMesher.Build(chunk, nextField, chunks.Resolution(chunk, after)));
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
