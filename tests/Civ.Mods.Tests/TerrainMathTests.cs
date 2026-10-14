using Civ.Client;
using Civ.Client.Terrain;
using Civ.Content;
using Civ.Game;
using Xunit;
using Schema = Civ.Content.Schema;

namespace Civ.Mods.Tests;

/// <summary>Snapshots and sessions shared by the terrain math and mesh tests.</summary>
internal static class TerrainTestMaps
{
    public static readonly double Sqrt3 = Math.Sqrt(3);

    public static (double X, double Z) Centre(Hex hex) => hex.ToPixel(1);

    /// <summary>A wrapping map of every relief class, with river edges on known tiles (mirrored like the game does).</summary>
    public static TerrainSnapshot Mixed(int width = 16, int height = 12, int seed = 5)
    {
        var s = new TerrainSnapshot(new MapSpec(width, height, true));
        var rng = new Random(seed);
        var classes = Enum.GetValues<ReliefClass>();
        for (int i = 0; i < s.Relief.Length; i++)
        {
            s.Relief[i] = classes[rng.Next(classes.Length)];
            s.Sight[i] = s.Relief[i] == ReliefClass.Hidden ? Sight.Hidden : Sight.Visible;
        }
        for (int i = 0; i < s.Relief.Length; i++)
            for (int d = 0; d < 3; d++)
                if (rng.Next(4) == 0 && s.Relief[i] != ReliefClass.Hidden) SetRiver(s, i, d);
        return s;
    }

    /// <summary>A wrapping map where every tile has one relief class.</summary>
    public static TerrainSnapshot Uniform(ReliefClass relief, int width = 16, int height = 12)
    {
        var s = new TerrainSnapshot(new MapSpec(width, height, true));
        Array.Fill(s.Relief, relief);
        Array.Fill(s.Sight, Sight.Visible);
        return s;
    }

    public static void SetRiver(TerrainSnapshot s, int tile, int direction)
    {
        s.Rivers[tile] |= (byte)(1 << direction);
        if (s.TryTile(s.Grid.HexOf(new TileId(tile)).Neighbor(direction), out int other))
            s.Rivers[other] |= (byte)(1 << ((direction + 3) % 6));
    }

    /// <summary>A small all-grass, fully revealed and visible game on a wrapping map.</summary>
    public static GameSession RevealedSession(ContentDb content, int width = 24, int height = 16)
    {
        var sim = GameFactory.Create(content);
        sim.State.Map = new(width, height, true);
        sim.State.Plots = Enumerable.Range(0, width * height).Select(_ => new Plot { Terrain = "world:grass" }).ToArray();
        sim.State.Units.Clear();
        sim.State.Cities.Clear();
        foreach (var player in sim.State.Players) player.StartingPlot = null;
        sim.State.RebuildIndexes();
        var session = new GameSession(sim);
        foreach (var plot in sim.State.Plots) { plot.Revealed.Add(session.Me.TeamId); plot.Visible.Add(session.Me.TeamId); }
        return session;
    }

    /// <summary>Palette stand-in: a stable index per terrain id, and per feature id (0 = none).</summary>
    public static TerrainSnapshot Snapshot(GameSession session)
    {
        var terrains = session.Sim.Content.Get<Schema.TerrainDef>().All.Select(t => t.Id).Order(StringComparer.Ordinal).ToList();
        var features = session.Sim.Content.Get<Schema.FeatureDef>().All.Select(f => f.Id).Order(StringComparer.Ordinal).ToList();
        return TerrainSnapshot.From(session, t => terrains.IndexOf(t), f => f == null ? 0 : features.IndexOf(f) + 1);
    }
}

/// <summary>Hex blend weights and the terrain height field: the continuity, wrap, fog and determinism contracts that the
/// mesher, the shader and scatter all rely on.</summary>
public sealed class TerrainMathTests(LoadedMods mods) : IClassFixture<LoadedMods>
{
    private static readonly double Sqrt3 = TerrainTestMaps.Sqrt3;

    [Fact]
    public void Blend_weights_sum_to_one_and_stay_in_the_unit_interval()
    {
        var rng = new Random(1);
        for (int i = 0; i < 2000; i++)
        {
            var tri = HexBlend.CornerTriangle(rng.NextDouble() * 40 - 20, rng.NextDouble() * 40 - 20);
            Assert.Equal(1, tri.Lambda.Sum, 9);
            foreach (double s in new[] { 1.0, HexBlend.MaterialSharpness, HexBlend.PeakSharpness })
            {
                var w = HexBlend.Weights(tri.Lambda, s);
                Assert.Equal(1, w.Sum, 9);
                for (int k = 0; k < 3; k++) Assert.InRange(w[k], 0, 1);
            }
        }
    }

