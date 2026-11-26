using Ariel.Client;
using Ariel.Client.Terrain;
using Ariel.Game;
using Xunit;
using Xunit.Sdk;

namespace Ariel.Mods.Tests;

/// <summary>A river that reaches a cliff pours over it as a waterfall at one hex corner, over a ledge rock that sits on the
/// rock wall, and a river along a cliff edge runs on at the foot. These tests pin that geometry.</summary>
/// <remarks>Each theory builds one arrangement per layout and runs a group of checks on it, reporting every check that
/// fails. Every check runs in every turn, on the wrap seam and at both river widths: direction tables, the seam and
/// width-relative sizes change what it sees. Moving the strip to another tile (the chunk-line placements) changes which
/// chunk owns and meshes each piece of water, and the tile hashes that place the wall's columns and wiggle the river's
/// course. So checks that mesh the water or measure clearance from the columns (<see cref="Every"/>) also run in those
/// placements; checks on the river's course, the ground and the ledge alone (<see cref="FieldLayouts"/>) do not, since
/// another draw of the course's wiggle tests nothing new.</remarks>
public sealed class RiverFallTests
{
    // The strip's tiles, in its own frame (see Layout): the raised tile the river falls from, its raised neighbour to the
    // east the river comes down beside, and the low tiles to its south-east and south-west.
    private static readonly Hex High = new(0, 0), East = new(1, 0), Below = new(0, 1), West = new(-1, 1);

    /// <summary>One way to lay the strip on a <see cref="Size"/> × <see cref="Size"/> map: mirrored across its own line
    /// when <see cref="Mirror"/>, then turned <see cref="Turns"/> sixths of a circle anticlockwise (east to north-east),
    /// with its high tile at offset (<see cref="Col"/>, <see cref="Row"/>), on a map that wraps when <see cref="Wrap"/>,
    /// with rivers <see cref="RiverWidth"/> wide. Every direction and both ends of a curve reach the fall in one of the
    /// turns, and the placements put the fall on chunk lines and on the wrap seam.</summary>
    public sealed record Layout(string Name, int Turns = 0, bool Mirror = false, int Col = 10, int Row = 10, bool Wrap = false,
        double RiverWidth = 0.16, bool ChunkLine = false)
    {
        public const int Size = 16;

        private Hex Origin => HexGrid.FromOffset(Col, Row);

        /// <summary>Where a hex of the strip's frame lies on the map, unwrapped: near the high tile.</summary>
        public Hex Place(Hex h)
        {
            if (Mirror) h = new(h.Q + h.R, -h.R);
            for (int i = 0; i < Turns; i++) h = new(h.Q + h.R, -h.Q);
            return Origin + h;
        }

        /// <summary>The hex of the strip's frame a map hex lies at; on a wrapping map, its nearest copy.</summary>
        public Hex Unplace(Hex hex)
        {
            var h = hex - Origin;
            if (Wrap)
                h = new[] { h, new Hex(h.Q - Size, h.R), new Hex(h.Q + Size, h.R) }.MinBy(c => c.DistanceTo(new Hex(0, 0)));
            for (int i = 0; i < Turns; i++) h = new(-h.R, h.Q + h.R);
            return Mirror ? new(h.Q + h.R, -h.R) : h;
        }

        /// <summary>The map direction a direction of the strip's frame points in.</summary>
        public int Direction(int d) => ((Mirror ? 6 - d : d) + Turns) % 6;

        public override string ToString() => Name;
    }

    private static readonly Layout[] Layouts =
    [
        .. Enumerable.Range(0, 6).Select(t => new Layout($"turned {t}", t)),
        .. Enumerable.Range(0, 6).Select(t => new Layout($"mirrored, turned {t}", t, Mirror: true)),
        // Every column of a chunk, on and off a chunk row's line (row 10's centres lie on one).
        .. from col in Enumerable.Range(8, 4) from row in new[] { 9, 10 } where (col, row) != (10, 10)
           select new Layout($"at {col},{row}", Col: col, Row: row, ChunkLine: true),
        // The fall's corner on the seam, and on either side of it.
        new("across the seam at 15,9", Col: 15, Row: 9, Wrap: true),
        new("across the seam at 0,10", Col: 0, Row: 10, Wrap: true),
        new("across the seam at 15,10", Col: 15, Row: 10, Wrap: true),
        new("across the seam, turned 3", 3, Col: 0, Row: 10, Wrap: true),
        new("across the seam, mirrored, turned 2", 2, Mirror: true, Col: 15, Row: 9, Wrap: true),
        new("a narrow river", RiverWidth: 0.05),
        new("a wide river", RiverWidth: 0.3),
    ];

    /// <summary>Every layout, for checks on the meshes the chunks build or on the wall's columns.</summary>
    public static TheoryData<string> Every => new(Layouts.Select(l => l.Name));

    /// <summary>Every layout but the chunk-line placements, for checks on the river's course, the ground and the ledge.</summary>
    public static TheoryData<string> FieldLayouts => new(Layouts.Where(l => !l.ChunkLine).Select(l => l.Name));

    /// <summary>A strip of seven raised tiles through the high tile, along its frame's east-west line, with a wall on their
    /// south-west and south-east sides (or the tiles <paramref name="raised"/> lists, walled the same way), laid on the map
    /// as <paramref name="layout"/> says. With <paramref name="river"/>, a
    /// river comes down between the high tile and its east neighbour, runs along the wall edge of the high tile and on
    /// between the low tiles below it. With <paramref name="sea"/>, the low side is sea instead and the river ends at the
    /// wall. <paramref name="rivers"/>, when given, replaces that river with these edges (a tile and a direction, in the
    /// strip's frame).</summary>
    private static Scene Strip(string layout = "turned 0", bool cliff = true, bool river = true,
        bool sea = false, (Hex Tile, int Direction)[]? rivers = null, Hex[]? raised = null)
    {
        var at = Layouts.Single(l => l.Name == layout);
        var s = new TerrainSnapshot(new MapSpec(Layout.Size, Layout.Size, at.Wrap));
        var grid = s.Grid;
        for (int i = 0; i < s.Relief.Length; i++)
        {
            s.Relief[i] = ReliefClass.Flat; s.Sight[i] = Sight.Visible;
            var h = at.Unplace(grid.HexOf(new TileId(i)));
            if (sea && h.R > 0) s.Relief[i] = ReliefClass.ShallowWater;
            if (raised != null ? !raised.Contains(h) : h.R != 0 || Math.Abs(h.Q) > 3) continue;
            s.Heights[i] = 1;
            if (!cliff) continue;
            foreach (int d in new[] { 4, 5 })
            {
                int dir = at.Direction(d), low = grid.Neighbor(new TileId(i), dir)!.Value.Value;
                s.Cliffs[i] |= (byte)(1 << dir); s.CliffLifts[i] |= (byte)(1 << dir);
                s.Cliffs[low] |= (byte)(1 << (dir + 3) % 6);
            }
        }
        rivers ??= !river ? [] : sea ? [(High, 0), (High, 5)] : [(High, 0), (High, 5), (Below, 3)];
        foreach (var (tile, d) in rivers)
        {
            int dir = at.Direction(d), t = grid.TileAt(at.Place(tile)).Value;
            s.Rivers[t] |= (byte)(1 << dir);
            s.Rivers[grid.Neighbor(new TileId(t), dir)!.Value.Value] |= (byte)(1 << (dir + 3) % 6);
        }
        return new Scene(new TerrainField(s, 3, new TerrainTuning { RiverWidth = at.RiverWidth }), at);
    }

