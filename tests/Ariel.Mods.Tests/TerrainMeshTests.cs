using System.Numerics;
using Ariel.Client;
using Ariel.Client.Terrain;
using Ariel.Game;
using Xunit;

namespace Ariel.Mods.Tests;

/// <summary>Chunk meshing, dirty signatures and terrain picking: no cracks at chunk borders or the wrap, no stale chunk
/// after a map change or a reveal, and picks that land on the slope under the cursor.</summary>
public sealed class TerrainMeshTests(LoadedMods mods, TerrainMeshTests.MeshBaseline baseline)
    : IClassFixture<LoadedMods>, IClassFixture<TerrainMeshTests.MeshBaseline>
{
    private static readonly double Sqrt3 = TerrainTestMaps.Sqrt3;

    /// <summary>One rendered map serves both fixture calls: the border test needs a resolution boundary between fine and
    /// default chunks and the anchor test needs a vertex at every tile centre, and neither needs the peak lattice over the
    /// whole map. Both properties are read-only fixtures, so sharing them neither changes a check nor duplicates the
    /// meshing; tests must not mutate their snapshot or output arrays.</summary>
    public sealed class MeshBaseline
    {
        private readonly Lazy<RenderedMap> _rendered = new(() => new RenderedMap(ConcentratedPeaks()));
        public RenderedMap Mixed => _rendered.Value;
        public RenderedMap MixedResolution => _rendered.Value;
    }

    public sealed class RenderedMap
    {
        public TerrainField Field { get; }
        public TerrainChunks Chunks { get; }
        public int[] Resolutions { get; }
        public TerrainMeshData[] Meshes { get; }

        public RenderedMap(TerrainSnapshot snapshot)
        {
            Field = new(snapshot, 8);
            Chunks = new(snapshot.Grid.Spec);
            Resolutions = Chunks.Resolutions(snapshot);
            Meshes = Chunks.Chunks.Select(c => TerrainMesher.Build(c, Field, Chunks.Resolution(c, Resolutions))).ToArray();
        }
    }

    private static (float X, float Z) Key(float[] p, int v) => (p[v * 3], p[v * 3 + 2]);

    /// <summary>The mixed test map with its peaks kept inside one chunk column and one chunk row, so those chunks mesh at
    /// the peak resolution and every other chunk at the default one. The fixture asserts vertex identity across that
    /// resolution boundary and an anchor at every tile centre, not how much of the map is a peak. Cliff bits outside the
    /// same window are dropped for the same reason: a cliff forces its chunk to the peak resolution too.</summary>
    private static TerrainSnapshot ConcentratedPeaks()
    {
        var snapshot = TerrainTestMaps.Mixed();
        foreach (var tile in snapshot.Grid.Tiles)
        {
            int col = tile.Value % snapshot.Width, row = tile.Value / snapshot.Width;
            if (col >= 4 && col < 8 && row < 2) continue;
            if (snapshot.Relief[tile.Value] == ReliefClass.Peak) snapshot.Relief[tile.Value] = ReliefClass.Hills;
            snapshot.Cliffs[tile.Value] = 0;
        }
        return snapshot;
    }

    [Fact]
    public void Neighbouring_chunks_share_identical_border_vertices()
    {
        var rendered = baseline.MixedResolution;
        var chunks = rendered.Chunks;
        Assert.Equal(2, rendered.Resolutions.Distinct().Count());
        var seen = new Dictionary<(float X, float Z), (float Y, float Nx, float Ny, float Nz, float Cavity)>();
        int shared = 0;
        foreach (var mesh in rendered.Meshes)
            for (int v = 0; v < mesh.VertexCount; v++)
            {
                var value = (mesh.Positions[v * 3 + 1], mesh.Normals[v * 3], mesh.Normals[v * 3 + 1], mesh.Normals[v * 3 + 2],
                    mesh.Cavity[v]);
                if (seen.TryGetValue(Key(mesh.Positions, v), out var other)) { Assert.Equal(other, value); shared++; }
                else seen.Add(Key(mesh.Positions, v), value);
            }
        // Every internal chunk edge contributes a full lattice line of shared vertices.
        Assert.True(shared > (chunks.Columns - 1) * chunks.Rows * TerrainChunks.TileRows * TerrainMesher.DefaultResolution);
    }

    [Fact]
    public void A_fine_chunk_edge_lies_on_its_coarse_neighbour_without_cracks()
    {
        var rendered = baseline.MixedResolution;
        var chunks = rendered.Chunks;
        var resolutions = rendered.Resolutions;
        var meshes = rendered.Meshes;
        int checkedVertices = 0;
        foreach (var fine in chunks.Chunks.Where(c => resolutions[c.Id] == TerrainMesher.PeakResolution))
            foreach (var coarse in chunks.Chunks.Where(c => resolutions[c.Id] == TerrainMesher.DefaultResolution
                && Math.Abs(c.Row - fine.Row) + Math.Abs(c.Column - fine.Column) == 1))
            {
                var mesh = meshes[fine.Id];
                foreach (int v in SharedEdgeVertices(mesh, fine, coarse))
                {
                    double x = mesh.Positions[v * 3], z = mesh.Positions[v * 3 + 2];
                    x = Math.Clamp(x, coarse.X0, coarse.X1);
                    z = Math.Clamp(z, coarse.Z0, coarse.Z1);
                    // Exact lattice agreement is guaranteed; float storage leaves ~1e-7, and a 1e-5 epsilon is tighter
                    // than rounding both heights to four decimals (which admits 5e-5 and flips on a boundary).
                    Assert.Equal(TerrainTestMaps.HeightAt(meshes[coarse.Id], x, z), mesh.Positions[v * 3 + 1], 1e-5);
                    checkedVertices++;
                }
            }
        Assert.True(checkedVertices > 100);
    }

    private static IEnumerable<int> SharedEdgeVertices(TerrainMeshData mesh, TerrainChunk a, TerrainChunk b)
    {
        const double e = 1e-4;
        double x0 = Math.Max(a.X0, b.X0), x1 = Math.Min(a.X1, b.X1);
        double z0 = Math.Max(a.Z0, b.Z0), z1 = Math.Min(a.Z1, b.Z1);
        return Enumerable.Range(0, mesh.VertexCount).Where(v =>
            mesh.Positions[v * 3] >= x0 - e && mesh.Positions[v * 3] <= x1 + e
            && mesh.Positions[v * 3 + 2] >= z0 - e && mesh.Positions[v * 3 + 2] <= z1 + e);
    }


    [Fact]
    public void Cavity_is_negative_on_a_lone_summit_and_positive_in_the_ring_at_its_foot()
    {
        // One noise-free peak on flat land: a convex summit, then a concave foot where its flank meets the plain.
        var snapshot = TerrainTestMaps.Uniform(ReliefClass.Flat);
        var peak = HexGrid.FromOffset(8, 6);
        snapshot.Relief[snapshot.ClampedTile(peak)] = ReliefClass.Peak;
        var tuning = new TerrainTuning { FlatNoise = 0, PeakNoise = 0, PeakRoughness = 0, PeakErosion = 0, WarpAmplitude = 0 };
        var field = new TerrainField(snapshot, 1, tuning);
        var chunks = new TerrainChunks(field.Snapshot.Grid.Spec);
        var (px, pz) = TerrainTestMaps.Centre(peak);
        var meshes = chunks.Chunks.Where(c => c.Contains(px, pz) || c.Contains(px + 1.8, pz) || c.Contains(px - 1.8, pz))
            .Select(c => TerrainMesher.Build(c, field)).ToArray();
        var vertices = meshes.SelectMany(m => Enumerable.Range(0, m.VertexCount)
            .Select(v => (Y: m.Positions[v * 3 + 1], Cavity: m.Cavity[v]))).ToArray();
        var summit = vertices.MaxBy(v => v.Y);
        Assert.True(summit.Cavity < 0);
        // The foot lies between the plain and the flank: a little above the flat level.
        var foot = vertices.Where(v => v.Y > tuning.FlatBase + 0.02 && v.Y < tuning.FlatBase + 0.12).ToArray();
        Assert.NotEmpty(foot);
        Assert.True(foot.Average(v => v.Cavity) > 0);
    }

    [Fact]
    public void Chunk_triangles_face_up_and_tile_the_chunk_rectangle_exactly_once()
    {
        var rendered = baseline.MixedResolution;
        foreach (var chunk in rendered.Chunks.Chunks.Take(6))
        {
            var mesh = rendered.Meshes[chunk.Id];
            var p = mesh.Positions;
            double area = 0;
            for (int i = 0; i < mesh.Indices.Length; i += 3)
            {
                int a = mesh.Indices[i] * 3, b = mesh.Indices[i + 1] * 3, c = mesh.Indices[i + 2] * 3;
                double up = (p[b + 2] - p[a + 2]) * (p[c] - p[a]) - (p[b] - p[a]) * (p[c + 2] - p[a + 2]);
                Assert.True(up > 0);
                area += up / 2;
            }
            Assert.Equal((chunk.X1 - chunk.X0) * (chunk.Z1 - chunk.Z0), area, 2);
        }
    }

    [Fact]
    public void The_east_edge_chunk_matches_the_first_chunk_shifted_by_the_period()
    {
        var rendered = baseline.MixedResolution;
        var chunks = rendered.Chunks;
        float period = (float)rendered.Field.Period;
        int matched = 0;
        for (int row = 0; row < chunks.Rows; row++)
        {
            var westChunk = chunks.Chunks[row * chunks.Columns];
            var eastChunk = chunks.Chunks[row * chunks.Columns + chunks.Columns - 1];
            var west = rendered.Meshes[westChunk.Id];
            var east = rendered.Meshes[eastChunk.Id];
            var westEdge = Enumerable.Range(0, west.VertexCount).Where(v => west.Positions[v * 3] < 0.2f).ToList();
            foreach (int e in Enumerable.Range(0, east.VertexCount).Where(v => east.Positions[v * 3] > period - 0.01f))
            {
                int w = westEdge.Single(v => Math.Abs(west.Positions[v * 3] + period - east.Positions[e * 3]) < 1e-3
                    && west.Positions[v * 3 + 2] == east.Positions[e * 3 + 2]);
                for (int c = 0; c < 3; c++) Assert.Equal(west.Normals[w * 3 + c], east.Normals[e * 3 + c], 4);
                Assert.Equal(west.Positions[w * 3 + 1], east.Positions[e * 3 + 1], 5);
                matched++;
            }
        }
        Assert.True(matched > chunks.Rows * TerrainMesher.DefaultResolution);
    }

    [Fact]
    public void Mesh_anchors_at_every_tile_centre_match_the_field_including_the_seabed()
    {
        var rendered = baseline.Mixed;
        var field = rendered.Field;
        var vertices = new Dictionary<(float X, float Z), float>();
        foreach (var mesh in rendered.Meshes)
            for (int v = 0; v < mesh.VertexCount; v++)
                vertices.TryAdd(Key(mesh.Positions, v), mesh.Positions[v * 3 + 1]);
        foreach (var tile in field.Snapshot.Grid.Tiles)
        {
            var (x, z) = field.Snapshot.Grid.HexOf(tile).ToPixel(1);
            // Centres have exact float lattice coordinates; index actual output, not a mesher-derived expected height.
            Assert.True(vertices.TryGetValue(((float)x, (float)z), out float height));
            Assert.InRange(Math.Abs(height - field.HeightAt(x, z)), 0, 1e-4);
        }
    }

    [Fact]
    public void Surface_picking_hits_water_at_sea_level_not_the_tile_behind_on_the_seabed()
    {
        var field = new TerrainField(TerrainTestMaps.Uniform(ReliefClass.DeepWater), 5);
        var hex = HexGrid.FromOffset(6, 6);
        var (x, z) = hex.ToPixel(1);
        var aim = new Vector3((float)x, 0, (float)z + 0.85f);
        var direction = Vector3.Normalize(new Vector3(0, -1, 1));
        var origin = aim - direction * 20;
        var hit = TerrainRay.Pick(origin, direction, field, includeWaterSurface: true);
        Assert.NotNull(hit);
        Assert.InRange(Vector3.Distance(aim, hit.Value), 0, 1e-3f);
        Assert.Equal(hex, Hex.FromPixel(hit.Value.X, hit.Value.Z, 1));
        var seabed = TerrainRay.Pick(origin, direction, field);
        Assert.NotNull(seabed);
        Assert.NotEqual(hex, Hex.FromPixel(seabed.Value.X, seabed.Value.Z, 1));
    }

    [Theory]
    [InlineData(40, 24)]
    [InlineData(200, 120)]
    [InlineData(16, 9)]
    public void Every_tile_centre_lies_in_exactly_one_chunk(int width, int height)
    {
        var map = new MapSpec(width, height, true);
        var chunks = new TerrainChunks(map);
        var grid = new HexGrid(map);
        foreach (var tile in grid.Tiles)
        {
            var (x, z) = grid.HexOf(tile).ToPixel(1);
            int containing = 0;
            // Visit every chunk so incorrect edge inclusion cannot be hidden by a bounds prefilter.
            foreach (var chunk in chunks.Chunks)
                if (chunk.Contains(x, z)) containing++;
            Assert.Equal(1, containing);
        }
    }

    [Theory]
    [InlineData("terrain", 7, 7)]
    [InlineData("relief", 11, 7)] // reshapes the diagonal chunk across the corner at (4√3, 9)
    [InlineData("relief", 3, 0)]  // north edge: rows beyond it clamp to row 0
    [InlineData("relief", 0, 5)]  // west edge: the wrap image dirties east chunks
    public void Changing_one_plot_dirties_every_chunk_it_reshapes_and_only_chunks_within_one_tile(string change, int col, int row)
    {
        var session = TerrainTestMaps.RevealedSession(mods.Content);
        var target = HexGrid.FromOffset(col, row);
        var before = TerrainTestMaps.Snapshot(session);
        var plot = session.Sim.State.PlotAt(target);
        if (change == "terrain") plot.Terrain = "world:desert"; else plot.Kind = PlotKind.Peak;
        var after = TerrainTestMaps.Snapshot(session);
        AssertDirtyChunks(before, after, target);
    }

    [Fact]
    public void Revealing_a_tile_dirties_its_chunks()
    {
        var session = TerrainTestMaps.RevealedSession(mods.Content);
        var target = HexGrid.FromOffset(12, 4);
        var plot = session.Sim.State.PlotAt(target);
        plot.Kind = PlotKind.Hills;
        plot.Revealed = new TeamMask();
        plot.Visible = new TeamMask();
        var hidden = TerrainTestMaps.Snapshot(session);
        plot.Revealed.Add(session.Me.TeamId);
        var withoutMemory = TerrainTestMaps.Snapshot(session);
        int tile = session.Sim.State.Grid.TileAt(target).Value;
        Assert.Equal(ReliefClass.Hidden, withoutMemory.Relief[tile]);
        Assert.Equal(hidden.Relief, withoutMemory.Relief);
        session.Sim.Content.Services.Get<IVisibilityService>().Single()
            .RevealPlot(session.Sim, new TileId(tile), session.Me.TeamId);
        var revealed = TerrainTestMaps.Snapshot(session);
        Assert.Equal(ReliefClass.Hills, revealed.Relief[tile]);
        AssertDirtyChunks(hidden, revealed, target);
    }

    /// <summary>Dirty chunks are exactly what a renderer may skip rebuilding: every chunk whose mesh changed must be dirty,
    /// the chunk under the tile must be dirty, and no chunk may be dirty unless it reaches the tile's ring of neighbours
    /// (within √3 + 1 of its centre). Wrap images of the tile count, so a change at column 0 may dirty the east chunks.</summary>
    private static void AssertDirtyChunks(TerrainSnapshot before, TerrainSnapshot after, Hex target)
    {
        var chunks = new TerrainChunks(before.Grid.Spec);
        var dirty = TerrainChunks.DirtyChunks(chunks.Signatures(before), chunks.Signatures(after)).ToHashSet();
        var (cx, cz) = target.ToPixel(1);
        Assert.Contains(chunks.Chunks.Single(c => c.Contains(cx, cz)).Id, dirty);
        double period = before.Period;
        foreach (int id in dirty)
        {
            var c = chunks.Chunks[id];
            double distance = new[] { cx - period, cx, cx + period }.Min(x =>
            {
                double dx = Math.Max(Math.Max(c.X0 - x, x - c.X1), 0), dz = Math.Max(Math.Max(c.Z0 - cz, cz - c.Z1), 0);
                return Math.Sqrt(dx * dx + dz * dz);
            });
            Assert.True(distance <= Sqrt3 + 1, $"chunk {id} is {distance:F2} from the tile, beyond its neighbours");
        }
        var oldField = new TerrainField(before, 2);
        var newField = oldField.With(after);
        foreach (var chunk in chunks.Chunks)
        {
            var a = TerrainMesher.Build(chunk, oldField, chunks.Resolution(chunk, before));
            var b = TerrainMesher.Build(chunk, newField, chunks.Resolution(chunk, after));
            if (!a.Positions.SequenceEqual(b.Positions) || !a.Normals.SequenceEqual(b.Normals))
                Assert.Contains(chunk.Id, dirty);
        }
    }

    [Fact]
    public void A_ray_from_the_game_camera_at_a_peak_slope_picks_the_peak_not_the_tile_behind()
    {
        var snapshot = TerrainTestMaps.Uniform(ReliefClass.Flat);
        var peak = HexGrid.FromOffset(6, 6);
        snapshot.Relief[snapshot.ClampedTile(peak)] = ReliefClass.Peak;
        var field = new TerrainField(snapshot, 5);
        double pitch = 62 * Math.PI / 180; // CameraRig pitch: looking north and down
        var direction = new Vector3(0, (float)-Math.Sin(pitch), (float)-Math.Cos(pitch));
        var (px, pz) = peak.ToPixel(1);
        int checkedRays = 0;
        for (double ox = -0.6; ox <= 0.6; ox += 0.1)
            for (double oz = -0.6; oz <= 0.6; oz += 0.1)
            {
                double x = px + ox, z = pz + oz;
                var aim = new Vector3((float)x, (float)field.HeightAt(x, z), (float)z);
                var origin = aim - direction * 20;
                if (!FirstHitIsAt(field, origin, direction, aim)) continue; // hidden behind another part of the peak
                var plane = origin - direction * (origin.Y / direction.Y);
                if (Hex.FromPixel(plane.X, plane.Z, 1) == peak) continue; // a flat pick would get this one right anyway
                var hit = TerrainRay.Pick(origin, direction, field);
                Assert.NotNull(hit);
                Assert.Equal(peak, Hex.FromPixel(hit.Value.X, hit.Value.Z, 1));
                checkedRays++;
            }
        Assert.True(checkedRays >= 5, $"only {checkedRays} rays exercised the slope");
    }

    /// <summary>Independent fine-step check that the ray stays above the terrain until it reaches the aim point.</summary>
    private static bool FirstHitIsAt(TerrainField field, Vector3 origin, Vector3 direction, Vector3 aim)
    {
        float length = Vector3.Distance(origin, aim);
        for (float t = (origin.Y - (float)field.MaxHeight) / -direction.Y; t < length - 0.01f; t += 0.002f)
        {
            var p = origin + direction * t;
            if (p.Y <= field.HeightAt(p.X, p.Z)) return false;
        }
        return true;
    }
}