    [Fact]
    public void Blend_weight_is_one_at_a_tile_centre_half_at_an_edge_midpoint_and_a_third_at_a_corner()
    {
        var hex = new Hex(3, -2);
        var (cx, cz) = TerrainTestMaps.Centre(hex);
        foreach (double s in new[] { HexBlend.MaterialSharpness, HexBlend.PeakSharpness })
        {
            Assert.Equal(1, WeightOf(hex, cx, cz, s), 9);
            for (int d = 0; d < 6; d++)
            {
                var n = hex.Neighbor(d);
                var next = hex.Neighbor(d + 1);
                var (nx, nz) = TerrainTestMaps.Centre(n);
                var (mx, mz) = TerrainTestMaps.Centre(next);
                Assert.Equal(0.5, WeightOf(hex, (cx + nx) / 2, (cz + nz) / 2, s), 9);
                Assert.Equal(0.5, WeightOf(n, (cx + nx) / 2, (cz + nz) / 2, s), 9);
                double kx = (cx + nx + mx) / 3, kz = (cz + nz + mz) / 3;
                Assert.Equal(1 / 3.0, WeightOf(hex, kx, kz, s), 9);
                Assert.Equal(1 / 3.0, WeightOf(n, kx, kz, s), 9);
                Assert.Equal(1 / 3.0, WeightOf(next, kx, kz, s), 9);
            }
        }
    }

    private static double WeightOf(Hex hex, double x, double z, double sharpness)
    {
        var tri = HexBlend.CornerTriangle(x, z);
        var w = HexBlend.Weights(tri.Lambda, sharpness);
        return Enumerable.Range(0, 3).Where(k => tri[k] == hex).Sum(k => w[k]);
    }

    [Theory]
    [InlineData(0.0)]
    [InlineData(0.3)]
    public void Height_is_continuous_across_tile_edges_and_corners(double warp)
    {
        var field = new TerrainField(TerrainTestMaps.Mixed(), 11, new TerrainTuning { WarpAmplitude = warp });
        const double step = 2e-4;
        // Lines through centres, edge midpoints and corners in several directions cross every kind of border many times.
        var lines = new (double X, double Z, double Dx, double Dz)[]
        {
            (0.1, 3.0, 1, 0), (2.0, 0.0, 0.5, Sqrt3 / 2), (0.0, 1.5, Sqrt3 / 2, 0.5), (Sqrt3 / 2, 0.0, 0, 1), (5.0, 13.5, 0.8, -0.6),
        };
        double worst = 0;
        foreach (var (x0, z0, dx, dz) in lines)
        {
            double previous = field.HeightAt(x0, z0);
            for (int i = 1; i <= 60_000; i++)
            {
                double h = field.HeightAt(x0 + dx * i * step, z0 + dz * i * step);
                worst = Math.Max(worst, Math.Abs(h - previous));
                previous = h;
            }
        }
        // The steepest ridged slopes change by well under 0.01 per 2e-4 step; a blend jump between classes is ~0.1 or more.
        Assert.InRange(worst, 0, 0.01);
    }

    [Fact]
    public void Height_blends_profiles_convexly_and_stays_within_the_field_bounds()
    {
        // Noise-free hills and peaks: a weight sum other than 1 would dip below the hills level or overshoot the peaks.
        var snapshot = new TerrainSnapshot(new MapSpec(16, 12, true));
        var rng = new Random(6);
        for (int i = 0; i < snapshot.Relief.Length; i++) snapshot.Relief[i] = rng.Next(2) == 0 ? ReliefClass.Hills : ReliefClass.Peak;
        var tuning = new TerrainTuning { HillsNoise = 0, PeakNoise = 0, PeakRise = 0 };
        var plain = new TerrainField(snapshot, 1, tuning);
        var mixed = new TerrainField(TerrainTestMaps.Mixed(), 1);
        for (int i = 0; i < 5000; i++)
        {
            double x = rng.NextDouble() * plain.Period, z = rng.NextDouble() * 20 - 2;
            Assert.InRange(plain.HeightAt(x, z), tuning.HillsBase - 1e-12, tuning.PeakBase + 1e-12);
            Assert.InRange(mixed.HeightAt(x, z), mixed.MinHeight, mixed.MaxHeight);
        }
    }