    /// <summary>A strip laid out, with the meshes of the chunks within reach of its fall, each built once on first use.</summary>
    private sealed class Scene(TerrainField terrain, Layout layout)
    {
        private RiverMeshData[]? _riverParts;
        private RiverMeshData? _rivers;
        private WaterfallMeshData[]? _falls;

        public TerrainField Field => terrain;
        public Layout At => layout;

        /// <summary>Each chunk's river mesh.</summary>
        public RiverMeshData[] RiverParts =>
            _riverParts ??= [.. Chunks(terrain).Select(c => RiverMesher.Build(c, terrain)).OfType<RiverMeshData>()];

        /// <summary>The chunks' river meshes run together. Only its vertex attributes are meaningful: the indices of each
        /// part still count from that part's first vertex.</summary>
        public RiverMeshData Rivers => _rivers ??= new RiverMeshData(
            [.. RiverParts.SelectMany(p => p.Positions)], [.. RiverParts.SelectMany(p => p.Normals)],
            [.. RiverParts.SelectMany(p => p.Lanes)], [.. RiverParts.SelectMany(p => p.Flow)],
            [.. RiverParts.SelectMany(p => p.Info)], [.. RiverParts.SelectMany(p => p.Rock)], [.. RiverParts.SelectMany(p => p.Depth)],
            [.. RiverParts.SelectMany(p => p.Indices)]);

        /// <summary>Each chunk's waterfall mesh.</summary>
        public WaterfallMeshData[] Falls =>
            _falls ??= [.. Chunks(terrain).Select(c => WaterfallMesher.Build(c, terrain)).OfType<WaterfallMeshData>()];

        public void Deconstruct(out TerrainField f, out Layout a) => (f, a) = (terrain, layout);
    }

    /// <summary>Runs every check on the scene and fails with all the checks that failed, by name.</summary>
    private static void Check(Scene scene, params Action<Scene>[] checks)
    {
        var failures = new List<string>();
        foreach (var check in checks)
        {
            try { check(scene); }
            catch (XunitException e) { failures.Add($"{check.Method.Name}: {e.Message}"); }
        }
        if (failures.Count > 0) throw new XunitException(string.Join(Environment.NewLine, failures));
    }

    /// <summary>The hex corner three tiles of the strip share: the mean of their centres, unwrapped.</summary>
    private static (double X, double Z) Corner(Layout at, params Hex[] tiles)
    {
        double x = 0, z = 0;
        foreach (var t in tiles)
        {
            var (px, pz) = at.Place(t).ToPixel(1);
            x += px / tiles.Length; z += pz / tiles.Length;
        }
        return (x, z);
    }

    /// <summary>An x difference between two points, taken the short way round a wrapping map.</summary>
    private static double Dx(TerrainField field, double dx) =>
        field.Snapshot.Grid.Spec.Wrap ? dx - field.Snapshot.Period * Math.Round(dx / field.Snapshot.Period) : dx;

    /// <summary>The chunks within reach of a fall, or all of them when there is none.</summary>
    private static IEnumerable<TerrainChunk> Chunks(TerrainField field)
    {
        var chunks = new TerrainChunks(field.Snapshot.Grid.Spec, field.Tuning).Chunks;
        var falls = field.Rivers.Falls;
        if (!falls.Any()) return chunks;
        return chunks.Where(c => falls.Any(f =>
            Math.Abs(Dx(field, f.X - (c.X0 + c.X1) / 2)) < (c.X1 - c.X0) / 2 + 3 &&
            Math.Abs(f.Z - (c.Z0 + c.Z1) / 2) < (c.Z1 - c.Z0) / 2 + 3));
    }

    // A river comes down between two raised tiles, falls over the wall at the corner they share and runs on at the foot.

    [Theory, MemberData(nameof(FieldLayouts))]
    public void A_river_along_a_cliff_falls_at_its_corner_and_runs_on_along_the_foot(string layout) => Check(Strip(layout),
        FallsAtTheCornerBetweenTheRaisedTiles, GathersToTheWidthOfTheLipBeforeItFalls,
        StartsAtTheFootUnderItsFallNotOnTheBrinkAboveIt, RunsInShortStepsWithNoJumpBetweenSamples,
        ReachesItsFallRunningStraightOutOverTheLipWithoutAKink, RunsStraightAlongTheLipUnderItsFallInsteadOfTurningIntoTheCorner,
        MeetsTheRiverItRunsOnIntoAtOnePointWithOneDirection, RunsOntoItsFallLevelWithItsBanks,
        LedgeStandsNoLowerThanThePlateauBehindAnyPartOfItsLip);

    [Theory, MemberData(nameof(Every))]
    public void The_water_of_a_river_along_a_cliff_runs_among_the_columns_and_is_meshed_over_and_under_its_fall(string layout) =>
        Check(Strip(layout), RunsAgainstTheFaceAfterItsFallAndTheCliffKeepsItsStep,
        RunsOnUnderTheLedgeAndTheTongueLiesOverItWithNoGroundBetween, SoftensInAtTheStartOfTheFootRiverInsteadOfEndingInACut,
        NoGroundShowsThroughTheWaterRunningOntoAFall, ChurnsAgainstTheRockColumnsAndNowhereElse,
        SheetIsAsNarrowAsItsLipAndFallsFromTheLedgeToTheFoot, PoolStaysInTheChannelOfTheRiverAlongTheFoot,
        LiesLevelFromWaterlineToWaterline, GroundUnderTheRiverAlongTheFootStandsNoHigherThanItsWater);

    private static void FallsAtTheCornerBetweenTheRaisedTiles(Scene scene)
    {
        var (field, at) = scene;
        var curve = Assert.Single(field.Rivers.Curves, c => c.Fall != null);
        var fall = curve.Fall!;
        Assert.True(fall.AlongFoot);
        var (cx, cz) = Corner(at, High, East, Below);
        Assert.True(Math.Abs(Dx(field, fall.X - cx)) < 1e-6 && Math.Abs(fall.Z - cz) < 1e-6, "the fall is not at the corner the water arrives at");
        var (lx, lz) = at.Place(Below).ToPixel(1);
        Assert.True(fall.DirectionX * (lx - cx) + fall.DirectionZ * (lz - cz) > 0, "the fall does not drop towards the low tile");
    }

    private static void RunsAgainstTheFaceAfterItsFallAndTheCliffKeepsItsStep(Scene scene)
    {
        var field = scene.Field;
        var curve = field.Rivers.Curves.Single(c => c.Fall != null);
        // The middle of the edge's curve lies just out from the drawn face, on the line the wall's columns stand on.
        int mid = curve.Count / 2;
        var (fx, fz, nx, nz) = field.DrawPoint(curve.X[mid], curve.Z[mid], TerrainField.FaceSnap);
        Assert.True(nx != 0 || nz != 0, "no face beside the river");
        double out_ = (curve.X[mid] - fx) * nx + (curve.Z[mid] - fz) * nz;
        Assert.True(out_ > 0 && out_ < 0.2, $"the river runs {out_:F2} out from the face, not against it");
        // The ground still steps at the face beside it: the trough does not cut into the brink.
        double drop = field.HeightAt(fx - nx * 0.2, fz - nz * 0.2) - field.HeightAt(fx + nx * 0.12, fz + nz * 0.12);
        Assert.True(drop > 0.8 * field.Tuning.CliffRise, $"the cliff beside the river drops only {drop:F2}");
        // The wall's columns there stand with their feet in the channel, below the water.
        double water = field.HeightAt(curve.X[mid], curve.Z[mid]) + field.Rivers.Carve(curve.X[mid], curve.Z[mid]);
        var near = field.CliffRocks(fx - 0.3, fx + 0.3, fz - 0.3, fz + 0.3)
            .Where(r => Dx(field, r.X - fx) * Dx(field, r.X - fx) + (r.Z - fz) * (r.Z - fz) < 0.15 * 0.15).ToArray();
        Assert.NotEmpty(near);
        Assert.All(near, r => Assert.True(r.Y < water, $"a column stands at {r.Y:F3}, above the water at {water:F3}"));
    }

    private static void GathersToTheWidthOfTheLipBeforeItFalls(Scene scene)
    {
        var field = scene.Field;
        var fall = field.Rivers.Falls.Single();
        var upstream = field.Rivers.Curves.Where(c => c.Fall == null).SelectMany(c => new[]
        {
            (c.X[0], c.Z[0], c.Width[0]), (c.X[^1], c.Z[^1], c.Width[^1]),
        }).Where(e => Math.Abs(Dx(field, e.Item1 - fall.X)) < 1e-6 && Math.Abs(e.Item2 - fall.Z) < 1e-6).ToArray();
        Assert.NotEmpty(upstream);
        Assert.All(upstream, e => Assert.True(e.Item3 <= fall.HalfWidth + 1e-9,
            $"water {e.Item3:F2} wide reaches a lip {fall.HalfWidth:F2} wide"));
    }

    private static void RunsOnUnderTheLedgeAndTheTongueLiesOverItWithNoGroundBetween(Scene scene)
    {
        var field = scene.Field;
        var fall = field.Rivers.Falls.Single();
        var ledge = field.LedgeOf(fall);
        double Along(double x, double z) => Dx(field, x - ledge.X) * ledge.DirectionX + (z - ledge.Z) * ledge.DirectionZ;
        double Side(double x, double z) => Math.Abs(-Dx(field, x - ledge.X) * ledge.DirectionZ + (z - ledge.Z) * ledge.DirectionX);
        double high = ledge.Foot + 0.5 * (ledge.Y - ledge.Foot);

        // The river's water runs on, unfaded, to the back of the ledge rock...
        var river = scene.Rivers;
        double reach = double.MinValue;
        for (int v = 0; v < river.VertexCount; v++)
        {
            double x = river.Positions[v * 3], y = river.Positions[v * 3 + 1], z = river.Positions[v * 3 + 2];
            if (y < high || Side(x, z) > fall.HalfWidth || river.Info[v * 2] < 0.99) continue;
            reach = Math.Max(reach, Along(x, z));
        }
        Assert.True(reach >= -ledge.BackIn, $"the river stops {-ledge.BackIn - reach:F3} short of the ledge rock");

        // ...where the tongue already lies on top of it, fully faded in.
        var falls = Assert.Single(scene.Falls);
        double tongue = double.MaxValue;
        for (int v = 0; v < falls.WaterVertexCount; v++)
            if (falls.WaterInfo[v * 4] == WaterfallMesher.Lip && falls.WaterInfo[v * 4 + 3] > 0.99)
                tongue = Math.Min(tongue, Along(falls.WaterPositions[v * 3], falls.WaterPositions[v * 3 + 2]));
        Assert.True(tongue <= reach, $"the tongue starts {tongue - reach:F3} past the end of the river");
    }