    [Fact]
    public void Height_repeats_every_period()
    {
        var field = new TerrainField(TerrainTestMaps.Mixed(), 3);
        var rng = new Random(2);
        for (int i = 0; i < 500; i++)
        {
            double x = rng.NextDouble() * field.Period, z = rng.NextDouble() * 22 - 2;
            Assert.Equal(field.HeightAt(x, z), field.HeightAt(x + field.Period, z), 6);
            Assert.Equal(field.HeightAt(x, z), field.HeightAt(x - 2 * field.Period, z), 6);
        }
    }

    [Fact]
    public void Adjacent_peaks_keep_their_shared_edge_above_the_hills_level()
    {
        var snapshot = TerrainTestMaps.Uniform(ReliefClass.Flat);
        var pairs = new List<(Hex A, Hex B)>();
        for (int row = 1; row < snapshot.Height - 1; row += 3)
            for (int col = 0; col < snapshot.Width; col += 4)
            {
                var a = HexGrid.FromOffset(col, row);
                var b = a.Neighbor(col / 4 % 6);
                snapshot.Relief[snapshot.ClampedTile(a)] = ReliefClass.Peak;
                snapshot.Relief[snapshot.ClampedTile(b)] = ReliefClass.Peak;
                pairs.Add((a, b));
            }
        var tuning = new TerrainTuning();
        foreach (int seed in new[] { 1, 2, 3 })
        {
            var field = new TerrainField(snapshot, seed, tuning);
            foreach (var (a, b) in pairs)
            {
                var (ax, az) = TerrainTestMaps.Centre(a);
                var (bx, bz) = TerrainTestMaps.Centre(b);
                Assert.True(field.HeightAt((ax + bx) / 2, (az + bz) / 2) > tuning.HillsBase + tuning.HillsNoise);
            }
        }
    }

    [Fact]
    public void A_hidden_tile_is_flat_and_its_true_relief_and_rivers_do_not_change_any_height()
    {
        var target = HexGrid.FromOffset(8, 6);
        TerrainSnapshot Hidden(bool peak)
        {
            var session = TerrainTestMaps.RevealedSession(mods.Content);
            var state = session.Sim.State;
            foreach (var hex in new[] { target, target.Neighbor(0) })
            {
                var plot = state.PlotAt(hex);
                plot.Revealed = new TeamMask();
                plot.Visible = new TeamMask();
            }
            if (peak)
            {
                var plot = state.PlotAt(target);
                plot.Kind = PlotKind.Peak;
                plot.Terrain = "world:snow";
                plot.Feature = "world:forest";
                plot.Owner = 1;
                state.SetRiver(state.Grid.TileAt(target), 0, true); // between the two hidden tiles
            }
            return TerrainTestMaps.Snapshot(session);
        }
        var flat = new TerrainField(Hidden(peak: false), 4);
        var peak = new TerrainField(Hidden(peak: true), 4);
        int tile = peak.Snapshot.ClampedTile(target);
        Assert.Equal(ReliefClass.Hidden, peak.Snapshot.Relief[tile]);
        Assert.Equal((0, 0, 0, 0), (peak.Snapshot.Layer[tile], peak.Snapshot.FeatureGround[tile], peak.Snapshot.Owner[tile], peak.Snapshot.Rivers[tile]));

        var (cx, cz) = TerrainTestMaps.Centre(target);
        Assert.InRange(peak.HeightAt(cx, cz), -0.02, 0.02);
        var rng = new Random(9);
        for (int i = 0; i < 400; i++)
        {
            double x = cx + rng.NextDouble() * 6 - 3, z = cz + rng.NextDouble() * 6 - 3;
            Assert.Equal(flat.HeightAt(x, z), peak.HeightAt(x, z));
        }
    }

    [Fact]
    public void The_same_seed_gives_the_same_heights()
    {
        var snapshot = TerrainTestMaps.Mixed();
        var a = new TerrainField(snapshot, 21);
        var b = new TerrainField(TerrainTestMaps.Mixed(), 21);
        var other = new TerrainField(snapshot, 22);
        var rng = new Random(4);
        int differs = 0;
        for (int i = 0; i < 300; i++)
        {
            double x = rng.NextDouble() * a.Period, z = rng.NextDouble() * 18;
            Assert.Equal(a.HeightAt(x, z), b.HeightAt(x, z));
            if (a.HeightAt(x, z) != other.HeightAt(x, z)) differs++;
        }
        Assert.True(differs > 250);
    }
}