    private static void StartsAtTheFootUnderItsFallNotOnTheBrinkAboveIt(Scene scene)
    {
        var field = scene.Field;
        var fall = field.Rivers.Falls.Single();
        var ledge = field.LedgeOf(fall);
        var foot = field.Rivers.Curves.Single(c => c.Fall == fall);
        double half = ledge.Foot + 0.5 * (ledge.Y - ledge.Foot);
        int start = fall.AtStart ? 0 : foot.Count - 1, far = foot.Count - 1 - start;
        double Side(int i) => -Dx(field, foot.X[i] - ledge.X) * ledge.DirectionZ + (foot.Z[i] - ledge.Z) * ledge.DirectionX;
        double downstream = Math.Sign(Side(far));
        // From under the middle of the sheet on, the river runs at the foot. (Back from there it may run in under the
        // brink of a raised tile beside the fall, which hides it.)
        for (int i = 0; i < foot.Count; i++)
            if (Side(i) * downstream >= 0)
                Assert.True(field.HeightAt(foot.X[i], foot.Z[i]) < half, $"sample {i} of the foot river stands on the brink");
        double side = Math.Abs(Side(start));
        // It starts at the far side of the sheet, so its water lies under all of it.
        Assert.True(Side(start) * downstream <= -fall.HalfWidth + 1e-9, $"the foot river starts {side:F3} along the wall from its fall, under its sheet");
    }

    private static void RunsInShortStepsWithNoJumpBetweenSamples(Scene scene)
    {
        foreach (var c in scene.Field.Rivers.Curves)
            for (int i = 1; i < c.Count; i++)
            {
                double dx = c.X[i] - c.X[i - 1], dz = c.Z[i] - c.Z[i - 1], step = Math.Sqrt(dx * dx + dz * dz);
                Assert.True(step < 2 * RiverNetwork.SampleStep, $"a river jumps {step:F2} between samples {i - 1} and {i}");
            }
    }

    private static void ReachesItsFallRunningStraightOutOverTheLipWithoutAKink(Scene scene)
    {
        var field = scene.Field;
        var fall = field.Rivers.Falls.Single();
        var feed = field.Rivers.Curves.Single(c => c.Feeds == fall);
        int end = feed.FeedsAtStart ? 0 : feed.Count - 1, step = feed.FeedsAtStart ? 1 : -1;
        // Its last segment heads out over the lip, the way the water runs on onto the ledge.
        double dx = (feed.X[end] - feed.X[end + step]), dz = (feed.Z[end] - feed.Z[end + step]);
        double cos = (dx * fall.DirectionX + dz * fall.DirectionZ) / Math.Sqrt(dx * dx + dz * dz);
        Assert.True(cos > Math.Cos(Math.PI / 36), $"the river reaches its fall {Math.Acos(cos) * 180 / Math.PI:F0} degrees off the way it pours");
        // And it turns there gently: no two segments near the fall meet at a sharp angle.
        for (int i = end + step; Math.Abs(i - end) < feed.Count / 2; i += step)
        {
            double ax = feed.X[i] - feed.X[i + step], az = feed.Z[i] - feed.Z[i + step];
            double bx = feed.X[i - step] - feed.X[i], bz = feed.Z[i - step] - feed.Z[i];
            double turn = Math.Acos(Math.Clamp((ax * bx + az * bz) / Math.Sqrt((ax * ax + az * az) * (bx * bx + bz * bz)), -1, 1));
            Assert.True(turn < Math.PI / 12, $"the river turns {turn * 180 / Math.PI:F0} degrees at sample {i} by its fall");
        }
    }

    private static void RunsStraightAlongTheLipUnderItsFallInsteadOfTurningIntoTheCorner(Scene scene)
    {
        var field = scene.Field;
        var fall = field.Rivers.Falls.Single();
        var foot = field.Rivers.Curves.Single(c => c.Fall == fall);
        for (int i = 0; i < foot.Count; i++)
        {
            double along = Dx(field, foot.X[i] - fall.X) * -fall.DirectionZ + (foot.Z[i] - fall.Z) * fall.DirectionX;
            if (Math.Abs(along) > fall.HalfWidth) continue;
            // Under the sheet its course lies across the fall's direction, so the water the sheet lands in is square to it.
            double out_ = Math.Abs(foot.TX[i] * fall.DirectionX + foot.TZ[i] * fall.DirectionZ);
            Assert.True(out_ < Math.Sin(Math.PI / 18), $"sample {i} of the foot river turns {Math.Asin(out_) * 180 / Math.PI:F0} degrees off the lip under its fall");
        }
    }

    private static void MeetsTheRiverItRunsOnIntoAtOnePointWithOneDirection(Scene scene)
    {
        var field = scene.Field;
        var fall = field.Rivers.Falls.Single();
        var foot = field.Rivers.Curves.Single(c => c.Fall == fall);
        int node = fall.AtStart ? foot.Link.B : foot.Link.A, end = fall.AtStart ? foot.Count - 1 : 0;
        var next = field.Rivers.Curves.Where(c => c != foot && (c.Link.A == node || c.Link.B == node)).ToArray();
        Assert.NotEmpty(next);
        foreach (var c in next)
        {
            int e = c.Link.A == node ? 0 : c.Count - 1;
            double gx = Dx(field, c.X[e] - foot.X[end]), gz = c.Z[e] - foot.Z[end];
            double gap = Math.Sqrt(gx * gx + gz * gz);
            Assert.True(gap < 1e-9, $"the rivers meet {gap:F3} apart");
            double turn = Math.Abs(c.TX[e] * foot.TZ[end] - c.TZ[e] * foot.TX[end]);
            Assert.True(turn < 1e-9, $"the rivers meet at an angle ({turn:F3})");
        }
    }

    private static void SoftensInAtTheStartOfTheFootRiverInsteadOfEndingInACut(Scene scene)
    {
        var field = scene.Field;
        var fall = field.Rivers.Falls.Single();
        var foot = field.Rivers.Curves.Single(c => c.Fall == fall);
        int start = fall.AtStart ? 0 : foot.Count - 1;
        var river = scene.Rivers;
        int ends = 0;
        for (int v = 0; v < river.VertexCount; v++)
        {
            double x = river.Positions[v * 3], z = river.Positions[v * 3 + 2];
            // The lanes of the river's first section lie on the line across it through its start sample, each its share of
            // the width out from it (the river feeding the fall reaches that line too, under the lip, at other points).
            double dx = Dx(field, x - foot.X[start]), dz = z - foot.Z[start];
            if (Math.Abs(dx * foot.TX[start] + dz * foot.TZ[start]) > 1e-4) continue;
            double side = -dx * foot.TZ[start] + dz * foot.TX[start];
            if (Math.Abs(side - river.Lanes[v * 2] * foot.Width[start]) > 1e-4) continue;
            ends++;
            Assert.True(river.Info[v * 2] < 0.2, $"the foot river's start shows {river.Info[v * 2]:F2}, a cut across it");
        }
        Assert.True(ends > 0, "no section at the foot river's start");
    }

    private static void NoGroundShowsThroughTheWaterRunningOntoAFall(Scene scene)
    {
        var field = scene.Field;
        var fall = field.Rivers.Falls.Single();
        var ledge = field.LedgeOf(fall);
        var lattice = new TerrainLattice(field);
        foreach (var river in scene.RiverParts)
            for (int t = 0; t < river.Indices.Length; t += 3)
            {
                int a = river.Indices[t], b = river.Indices[t + 1], c = river.Indices[t + 2];
                // Water triangles on the plateau within reach of the brink, where the drawn ground rounds down. (A river
                // along the foot that runs in under the brink keeps to the foot's level, hidden by the raised ground.)
                if (new[] { a, b, c }.Any(v => Math.Abs(river.Lanes[v * 2]) > 1 || river.Info[v * 2] < 0.5 ||
                    river.Positions[v * 3 + 1] < ledge.Brim - 0.1)) continue;
                double mx = (river.Positions[a * 3] + river.Positions[b * 3] + river.Positions[c * 3]) / 3;
                double mz = (river.Positions[a * 3 + 2] + river.Positions[b * 3 + 2] + river.Positions[c * 3 + 2]) / 3;
                double behind = -(Dx(field, mx - ledge.X) * ledge.DirectionX + (mz - ledge.Z) * ledge.DirectionZ);
                if (behind < ledge.BackIn || behind > FallLedge.BrimReach) continue;
                if (Math.Abs(-Dx(field, mx - ledge.X) * ledge.DirectionZ + (mz - ledge.Z) * ledge.DirectionX) > 0.4) continue;
                // Inside the triangle the ground may not rise above the water by more than the lift the water is drawn with.
                foreach (var (u, w) in new[] { (1 / 3.0, 1 / 3.0), (0.5, 0.5), (0.5, 0), (0, 0.5), (0.2, 0.6), (0.6, 0.2), (0.2, 0.2) })
                {
                    double l = 1 - u - w;
                    double x = river.Positions[a * 3] * l + river.Positions[b * 3] * u + river.Positions[c * 3] * w;
                    double y = river.Positions[a * 3 + 1] * l + river.Positions[b * 3 + 1] * u + river.Positions[c * 3 + 1] * w;
                    double z = river.Positions[a * 3 + 2] * l + river.Positions[b * 3 + 2] * u + river.Positions[c * 3 + 2] * w;
                    double over = lattice.Sample(x, z).Height - y;
                    Assert.True(over < 0.015, $"the ground shows {over:F3} through the water {behind:F2} behind the brink");
                }
            }
    }

    private static void ChurnsAgainstTheRockColumnsAndNowhereElse(Scene scene)
    {
        var field = scene.Field;
        var river = scene.Rivers;
        var rocks = field.CliffRocks(0, field.Period, -3, 30).ToArray();
        int against = 0;
        for (int v = 0; v < river.VertexCount; v++)
        {
            if (Math.Abs(river.Lanes[v * 2]) > 1) continue;
            double x = river.Positions[v * 3], z = river.Positions[v * 3 + 2];
            double nearest = rocks.Min(r => Math.Sqrt(Dx(field, r.X - x) * Dx(field, r.X - x) + (r.Z - z) * (r.Z - z)));
            // Water standing among the columns churns; water well clear of every column does not.
            if (nearest < 0.05) { Assert.True(river.Rock[v] > 0.9, $"water {nearest:F2} from a column shows rock {river.Rock[v]:F2}"); against++; }
            if (nearest > 0.6) Assert.Equal(0f, river.Rock[v]);
        }
        Assert.True(against > 0, "no water stands among the wall's columns");
    }

    private static void SheetIsAsNarrowAsItsLipAndFallsFromTheLedgeToTheFoot(Scene scene)
    {
        var field = scene.Field;
        var fall = field.Rivers.Falls.Single();
        var ledge = field.LedgeOf(fall);
        var data = Assert.Single(scene.Falls);
        int sheet = 0;
        double lowest = double.MaxValue, highest = double.MinValue;
        for (int v = 0; v < data.WaterVertexCount; v++)
        {
            float part = data.WaterInfo[v * 4];
            if (part is not (WaterfallMesher.Lip or WaterfallMesher.Sheet)) continue;
            sheet++;
            double x = Dx(field, data.WaterPositions[v * 3] - fall.X), y = data.WaterPositions[v * 3 + 1], z = data.WaterPositions[v * 3 + 2] - fall.Z;
            double side = Math.Abs(-x * fall.DirectionZ + z * fall.DirectionX);
            // Mesh positions are single precision: a few millionths of a unit over is rounding, not overreach.
            Assert.True(side <= fall.HalfWidth + 1e-4, $"the sheet reaches {side:F4} along the cliff, past its lip at {fall.HalfWidth:F4}");
            lowest = Math.Min(lowest, y);
            highest = Math.Max(highest, y);
        }
        Assert.True(sheet > 0, "the fall has no sheet");
        Assert.True(highest >= ledge.Y, "the water does not run over the ledge's top");
        Assert.True(lowest < ledge.Foot + 0.03, $"the sheet stops at {lowest:F2}, above the foot at {ledge.Foot:F2}");
        Assert.NotEmpty(data.Mist.Indices);
    }

    private static void PoolStaysInTheChannelOfTheRiverAlongTheFoot(Scene scene)
    {
        var field = scene.Field;
        var fall = field.Rivers.Falls.Single();
        var (lx, lz) = field.LedgeOf(fall).Landing;
        var data = Assert.Single(scene.Falls);
        for (int v = 0; v < data.WaterVertexCount; v++)
        {
            if (data.WaterInfo[v * 4] != WaterfallMesher.Pool || data.WaterInfo[v * 4 + 3] == 0) continue;
            double x = data.WaterPositions[v * 3], z = data.WaterPositions[v * 3 + 2];
            Assert.True(field.Rivers.Near(x, z), $"the pool reaches ({x:F2}, {z:F2}), out of the river's channel");
        }
        Assert.True(field.Rivers.Near(lx, lz), "the sheet lands outside the river along the foot");
    }

    private static void RunsOntoItsFallLevelWithItsBanks(Scene scene)
    {
        var field = scene.Field;
        var fall = field.Rivers.Falls.Single();
        var ledge = field.LedgeOf(fall);
        // Just behind the ledge, on the plateau, the channel is barely cut into the ground.
        double x = fall.X - fall.DirectionX * (ledge.BackIn + 0.1), z = fall.Z - fall.DirectionZ * (ledge.BackIn + 0.1);
        double carve = field.Rivers.Carve(x, z);
        Assert.True(carve < 0.01, $"the river runs onto its fall {carve:F3} below its banks");
    }

    /// <summary>Where the brink runs slantwise across the lip, the ground behind the ledge's origin is already rounding
    /// down over the edge. A slab set by that ground alone sank below the plateau beside it, and the wall columns, capped
    /// under the slab, hid the sheet from the open water.</summary>
    private static void LedgeStandsNoLowerThanThePlateauBehindAnyPartOfItsLip(Scene scene)
    {
        var field = scene.Field;
        var fall = field.Rivers.Falls.Single();
        var ledge = field.LedgeOf(fall);
        for (int k = 0; k <= 8; k++)
        {
            double s = fall.HalfWidth * (k / 4.0 - 1);
            double x = fall.X - fall.DirectionZ * s - fall.DirectionX * 0.25, z = fall.Z + fall.DirectionX * s - fall.DirectionZ * 0.25;
            double ground = field.HeightAt(x, z);
            Assert.True(ground <= ledge.Y, $"the plateau {s:F2} across the lip stands {ground - ledge.Y:F3} above the ledge");
        }
    }

    /// <summary>Water is level across: a cross-section's water lanes all lie at one height, along the foot of a wall (with
    /// a fall or without one) as anywhere else. Where the ground rises above that level, it hides the water instead, but
    /// where the drawn ground stands only a hair above it the water lies on that ground, so no lane rises further.</summary>
    private static void LiesLevelFromWaterlineToWaterline(Scene scene)
    {
        var data = scene.Rivers;
        for (int v = 0; v < data.VertexCount;)
        {
            int end = v;
            double lo = double.MaxValue, hi = double.MinValue;
            for (; end < data.VertexCount && data.Lanes[end * 2 + 1] == data.Lanes[v * 2 + 1]; end++)
            {
                if (Math.Abs(data.Lanes[end * 2]) > 1) continue;
                double y = data.Positions[end * 3 + 1];
                (lo, hi) = (Math.Min(lo, y), Math.Max(hi, y));
            }
            Assert.True(hi - lo <= RiverMesher.WaterOnGround + 1e-4, $"the water across a section at ({data.Positions[v * 3]:F2}, {data.Positions[v * 3 + 2]:F2}) tilts by {hi - lo:F3}");
            v = end;
        }
    }

    /// <summary>The low side climbs towards a wall over an apron of cliff lift, which reaches well out near the end of a run
    /// of wall. Under the water of a river along the foot it is taken away, so that ground does not rise through the level
    /// water and pinch the river to a sliver against the rocks. Its outer half is checked: the inner half runs in under the
    /// wall's columns.</summary>
    private static void GroundUnderTheRiverAlongTheFootStandsNoHigherThanItsWater(Scene scene)
    {
        var field = scene.Field;
        var foot = field.Rivers.Curves.Single(c => c.Foot != null);
        var river = scene.Rivers;
        for (int v = 0; v < river.VertexCount; v++)
        {
            // The water inside its waterlines: on the waterlines it meets the ground, which may stand a little above
            // it on the higher shore where the river rounds a corner.
            double across = river.Lanes[v * 2];
            if (Math.Abs(across) >= 1) continue;
            double x = river.Positions[v * 3], y = river.Positions[v * 3 + 1], z = river.Positions[v * 3 + 2];
            // Only the foot river's own water, on its open side of the centre line.
            int i = Enumerable.Range(0, foot.Count).MinBy(k => Math.Pow(Dx(field, x - foot.X[k]), 2) + Math.Pow(z - foot.Z[k], 2));
            double dx = Dx(field, x - foot.X[i]), dz = z - foot.Z[i];
            double along = dx * foot.TX[i] + dz * foot.TZ[i];
            // Past either end lies the river it runs on into, beyond the end of the wall.
            if (Math.Abs(along) > RiverNetwork.SampleStep || i == 0 && along < -1e-6 || i == foot.Count - 1 && along > 1e-6) continue;
            double side = dx * foot.Foot!.DirectionX + dz * foot.Foot.DirectionZ;
            if (side < 0.25 * foot.Width[i] || side > 0.95 * foot.Width[i]) continue;
            double ground = field.HeightAt(x, z);
            Assert.True(ground <= y + 0.005, $"the ground stands {ground - y:F3} above the water at ({x:F2}, {z:F2}), {side / foot.Width[i]:F2} of the way out");
        }
    }

    // A river that ends at the wall over the sea pours out of its mouth.

    [Theory, MemberData(nameof(Every))]
    public void A_mouth_over_a_sea_cliff_falls_clear_of_the_wall_and_the_sea_stacks(string layout) => Check(Strip(layout, sea: true),
        MouthFallsAndTheRockWallStaysUnderTheWater, NoRockStandsWhereAFallLands, LedgeStandsNoLowerThanThePlateauBehindAnyPartOfItsLip);

    private static void NoRockStandsWhereAFallLands(Scene scene)
    {
        var field = scene.Field;
        var fall = field.Rivers.Falls.Single();
        var ledge = field.LedgeOf(fall);
        double half = FallLedge.Width * ledge.Scale / 2;
        foreach (var rock in field.CliffRocks(fall.X - 1, fall.X + 1, fall.Z - 1, fall.Z + 1))
        {
            double dx = Dx(field, rock.X - ledge.X), dz = rock.Z - ledge.Z;
            double along = dx * ledge.DirectionX + dz * ledge.DirectionZ, side = Math.Abs(-dx * ledge.DirectionZ + dz * ledge.DirectionX);
            // Its broad foot counts: one centred beside a narrow sheet still stands in front of it.
            double radius = TerrainField.CliffRockRadius * rock.Scale;
            Assert.False(side - radius <= half && along > ledge.NoseOut && along <= ledge.NoseOut + 0.2,
                $"a column stands {along - ledge.NoseOut:F2} out from the nose, its foot {half - side + radius:F2} across the sheet");
        }
        foreach (var stack in field.SeaStacks)
        {
            double dx = Dx(field, stack.X - ledge.X), dz = stack.Z - ledge.Z;
            Assert.True(dx * dx + dz * dz > 0.6 * 0.6, $"a sea stack stands {Math.Sqrt(dx * dx + dz * dz):F2} from the fall");
        }
    }

    private static void MouthFallsAndTheRockWallStaysUnderTheWater(Scene scene)
    {
        var (field, at) = scene;
        var mouth = Assert.Single(field.Rivers.Curves, c => c.Link.Kind == RiverLinkKind.Mouth);
        var fall = Assert.IsType<RiverFall>(mouth.Fall);
        Assert.False(fall.AlongFoot);
        var (sx, sz) = at.Place(Below).ToPixel(1);
        Assert.True(fall.DirectionX * Dx(field, sx - fall.X) + fall.DirectionZ * (sz - fall.Z) > 0, "the fall points away from the sea");
        var ledge = field.LedgeOf(fall);
        double half = FallLedge.Width * ledge.Scale / 2, slab = ledge.Y - FallLedge.SlabDepth * ledge.Scale * ledge.Stretch;
        bool[] meets = new bool[2];
        foreach (var rock in field.CliffRocks(fall.X - 1, fall.X + 1, fall.Z - 1, fall.Z + 1))
        {
            double dx = Dx(field, rock.X - ledge.X), dz = rock.Z - ledge.Z;
            double along = dx * ledge.DirectionX + dz * ledge.DirectionZ, side = -dx * ledge.DirectionZ + dz * ledge.DirectionX;
            double radius = TerrainField.CliffRockRadius * rock.Scale;
            // The wall meets the ledge on a side of the lip where a column stands under the ledge, or the foot of one
            // beside it reaches the ledge's side (the ledge's own body fills the face between).
            if (along >= -ledge.BackIn - radius && along <= ledge.NoseOut + radius)
                for (int k = 0; k < 2; k++)
                    if ((2 * k - 1) * side >= -half && Math.Abs(side) - radius <= half + 0.01) meets[k] = true;
            if (Math.Abs(side) > half || along < -ledge.BackIn || along > ledge.NoseOut) continue;
            double lean = Math.Sqrt(rock.TiltX * rock.TiltX + rock.TiltZ * rock.TiltZ);
            double top = rock.Y + TerrainField.CliffRockHeight * rock.Scale * rock.Stretch * Math.Cos(lean);
            Assert.True(top <= slab + 0.01, $"a wall column under the ledge stands at {top:F3}, through the slab at {slab:F3}");
        }
        Assert.True(meets[0] && meets[1], $"the wall does not meet the ledge on {(meets[0] ? "one side" : meets[1] ? "the other side" : "either side")}, so it has a gap");
    }

    // Rivers that meet a wall without pouring over it.

    [Theory, MemberData(nameof(Every))]
    public void A_river_that_does_not_pour_over_a_wall_has_no_fall_and_lies_level(string layout)
    {
        // The edge between the high tile and its east neighbour, ending at the corner on the wall with nothing beyond it.
        Check(Strip(layout, rivers: [(High, 0)]), EndsAtACornerOnABrinkWithNoFall);
        // Only the high tile is raised. The river comes along the edge between its low east neighbour and the tile below
        // it, already at the foot, runs on under the high tile's south-east wall and on between the low tiles.
        Check(Strip(layout, raised: [High], rivers: [(East, 4), (High, 5), (Below, 3)]), RunsOnPastTheEndOfAWallWithNoFall,
            LiesLevelFromWaterlineToWaterline, GroundUnderTheRiverAlongTheFootStandsNoHigherThanItsWater);
        Check(Strip(layout, cliff: false), LiesLevelFromWaterlineToWaterline);
    }

    private static void EndsAtACornerOnABrinkWithNoFall(Scene scene)
    {
        var curve = Assert.Single(scene.Field.Rivers.Curves);
        Assert.Null(curve.Fall);
        Assert.Empty(scene.Field.Rivers.Falls);
        Assert.Empty(scene.Falls);
    }

    private static void RunsOnPastTheEndOfAWallWithNoFall(Scene scene)
    {
        Assert.Empty(scene.Field.Rivers.Falls);
        Assert.Empty(scene.Falls);
        // It still runs along the foot of the wall it passes under.
        var foot = Assert.Single(scene.Field.Rivers.Curves, c => c.Foot != null);
        Assert.Null(foot.Fall);
    }

    [Fact]
    public void A_river_without_a_cliff_has_no_fall()
    {
        // With no wall anywhere nothing can pour over one, wherever the strip lies; its water is still drawn.
        var scene = Strip(cliff: false);
        Assert.Empty(scene.Field.Rivers.Falls);
        Assert.Empty(scene.Falls);
        Assert.True(scene.Rivers.VertexCount > 0);
    }

    // Rivers that rise at the foot of a wall.

    [Fact]
    public void A_river_that_starts_at_the_foot_of_a_cliff_springs_from_a_pond_under_the_wall()
    {
        // The east edge of the low tile south-west of the high tile, whose upper corner is the foot of the wall under it.
        var (field, _) = Strip(rivers: [(West, 0)]);
        var curve = Assert.Single(field.Rivers.Curves);
        Assert.Null(curve.Fall);
        bool startAtFoot = curve.Z[0] < curve.Z[^1];
        Assert.False(startAtFoot ? curve.StartsDry : curve.EndsDry);
        Assert.True(startAtFoot ? curve.EndsDry : curve.StartsDry);
        // It rises in a pond, which lies back from the corner, clear of the wall's columns on both sides.
        var pond = Assert.IsType<RiverPond>(startAtFoot ? curve.StartPond : curve.EndPond);
        AssertClearOfWalls(field, pond);
    }

    [Theory, MemberData(nameof(Every))]
    public void A_river_that_rises_at_the_foot_of_a_wall_springs_from_a_pond_clear_of_it(string layout)
    {
        // Only the high tile is raised. The river rises at the corner where its south-east wall ends, and runs along the
        // foot of that wall and on between the low tiles. No river comes over the brink, so nothing falls.
        Check(Strip(layout, raised: [High], rivers: [(High, 5), (Below, 3)]), SpringsFromAPondInTheCorner);
        // The wall the river rises along meets the raised east neighbour's wall at the corner it rises in.
        Check(Strip(layout, rivers: [(High, 5), (Below, 3)]), KeepsItsPondClearOfBothWallsOfAnElbow);
    }

    private static void SpringsFromAPondInTheCorner(Scene scene)
    {
        var (field, at) = scene;
        Assert.Empty(field.Rivers.Falls);
        var (foot, pond) = FootSpring(field, at);
        // It rises in a spring: a pool broader than the river along the foot, but smaller than a pond in the open, so it
        // fits in the cramped corner.
        Assert.True(pond.Spring);
        double river = foot.Width.Min();
        Assert.True(pond.Radius >= 1.2 * river, $"a spring of radius {pond.Radius:F3} on a river {river:F3} wide");
        Assert.True(pond.Radius < 0.8 * RiverNetwork.PondScale * field.Tuning.RiverWidth, $"a spring of radius {pond.Radius:F3}");
        // It lies in that corner, shifted a little out into the low tile: off the corner, with its near shore no further
        // from it than the wall's columns and the gap past them need.
        var (cx, cz) = Corner(at, High, East, Below);
        double off = Math.Sqrt(Math.Pow(Dx(field, pond.X - cx), 2) + Math.Pow(pond.Z - cz, 2));
        Assert.True(off > pond.Radius && off - pond.Radius * (1 + RiverPond.Lobes) < 0.45,
            $"a spring's pond of radius {pond.Radius:F3}, {off:F3} from its corner");
        Assert.True(field.Snapshot.TryTile(Hex.FromPixel(pond.X, pond.Z, 1), out int tile));
        Assert.Equal(foot.Foot!.Low, tile);
        AssertClearOfWalls(field, pond);
    }

    private static void KeepsItsPondClearOfBothWallsOfAnElbow(Scene scene)
    {
        Assert.Empty(scene.Field.Rivers.Falls);
        AssertClearOfWalls(scene.Field, FootSpring(scene.Field, scene.At).Pond);
    }

    [Theory, MemberData(nameof(Every))]
    public void A_spring_bubbles_on_its_water_under_a_breath_of_mist(string layout)
    {
        var scene = Strip(layout, raised: [High], rivers: [(High, 5), (Below, 3)]);
        var (field, at) = scene;
        var (_, spring) = FootSpring(field, at);
        var parts = Chunks(field).Select(c => SpringMesher.Build(c, field)).OfType<SpringMeshData>().ToArray();
        // Drawn once, by the chunk that holds it.
        var data = Assert.Single(parts);
        Assert.Equal(SpringMesher.Bubbles * 4, data.Bubbles.VertexCount);
        Assert.Equal(SpringMesher.Wisps * 4, data.Mist.VertexCount);
        // Every bubble comes up on the water the river mesher draws inside the spring: at the height of the water lanes
        // (level, or lying on the ground just above it) there.
        var river = scene.Rivers;
        double low = double.MaxValue, high = double.MinValue;
        for (int w = 0; w < river.VertexCount; w++)
        {
            if (Math.Abs(river.Lanes[w * 2]) > 1) continue;
            double d = Math.Sqrt(Math.Pow(Dx(field, river.Positions[w * 3] - spring.X), 2) + Math.Pow(river.Positions[w * 3 + 2] - spring.Z, 2));
            if (d > spring.Radius) continue;
            low = Math.Min(low, river.Positions[w * 3 + 1]);
            high = Math.Max(high, river.Positions[w * 3 + 1]);
        }
        Assert.True(low <= high, "the spring has no water drawn in it");
        for (int v = 0; v < data.Bubbles.VertexCount; v++)
        {
            double off = Math.Sqrt(Math.Pow(Dx(field, data.Bubbles.Positions[v * 3] - spring.X), 2)
                + Math.Pow(data.Bubbles.Positions[v * 3 + 2] - spring.Z, 2));
            double reach = off + data.Bubbles.Info[v * 4];
            Assert.True(reach < spring.Radius, $"a bubble reaches {reach:F3} from the middle of a spring of radius {spring.Radius:F3}");
            // The ground between the lattice's vertices can stand a hair above them.
            Assert.InRange(data.Bubbles.Positions[v * 3 + 1], low, high + SpringMesher.BubbleLift + 0.002);
        }
        // The mist hangs over it.
        for (int v = 0; v < data.Mist.VertexCount; v++)
            Assert.True(data.Mist.Positions[v * 3 + 1] > low, "a wisp of the spring's mist starts under its water");
    }

    [Fact]
    public void A_pond_in_the_open_is_no_spring_and_bubbles_nowhere()
    {
        // No cliff: the river ends in a plain pond.
        var (field, _) = Strip(cliff: false, rivers: [(High, 5), (Below, 3)]);
        Assert.Contains(field.Rivers.Ponds, p => !p.Spring);
        Assert.DoesNotContain(field.Rivers.Ponds, p => p.Spring);
        Assert.All(Chunks(field), c => Assert.Null(SpringMesher.Build(c, field)));
    }

    /// <summary>The curve along the foot of the high tile's south-east wall, and the pond it rises in at the corner it
    /// shares with the east and lower tiles.</summary>
    private static (RiverCurve Foot, RiverPond Pond) FootSpring(TerrainField field, Layout at)
    {
        var foot = Assert.Single(field.Rivers.Curves, c => c.Foot != null);
        Assert.Null(foot.Fall);
        var (cx, cz) = Corner(at, High, East, Below);
        var pond = new[] { foot.StartPond, foot.EndPond }.OfType<RiverPond>()
            .MinBy(p => Math.Abs(Dx(field, p.X - cx)) + Math.Abs(p.Z - cz));
        Assert.NotNull(pond);
        return (foot, pond);
    }

    private static void AssertClearOfWalls(TerrainField field, RiverPond pond)
    {
        foreach (var rock in field.CliffRocks(-1, field.Period + 1, -100, 100))
        {
            double gap = Math.Sqrt(Math.Pow(Dx(field, rock.X - pond.X), 2) + Math.Pow(rock.Z - pond.Z, 2))
                - TerrainField.CliffRockRadius * rock.Scale - pond.Radius * (1 + RiverPond.Lobes);
            Assert.True(gap > 0, $"a wall column stands {-gap:F3} into the spring's pond");
        }
    }
}
