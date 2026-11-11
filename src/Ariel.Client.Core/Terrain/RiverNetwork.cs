namespace Ariel.Client.Terrain;

/// <summary>One sampled river curve: centre points, unit tangents, water half widths, arc lengths and carve depths, in the
/// unwrapped frame of its <see cref="RiverLink"/>. Tangents are exact, so curves meeting at a node share one. <see cref="Downstream"/> is +1 when water flows from the first sample to the last, -1 for
/// the reverse and 0 when the network does not decide (renderers then pick by height).</summary>
public sealed record RiverCurve(RiverLink Link, double[] X, double[] Z, double[] TX, double[] TZ, double[] Width, double[] Arc,
    double[] Depth, int Downstream)
{
    /// <summary>Number of samples.</summary>
    public int Count => X.Length;
    /// <summary>Whether the first sample is a dead end (a source or the tip of a tributary) where the water fades out.</summary>
    public bool StartsDry { get; init; }
    /// <summary>Whether the last sample is a dead end.</summary>
    public bool EndsDry { get; init; }
    /// <summary>The cliff foot this curve runs along, or null. A foot river may also pour over the brink at one end
    /// (<see cref="Fall"/>), or come to the foot already and run along it without falling.</summary>
    public RiverFoot? Foot { get; init; }
    /// <summary>The waterfall where this curve pours over a cliff, or null.</summary>
    public RiverFall? Fall { get; init; }
    /// <summary>The waterfall another curve pours over at an end of this one, or null: this curve brings the water to its
    /// lip, gathered to the lip's width, and runs on onto the ledge rock there.</summary>
    public RiverFall? Feeds { get; init; }
    /// <summary>Whether <see cref="Feeds"/> is at the first sample (otherwise at the last).</summary>
    public bool FeedsAtStart { get; init; }
    /// <summary>Water radius of the pond the first sample ends in, or 0 when it ends in no pond.</summary>
    public double StartPond { get; init; }
    /// <summary>Water radius of the pond the last sample ends in, or 0.</summary>
    public double EndPond { get; init; }
}

/// <summary>A small pond where a river ends on land in plain sight: centre, water radius (the mean of its lobed shoreline,
/// see <see cref="RadiusAt"/>) and a hash that varies its outline.</summary>
public sealed record RiverPond(double X, double Z, double Radius, uint Hash)
{
    /// <summary>Largest share by which the lobed shoreline strays from <see cref="Radius"/>.</summary>
    public const double Lobes = 0.14;

    /// <summary>Water radius toward angle <paramref name="angle"/> (radians from +x toward +z): three soft lobes.</summary>
    public double RadiusAt(double angle)
    {
        double p1 = (Hash & 0xff) / 255.0 * 2 * Math.PI, p2 = ((Hash >> 8) & 0xff) / 255.0 * 2 * Math.PI;
        double p3 = ((Hash >> 16) & 0xff) / 255.0 * 2 * Math.PI;
        return Radius * (1 + Lobes * (0.45 * Math.Cos(angle + p1) + 0.35 * Math.Cos(2 * angle + p2) + 0.2 * Math.Cos(3 * angle + p3)));
    }
}


/// <summary>Where the drawn cliffs stand, for laying rivers along them. <see cref="Face"/> snaps a point onto the nearest
/// drawn cliff face and gives the face's outward normal (zero when there is none); <see cref="Lift"/> is how far the
/// cliffs raise the ground at a point (0 at the foot of every cliff).</summary>
public sealed record CliffShape(
    Func<double, double, (double X, double Z, double NX, double NZ)> Face,
    Func<double, double, double> Lift);

/// <summary>A point on a river's waterline and the unit direction (<see cref="AcrossX"/>, <see cref="AcrossZ"/>) straight
/// across the water from it, perpendicular to the river's course there.</summary>
public readonly record struct RiverBank(double X, double Z, double AcrossX, double AcrossZ);

/// <summary>Smooth river curves through a <see cref="RiverGraph"/>, with organic width, plus a spatial index for the
/// height field's carve. Links are cubic Hermite curves whose tangents agree at shared corners, so connected links form
/// one smooth river. The river only follows its rules edges loosely: every free corner node is drawn part of the way
/// toward the middle of its neighbours (which straightens the zigzag around hex corners) and wanders with a slow noise, so
/// the course sweeps across the hex grid instead of tracing it; edge links also meander gently between their ends. Nodes
/// on cliffs and shores stay put. Widths narrow on hills and peaks and flare at mouths. A river that ends on land in plain
/// sight ends in a small pond. Everything is a pure function of the snapshot and seed.</summary>
public sealed class RiverNetwork
{
    /// <summary>Distance between curve samples, in world units.</summary>
    public const double SampleStep = 0.08;
    /// <summary>Widest water half width relative to <see cref="TerrainTuning.RiverWidth"/> (a mouth with the widest noise).</summary>
    public const double MaxWidthScale = MouthFlare * NoiseMax;
    private const double MouthFlare = 1.8, ShoreWiden = 1.3, SourceTaper = 0.3, PondWidth = 0.75, NoiseMax = 1.2;
    /// <summary>Farthest a corner node moves from its hex corner (see <see cref="NodeShift"/>).</summary>
    public const double MaxNodeShift = 0.35;
    /// <summary>Upper bound on the distance from any sample of a curve to either end of its link, as drawn (both ends may
    /// have moved by up to <see cref="MaxNodeShift"/>).</summary>
    public const double MaxLinkSpan = 2.3 + 2 * MaxNodeShift;
    /// <summary>Pond water radius in <see cref="TerrainTuning.RiverWidth"/> units; with its lobes it stays inside
    /// <see cref="MaxWidthScale"/>.</summary>
    public const double PondScale = 1.75;
    /// <summary>Least distance between two ponds. A dead end nearer than this to a pond already placed tapers out instead,
    /// so a short isolated river ends in one pond rather than two.</summary>
    public const double PondSpacing = 1.6;
    private const double Meander = 0.1, CellSize = 0.5;
    /// <summary>Share of the carve band, from the centre line, over which the channel bed stays at its full depth.</summary>
    private const double BedShare = 0.3;
    // Share of the way a two-link node moves toward the middle of its neighbours, and the reach and frequency of its wander.
    private const double NodePull = 0.32, NodeWander = 0.3, WanderFrequency = 0.22;
    /// <summary>How far out from the face a river along the foot of a cliff runs its centre line, in world units: just past
    /// the line the wall's columns stand on (<see cref="TerrainField.FaceOut"/>), so their broad feet stand in the water,
    /// the wall hides the inner half of the channel and the outer half runs on clear of the rocks.</summary>
    public const double FootOffset = 0.12;
    /// <summary>Distance along a river, in world units, over which it bends out to meet a foot river's far end where
    /// that end has moved off their shared corner (see <see cref="Rejoin"/>).</summary>
    private const double RejoinReach = 0.4;
    /// <summary>Distance along a river, in world units, over which it turns to run straight out over a fall it feeds.</summary>
    private const double AimReach = 0.35;
    /// <summary>Smoothing passes that round the turn where a foot river joins the river it runs on into.</summary>
    private const int RoundPasses = 24;
    /// <summary>Smoothing passes over a foot river's course along the drawn face.</summary>
    private const int FootSmoothing = 3;
    /// <summary>How far before the near side of its fall a foot river's head turns straight along the lip, in world units.</summary>
    private const double HeadIn = 0.08;
    /// <summary>How much faster the carve of a foot river runs out on the cliff's side of its centre line, so the trough
    /// stops at the face and leaves the brink above it whole.</summary>
    private const double WallSteep = 4;
    /// <summary>Distance along a river, in world units, from the end of a river along a foot that it runs on from, over
    /// which its channel still takes the cliff lift away (see <see cref="Unlift"/>).</summary>
    private const double UnliftOn = 1.2;
    /// <summary>Distance along a curve, in world units, over which its water narrows to a fall it runs into.</summary>
    private const double PinchReach = 0.5;
    /// <summary>Distance along a curve, in world units, over which its channel rises to the brink of a fall it runs into,
    /// so the water runs brim-full, level with the ground beside it, onto the ledge.</summary>
    private const double ShallowReach = 2.0;
    /// <summary>Distance scale over which <see cref="Level"/> blends the floors of nearby segments.</summary>
    private const double FloorBlend = 0.04;

    private readonly TerrainTuning _tuning;
    private readonly TerrainSnapshot _snapshot;
    private readonly bool _wrap;
    private readonly double _period, _x0, _z0;
    private readonly int _columns, _rows;
    private readonly int[] _cellStart;
    private readonly int[] _entrySegment;
    private readonly double[] _entryShift;
    private readonly Segment[] _segments;
    private readonly bool _hasFoot;

    /// <summary>A straight piece between two samples; carve band, depth and valley floor interpolate along it.</summary>
    /// <remarks>(<see cref="WX"/>, <see cref="WZ"/>) is the unit direction to the cliff a foot river runs along, zero for
    /// any other river. <see cref="UA"/> and <see cref="UB"/> are the share of the cliff lift its channel takes away at A
    /// and B (see <see cref="Unlift"/>): all of it along a foot, fading out along a river that runs on from one.</remarks>
    private readonly record struct Segment(double AX, double AZ, double BX, double BZ, double WA, double WB, double DA, double DB,
        double FA, double FB, double WX = 0, double WZ = 0, double UA = 0, double UB = 0);

    /// <summary>The network for a snapshot. <paramref name="noise"/> supplies the wrap-safe width variation, and
    /// <paramref name="ground"/> the uncarved terrain height below any cliff's lift, which sets each sample's valley
    /// floor. <paramref name="cliffs"/> says where the drawn cliffs stand, so a river along the foot of a cliff runs
    /// against the face wherever the warp has put it.</summary>
    public RiverNetwork(TerrainSnapshot snapshot, TerrainTuning tuning, PeriodicNoise noise, Func<double, double, double> ground,
        CliffShape? cliffs = null)
    {
        _tuning = tuning;
        _snapshot = snapshot;
        _wrap = snapshot.Grid.Spec.Wrap;
        _period = snapshot.Period;
        Graph = RiverGraph.Build(snapshot);
        var shifts = Enumerable.Range(0, Graph.NodeCount).Select(n => NodeShift(n, noise)).ToArray();
        _links = Graph.Links.Select(l => l with
        {
            AX = l.AX + shifts[l.A].X, AZ = l.AZ + shifts[l.A].Z, BX = l.BX + shifts[l.B].X, BZ = l.BZ + shifts[l.B].Z,
        }).ToArray();
        _ponds = PlacePonds();
        Curves = Aim(Gather(Rejoin(BuildCurves(noise).Select(c => Settle(snapshot, c, cliffs)).ToArray())));
        Falls = Curves.Where(c => c.Fall != null).Select(c => c.Fall!).ToArray();
        Ponds = BuildPonds();
        _columns = _wrap ? Math.Max(1, (int)Math.Round(_period / CellSize)) : (int)Math.Ceiling((_period + 8) / CellSize);
        _x0 = _wrap ? 0 : -4;
        _z0 = -6;
        _rows = (int)Math.Ceiling((1.5 * (snapshot.Height - 1) + 12) / CellSize);
        var footEnds = FootEnds();
        _segments = Curves.SelectMany(c =>
        {
            var floor = Floors(c, ground);
            var unlift = Unlifts(c, footEnds);
            return Enumerable.Range(0, c.Count - 1).Select(i =>
            {
                var (wx, wz) = WallSide(c, i);
                return new Segment(c.X[i], c.Z[i], c.X[i + 1], c.Z[i + 1], c.Width[i], c.Width[i + 1], c.Depth[i], c.Depth[i + 1],
                    floor[i], floor[i + 1], wx, wz, unlift[i], unlift[i + 1]);
            });
        }).Concat(Ponds.Select(p =>
        {
            // A pond is a segment of zero length: its bowl, banks and valley floor come from the same carve as the river.
            double floor = PondFloor(p, ground), width = p.Radius * (1 + RiverPond.Lobes);
            return new Segment(p.X, p.Z, p.X, p.Z, width, width, _tuning.RiverDepth, _tuning.RiverDepth, floor, floor);
        })).ToArray();
        (_cellStart, _entrySegment, _entryShift) = Index();
        _hasFoot = Curves.Any(c => c.Foot != null);
    }

    /// <summary>The topology the curves follow.</summary>
    public RiverGraph Graph { get; }
    /// <summary>One curve per graph link, in link order.</summary>
    public IReadOnlyList<RiverCurve> Curves { get; }
    /// <summary>Every waterfall, one per curve that pours over a cliff.</summary>
    public IReadOnlyList<RiverFall> Falls { get; }
    /// <summary>The ponds at the river ends, one per dead-end node in plain sight.</summary>
    public IReadOnlyList<RiverPond> Ponds { get; }

    private readonly RiverLink[] _links;
    private readonly bool[] _ponds;

    /// <summary>Carve depth (≥ 0) at a world point: a flat-bottomed channel of half width
    /// <see cref="TerrainTuning.RiverBankRatio"/> times the water half width around every curve. The bed under the water
    /// keeps nearly its full depth and the banks rise steeply just beyond the waterline, so the river sits down in the ground
    /// rather than in a shallow dip. Along the foot of a cliff
    /// the trough runs out <see cref="WallSteep"/> times faster on the cliff's side, so it does not cut into the brink.</summary>
    public double Carve(double x, double z)
    {
        double best = 0;
        var (from, to) = Entries(ref x, z);
        for (int e = from; e < to; e++)
        {
            var (s, t, distance, dx, dz) = Closest(e, x, z);
            if (dx * s.WX + dz * s.WZ > 0) distance *= WallSteep;
            double band = Lerp(s.WA, s.WB, t) * _tuning.RiverBankRatio;
            if (distance >= band) continue;
            best = Math.Max(best, Lerp(s.DA, s.DB, t) * (1 - SmoothStep(BedShare, 1, distance / band)));
        }
        return best;
    }

    /// <summary>The share (0..1) of the cliff lift at a world point that the channel of a river along the foot of a cliff
    /// takes away. The low side climbs towards a wall over an apron of lift (<see cref="TerrainTuning.CliffRamp"/>), which
    /// reaches well out near the end of a run of wall; the river would lie on it and it would rise through the level
    /// water. So the channel takes all of it away from its centre line out to its open waterline, fading to none at the
    /// edge of its carve band; on the cliff's side it runs out <see cref="WallSteep"/> times faster, as the carve does,
    /// so the face keeps its step. Past the end of the wall the river runs on round the raised tile's shoulder, with no
    /// rocks to hide water running in under the ground; the river it runs on into takes the lift away too, on both sides
    /// of its centre line, fading out over <see cref="UnliftOn"/> along it. Zero away from those rivers.</summary>
    public double Unlift(double x, double z)
    {
        if (!_hasFoot) return 0;
        double best = 0;
        var (from, to) = Entries(ref x, z);
        for (int e = from; e < to; e++)
        {
            var (s, t, distance, dx, dz) = Closest(e, x, z);
            if (s.UA == 0 && s.UB == 0) continue;
            if (dx * s.WX + dz * s.WZ > 0) distance *= WallSteep;
            double width = Lerp(s.WA, s.WB, t);
            double share = 1 - SmoothStep(width, width * _tuning.RiverBankRatio, distance);
            best = Math.Max(best, share * Lerp(s.UA, s.UB, t));
        }
        return best;
    }

    /// <summary>The terrain drawn down into every river valley. Within <see cref="TerrainTuning.RiverDrawdown"/> beyond a
    /// curve's banks, the ground's height above the curve's valley floor (the lowest ground at its two banks) shrinks towards the
    /// river: to nothing at the bank, so the water is level from bank to bank, easing back to full height at the reach.
    /// A mountain beside a river thus stands lower and slopes more steeply all the way from its summit, rather than
    /// ending in a sheer drop. Low ground beside the river rises slightly to the floor; ground below sea level never rises.</summary>
    public double Level(double x, double z, double ground)
    {
        // The floor is a soft nearest-point lookup (floors weighted by how much further than the nearest segment they lie),
        // and the drawdown follows the nearest bank, so both banks of a river get the same height and the result stays
        // continuous where rivers meet.
        // Distances are measured beyond each segment's bank, which stays continuous where the nearest segment changes.
        double reach = _tuning.RiverDrawdown, nearest = double.MaxValue;
        var (from, to) = Entries(ref x, z);
        for (int e = from; e < to; e++)
        {
            var (s, t, distance, _, _) = Closest(e, x, z);
            nearest = Math.Min(nearest, Math.Max(0, distance - Lerp(s.WA, s.WB, t) * _tuning.RiverBankRatio));
        }
        if (nearest >= reach) return ground;
        double weights = 0, floors = 0;
        for (int e = from; e < to; e++)
        {
            var (s, t, distance, _, _) = Closest(e, x, z);
            double beyond = Math.Max(0, distance - Lerp(s.WA, s.WB, t) * _tuning.RiverBankRatio), excess = beyond - nearest;
            if (beyond >= reach || excess >= 4 * FloorBlend) continue;
            double r = beyond / reach, w = Math.Exp(-excess / FloorBlend) * (1 - r * r) * (1 - r * r);
            weights += w;
            floors += w * Lerp(s.FA, s.FB, t);
        }
        if (weights <= 0) return ground;
        double floor = floors / weights;
        double g = 1 - Math.Pow(1 - nearest / reach, 2.5);
        double level = floor + (ground - floor) * g;
        // Lowland a little under the floor rises to it, so both banks match; ground below sea level keeps its height.
        return level >= ground ? ground + (level - ground) * Math.Clamp(1 + ground / 0.05, 0, 1) : level;
    }

    /// <summary>Valley floor at each sample of a curve: the lowest uncarved ground at its centre and its two banks.</summary>
    private double[] Floors(RiverCurve c, Func<double, double, double> ground)
    {
        var floor = new double[c.Count];
        for (int i = 0; i < c.Count; i++)
        {
            double valley = c.Width[i] * _tuning.RiverBankRatio;
            double nx = -c.TZ[i] * valley, nz = c.TX[i] * valley;
            floor[i] = Math.Min(ground(c.X[i], c.Z[i]), Math.Min(ground(c.X[i] + nx, c.Z[i] + nz), ground(c.X[i] - nx, c.Z[i] - nz)));
        }
        return floor;
    }

    /// <summary>The near bank of the river whose centre line passes closest to (x, z): the waterline point between (x, z)
    /// and that centre line, with the across direction pointing away from (x, z). Positions are in the frame of (x, z).
    /// Null when no river reaches (x, z) through its banks and valley drawdown (more than a tile with the default tuning).</summary>
    public RiverBank? NearBank(double x, double z)
    {
        double shift = x;
        var (from, to) = Entries(ref shift, z);
        shift -= x;
        double best = double.MaxValue, px = 0, pz = 0, tx = 0, tz = 0, width = 0;
        for (int e = from; e < to; e++)
        {
            var (s, t, distance, _, _) = Closest(e, x + shift, z);
            if (distance >= best) continue;
            double ax = s.AX + _entryShift[e] - shift, bx = s.BX + _entryShift[e] - shift;
            (best, px, pz, width) = (distance, Lerp(ax, bx, t), Lerp(s.AZ, s.BZ, t), Lerp(s.WA, s.WB, t));
            // A pond has no course: its across direction points from the point to the pond's centre.
            (tx, tz) = ax == bx && s.AZ == s.BZ ? Normalize(z - s.AZ, ax - x) : Normalize(bx - ax, s.BZ - s.AZ);
        }
        if (best == double.MaxValue) return null;
        double nx = -tz, nz = tx;
        if (nx * (px - x) + nz * (pz - z) < 0) (nx, nz) = (-nx, -nz);
        return new RiverBank(px - nx * width, pz - nz * width, nx, nz);
    }

    /// <summary>Whether a point lies in a river channel or within <paramref name="margin"/> (at most 0.1) of its banks.</summary>
    public bool Near(double x, double z, double margin = 0)
    {
        var (from, to) = Entries(ref x, z);
        for (int e = from; e < to; e++)
        {
            var (s, t, distance, _, _) = Closest(e, x, z);
            if (distance < Lerp(s.WA, s.WB, t) * _tuning.RiverBankRatio + margin) return true;
        }
        return false;
    }

    /// <summary>Whether the straight step from (x0, z0) to (x1, z1) crosses the centre line of a river. The step must be
    /// short (well under 0.1), as only the cell of its midpoint is searched.</summary>
    public bool Crosses(double x0, double z0, double x1, double z1)
    {
        double mx = (x0 + x1) / 2, shifted = mx;
        var (from, to) = Entries(ref shifted, (z0 + z1) / 2);
        double wrap = shifted - mx;
        (x0, x1) = (x0 + wrap, x1 + wrap);
        for (int e = from; e < to; e++)
        {
            var s = _segments[_entrySegment[e]];
            double ax = s.AX + _entryShift[e], bx = s.BX + _entryShift[e];
            if (Side(ax, s.AZ, bx, s.BZ, x0, z0) * Side(ax, s.AZ, bx, s.BZ, x1, z1) <= 0
                && Side(x0, z0, x1, z1, ax, s.AZ) * Side(x0, z0, x1, z1, bx, s.BZ) <= 0
                && (ax != bx || s.AZ != s.BZ))
                return true;
        }
        return false;

        static double Side(double ax, double az, double bx, double bz, double px, double pz) =>
            Math.Sign((bx - ax) * (pz - az) - (bz - az) * (px - ax));
    }

    /// <summary>The index entries of the cell holding a point; wraps x into the canonical period.</summary>
    private (int From, int To) Entries(ref double x, double z)
    {
        if (_segments.Length == 0) return (0, 0);
        if (_wrap) x -= _period * Math.Floor(x / _period);
        int col = (int)Math.Floor((x - _x0) / CellWidth), row = (int)Math.Floor((z - _z0) / CellSize);
        if (_wrap) col = Math.Clamp(col, 0, _columns - 1);
        if (col < 0 || col >= _columns || row < 0 || row >= _rows) return (0, 0);
        int cell = row * _columns + col;
        return (_cellStart[cell], _cellStart[cell + 1]);
    }

    /// <summary>An entry's segment, the parameter of its closest point to (x, z) and the distance to that point.</summary>
    /// <summary>The nearest point of an entry's segment to (x, z): the segment, the share along it, the distance and the
    /// offset from that point to (x, z).</summary>
    private (Segment Segment, double T, double Distance, double DX, double DZ) Closest(int entry, double x, double z)
    {
        var s = _segments[_entrySegment[entry]];
        double ax = s.AX + _entryShift[entry], bx = s.BX + _entryShift[entry];
        double vx = bx - ax, vz = s.BZ - s.AZ, len2 = vx * vx + vz * vz;
        double t = len2 > 0 ? Math.Clamp(((x - ax) * vx + (z - s.AZ) * vz) / len2, 0, 1) : 0;
        double dx = x - ax - t * vx, dz = z - s.AZ - t * vz;
        return (s, t, Math.Sqrt(dx * dx + dz * dz), dx, dz);
    }

    private double CellWidth => _wrap ? _period / _columns : CellSize;

    /// <summary>Compressed cell lists: every segment is listed in each cell its band (plus a margin for
    /// <see cref="Near"/>) can reach, with the whole-period shift that brings it next to that cell on a wrapping map.</summary>
    private (int[] Start, int[] Segment, double[] Shift) Index()
    {
        var cells = new List<(int Segment, double Shift)>[_columns * _rows];
        for (int i = 0; i < _segments.Length; i++) IndexSegment(cells, i);
        return Compress(cells);
    }

    /// <summary>Add a segment to each spatial cell its channel or bank can reach.</summary>
    private void IndexSegment(List<(int Segment, double Shift)>[] cells, int index)
    {
        var s = _segments[index];
        double width = CellWidth;
        double reach = Math.Max(s.WA, s.WB) * _tuning.RiverBankRatio + Math.Max(0.1, _tuning.RiverDrawdown);
        int c0 = (int)Math.Floor((Math.Min(s.AX, s.BX) - reach - _x0) / width);
        int c1 = (int)Math.Floor((Math.Max(s.AX, s.BX) + reach - _x0) / width);
        int r0 = Math.Max(0, (int)Math.Floor((Math.Min(s.AZ, s.BZ) - reach - _z0) / CellSize));
        int r1 = Math.Min(_rows - 1, (int)Math.Floor((Math.Max(s.AZ, s.BZ) + reach - _z0) / CellSize));
        for (int c = c0; c <= c1; c++)
        {
            int col = c, wraps = 0;
            if (_wrap)
            {
                wraps = (int)Math.Floor((double)c / _columns);
                col = c - wraps * _columns;
            }
            else if (c < 0 || c >= _columns) continue;
            for (int r = r0; r <= r1; r++)
                (cells[r * _columns + col] ??= []).Add((index, -wraps * _period));
        }
    }

    /// <summary>Store cell lists as contiguous segment and shift arrays with per-cell offsets.</summary>
    private static (int[] Start, int[] Segment, double[] Shift) Compress(List<(int Segment, double Shift)>[] cells)
    {
        var start = new int[cells.Length + 1];
        for (int i = 0; i < cells.Length; i++) start[i + 1] = start[i] + (cells[i]?.Count ?? 0);
        var segment = new int[start[^1]];
        var shift = new double[start[^1]];
        for (int i = 0; i < cells.Length; i++)
            for (int k = 0; k < (cells[i]?.Count ?? 0); k++)
                (segment[start[i] + k], shift[start[i] + k]) = cells[i]![k];
        return (start, segment, shift);
    }

    private IReadOnlyList<RiverCurve> BuildCurves(PeriodicNoise noise)
    {
        var tangents = NodeTangents();
        var widths = Enumerable.Range(0, Graph.NodeCount).Select(NodeWidth).ToArray();
        var curves = new RiverCurve[Graph.Links.Count];
        for (int i = 0; i < curves.Length; i++) curves[i] = Curve(i, tangents, widths, noise);
        return curves;
    }

    /// <summary>Unit tangent leaving each node along each of its links: the direction to the link's far end, bent away from
    /// the straightest other link at the node, so two links through a corner share one tangent line.</summary>
    private Dictionary<(int Node, int Link), (double X, double Z)> NodeTangents()
    {
        var tangents = new Dictionary<(int, int), (double, double)>();
        for (int node = 0; node < Graph.NodeCount; node++)
        {
            var links = Graph.LinksAt(node);
            foreach (int link in links)
            {
                var (dx, dz) = Away(node, link);
                (double X, double Z)? partner = null;
                double straightest = 2;
                foreach (int other in links)
                {
                    if (other == link) continue;
                    var o = Away(node, other);
                    double dot = dx * o.X + dz * o.Z;
                    if (dot < straightest) (straightest, partner) = (dot, o);
                }
                tangents[(node, link)] = partner is { } p ? Normalize(dx - p.X, dz - p.Z) : (dx, dz);
            }
        }
        return tangents;
    }

    /// <summary>How far a corner node moves from its hex corner: part of the way toward the middle of its two neighbours,
    /// which turns a zigzag around hex corners into a smoother course, plus a slow wander that is the same for every frame
    /// of a wrapping map. Confluences pull half as hard; pinned nodes (cliffs, shores, open ends) and the inland ends of
    /// creeks, which are no hex corners, stay put.</summary>
    private (double X, double Z) NodeShift(int node, PeriodicNoise noise)
    {
        var links = Graph.LinksAt(node);
        if (Graph.IsPinned(node) || links.Count == 0 || links.Any(l => Graph.Links[l].Kind == RiverLinkKind.Creek))
            return (0, 0);
        var first = Graph.Links[links[0]];
        var (x, z) = first.A == node ? (first.AX, first.AZ) : (first.BX, first.BZ);
        double sx = NodeWander * noise.Value(x, z, WanderFrequency, salt: 31);
        double sz = NodeWander * noise.Value(x, z, WanderFrequency, salt: 32);
        if (links.Count >= 2)
        {
            double mx = 0, mz = 0;
            foreach (int link in links)
            {
                var l = Graph.Links[link];
                (mx, mz) = l.A == node ? (mx + l.BX - l.AX, mz + l.BZ - l.AZ) : (mx + l.AX - l.BX, mz + l.AZ - l.BZ);
            }
            double pull = NodePull / links.Count * (links.Count == 2 ? 1 : 0.5);
            sx += mx * pull;
            sz += mz * pull;
        }
        double length = Math.Sqrt(sx * sx + sz * sz);
        return length > MaxNodeShift ? (sx / length * MaxNodeShift, sz / length * MaxNodeShift) : (sx, sz);
    }

    /// <summary>A pond at every dead end in plain sight, centred on the end of its curve.</summary>
    private IReadOnlyList<RiverPond> BuildPonds()
    {
        var ponds = new List<RiverPond>();
        for (int i = 0; i < Curves.Count; i++)
        {
            var c = Curves[i];
            if (c.StartPond > 0) ponds.Add(new(c.X[0], c.Z[0], c.StartPond, _links[i].Hash ^ 0x5bd1e995u));
            if (c.EndPond > 0) ponds.Add(new(c.X[^1], c.Z[^1], c.EndPond, _links[i].Hash ^ 0x1b873593u));
        }
        return ponds;
    }

    /// <summary>Valley floor of a pond: the lowest uncarved ground at its centre and around its banks.</summary>
    private double PondFloor(RiverPond pond, Func<double, double, double> ground)
    {
        double floor = ground(pond.X, pond.Z), bank = pond.Radius * _tuning.RiverBankRatio * 0.6;
        for (int k = 0; k < 6; k++)
            floor = Math.Min(floor, ground(pond.X + bank * Math.Cos(k * Math.PI / 3), pond.Z + bank * Math.Sin(k * Math.PI / 3)));
        return floor;
    }

    /// <summary>Unit direction from a node along a link's chord, as drawn.</summary>
    private (double X, double Z) Away(int node, int link)
    {
        var l = _links[link];
        return l.A == node ? Normalize(l.BX - l.AX, l.BZ - l.AZ) : Normalize(l.AX - l.BX, l.AZ - l.BZ);
    }

    /// <summary>Water half width at a node, before noise: the mean relief scale of its links, tapered at a source, widened
    /// where a mouth leaves the shore and flared at the open end in the water.</summary>
    private double NodeWidth(int node)
    {
        var links = Graph.LinksAt(node);
        double width = _tuning.RiverWidth * links.Average(l => Graph.Links[l].Relief);
        if (Graph.IsOpen(node)) return _tuning.RiverWidth * MouthFlare;
        // A river that ends in a pond keeps most of its width into the pond; one that runs on out of sight tapers.
        if (links.Count == 1) return width * (Pond(node) ? PondWidth : SourceTaper);
        if (links.Any(l => Graph.Links[l].Kind == RiverLinkKind.Mouth)) return width * ShoreWiden;
        return width;
    }

    private RiverCurve Curve(int index, Dictionary<(int, int), (double X, double Z)> tangents, double[] widths, PeriodicNoise noise)
    {
        // The course runs between the link's drawn ends; the curve keeps the rules link, whose corners the waterfall and
        // cliff-foot code read (those corners are pinned, so the two agree wherever it matters).
        var link = _links[index];
        double length = link.Length;
        var (t0x, t0z) = tangents[(link.A, index)];
        var (t1x, t1z) = tangents[(link.B, index)];
        // Shorter handles where a tangent turns hard against the chord keep the curve from looping.
        double cx = (link.BX - link.AX) / length, cz = (link.BZ - link.AZ) / length;
        double m0 = length * Math.Clamp(0.5 + t0x * cx + t0z * cz, 0.4, 1);
        double m1 = length * Math.Clamp(0.5 - (t1x * cx + t1z * cz), 0.4, 1);
        // A cliff or shore edge stays on its rules edge, so a waterfall leaves over the cliff itself.
        bool free = link.Kind == RiverLinkKind.Edge && !Graph.IsPinned(link.A) && !Graph.IsPinned(link.B);
        double meander = free ? Meander * ((link.Hash & 0xffff) / 32767.5 - 1) : 0;
        int n = Math.Max(6, (int)Math.Ceiling(length * 1.3 / SampleStep)) + 1;
        double[] xs = new double[n], zs = new double[n], ws = new double[n], arc = new double[n], depth = new double[n];
        double[] txs = new double[n], tzs = new double[n];
        for (int i = 0; i < n; i++)
        {
            double t = (double)i / (n - 1), t2 = t * t, t3 = t2 * t;
            double h00 = 2 * t3 - 3 * t2 + 1, h10 = t3 - 2 * t2 + t, h01 = -2 * t3 + 3 * t2, h11 = t3 - t2;
            // sin² keeps the meander's slope zero at both ends, so it cannot kink a joint.
            double sine = Math.Sin(Math.PI * t), bend = meander * sine * sine;
            xs[i] = h00 * link.AX + h10 * m0 * t0x + h01 * link.BX - h11 * m1 * t1x - bend * cz;
            zs[i] = h00 * link.AZ + h10 * m0 * t0z + h01 * link.BZ - h11 * m1 * t1z + bend * cx;
            double d00 = 6 * t2 - 6 * t, d10 = 3 * t2 - 4 * t + 1, d01 = 6 * t - 6 * t2, d11 = 3 * t2 - 2 * t;
            double slope = meander * Math.PI * Math.Sin(2 * Math.PI * t);
            (txs[i], tzs[i]) = Normalize(d00 * link.AX + d10 * m0 * t0x + d01 * link.BX - d11 * m1 * t1x - slope * cz,
                d00 * link.AZ + d10 * m0 * t0z + d01 * link.BZ - d11 * m1 * t1z + slope * cx);
            double ease = t * t * (3 - 2 * t);
            double variation = 1 + (NoiseMax - 1) * noise.Value(xs[i], zs[i], 0.9, salt: 21);
            ws[i] = (widths[link.A] + (widths[link.B] - widths[link.A]) * ease) * variation;
            depth[i] = _tuning.RiverDepth * Math.Clamp(ws[i] / _tuning.RiverWidth, 0.35, 1);
            if (i > 0) arc[i] = arc[i - 1] + Math.Sqrt((xs[i] - xs[i - 1]) * (xs[i] - xs[i - 1]) + (zs[i] - zs[i - 1]) * (zs[i] - zs[i - 1]));
        }
        // A dead end at the foot of a cliff is a spring: the water comes out from under the wall at full width.
        var rules = Graph.Links[index];
        return new(rules, xs, zs, txs, tzs, ws, arc, depth, Downstream(rules))
        {
            StartsDry = DeadEnd(rules.A) && !RiverFall.AtFoot(_snapshot, rules, start: true),
            EndsDry = DeadEnd(rules.B) && !RiverFall.AtFoot(_snapshot, rules, start: false),
            StartPond = Pond(rules.A) ? PondRadius(rules) : 0, EndPond = Pond(rules.B) ? PondRadius(rules) : 0,
        };
    }

    /// <summary>A curve with the cliff foot it runs along and its waterfall, if it has them. A mouth that falls off a sea
    /// cliff carves nothing, so the cliff keeps its brink and the water leaves over the ledge. A river edge along a cliff
    /// runs along the foot, so its course moves onto the foot of the face, where the wall's columns stand in its water.
    /// Where it also falls at one corner, it starts under its fall and its flow leads away from it.</summary>
    private RiverCurve Settle(TerrainSnapshot snapshot, RiverCurve c, CliffShape? cliffs)
    {
        var foot = RiverFoot.Of(snapshot, c.Link);
        var fall = RiverFall.Of(snapshot, Graph, c, foot);
        if (fall is { Course: FallCourse.Sea }) return c with { Fall = fall, Depth = new double[c.Count] };
        if (foot == null) return c;
        double[] xs = (double[])c.X.Clone(), zs = (double[])c.Z.Clone(), txs = (double[])c.TX.Clone(), tzs = (double[])c.TZ.Clone();
        for (int i = 0; i < c.Count; i++)
        {
            // Hold the whole course at the foot: from a fall, so the river starts under it instead of on the brink
            // above it (the water falls there; it need not meet the river above with the same tangent), to each corner
            // it runs on from, which the rivers it meets there follow it out to (see Rejoin).
            // The low side of the curve: its across direction, turned towards the low tile.
            double nx = -c.TZ[i], nz = c.TX[i];
            if (nx * foot.DirectionX + nz * foot.DirectionZ < 0) (nx, nz) = (-nx, -nz);
            double tx = c.X[i] + nx * FootOffset, tz = c.Z[i] + nz * FootOffset;
            // The drawn face wanders off the rules edge; follow it where it can be found.
            if (cliffs?.Face(c.X[i], c.Z[i]) is var (fx, fz, fnx, fnz) && (fnx != 0 || fnz != 0))
                (tx, tz) = (fx + fnx * FootOffset, fz + fnz * FootOffset);
            (xs[i], zs[i]) = (tx, tz);
        }
        // Samples by the fall that still stand on the brink (the drawn face can lie well ahead of the corner the link
        // starts from) are dropped, so the river starts at the foot, under its fall, instead of running down the face.
        int drop = 0;
        if (fall != null && cliffs != null)
        {
            double cut = 0.5 * cliffs.Lift(fall.X, fall.Z);
            while (drop < c.Count - 2 && cliffs.Lift(xs[fall.AtStart ? drop : c.Count - 1 - drop], zs[fall.AtStart ? drop : c.Count - 1 - drop]) > cut)
                drop++;
        }
        int first = fall?.AtStart == true ? drop : 0, count = c.Count - drop;
        var ws = c.Width[first..(first + count)];
        var depth = c.Depth[first..(first + count)];
        xs = xs[first..(first + count)]; zs = zs[first..(first + count)];
        // The head lies straight along the lip under the sheet, so the river's water lies under the whole sheet.
        if (fall != null) (xs, zs, ws, depth) = Head(xs, zs, ws, depth, fall);
        count = xs.Length;
        // The drawn face wanders a little from sample to sample; smooth the course along it, so the river does not
        // zig-zag, and round the turn onto the head.
        for (int pass = 0; pass < FootSmoothing; pass++)
        {
            double px = xs[0], pz = zs[0];
            for (int i = 1; i < count - 1; i++)
            {
                double x = xs[i], z = zs[i];
                xs[i] += ((px + xs[i + 1]) / 2 - x) * 0.5;
                zs[i] += ((pz + zs[i + 1]) / 2 - z) * 0.5;
                (px, pz) = (x, z);
            }
        }
        txs = new double[count]; tzs = new double[count];
        for (int i = 0; i < count; i++)
            (txs[i], tzs[i]) = Normalize(xs[Math.Min(i + 1, count - 1)] - xs[Math.Max(i - 1, 0)], zs[Math.Min(i + 1, count - 1)] - zs[Math.Max(i - 1, 0)]);
        // Each corner it runs on from keeps the tangent it meets the next river with.
        if (fall?.AtStart != true) (txs[0], tzs[0]) = (c.TX[0], c.TZ[0]);
        if (fall?.AtStart != false) (txs[count - 1], tzs[count - 1]) = (c.TX[^1], c.TZ[^1]);
        var arc = new double[count];
        for (int i = 1; i < count; i++)
            arc[i] = arc[i - 1] + Math.Sqrt((xs[i] - xs[i - 1]) * (xs[i] - xs[i - 1]) + (zs[i] - zs[i - 1]) * (zs[i] - zs[i - 1]));
        return c with
        {
            X = xs, Z = zs, TX = txs, TZ = tzs, Arc = arc, Width = ws, Depth = depth, Foot = foot, Fall = fall,
            Downstream = fall == null ? c.Downstream : fall.AtStart ? 1 : -1,
        };
    }

    /// <summary>A foot river with its head laid under its fall. Along the face the course follows the foot, but where the
    /// fall pours off a corner the foot turns with the wall, which would turn the river's head into the corner, askew to
    /// the sheet landing in it. So, from <see cref="HeadIn"/> before the near side of the lip, the head runs straight
    /// along the lip, at the distance out from the brink the course had there, to the lip's far side. Its channel runs
    /// out toward the far side, so it does not notch the wall beside the fall.</summary>
    private static (double[] X, double[] Z, double[] Width, double[] Depth) Head(double[] xs, double[] zs, double[] ws,
        double[] depth, RiverFall fall)
    {
        int n = xs.Length;
        // Samples in order from the far end (0) to the fall end.
        int At(int k) => fall.AtStart ? n - 1 - k : k;
        // Across the lip, turned so the river comes from the negative side.
        double ax = -fall.DirectionZ, az = fall.DirectionX;
        if ((xs[At(0)] - fall.X) * ax + (zs[At(0)] - fall.Z) * az > 0) (ax, az) = (-ax, -az);
        double Across(int i) => (xs[i] - fall.X) * ax + (zs[i] - fall.Z) * az;
        int keep = 1;
        while (keep < n - 1 && Across(At(keep)) < -fall.HalfWidth - HeadIn) keep++;
        int s = At(keep - 1);
        double u0 = Across(s), v = (xs[s] - fall.X) * fall.DirectionX + (zs[s] - fall.Z) * fall.DirectionZ;
        int m = Math.Max(1, (int)Math.Ceiling((fall.HalfWidth - u0) / SampleStep));
        double step = (fall.HalfWidth - u0) / m;
        var hx = new double[m];
        var hz = new double[m];
        var hw = new double[m];
        var hd = new double[m];
        for (int i = 0; i < m; i++)
        {
            double u = u0 + step * (i + 1);
            (hx[i], hz[i]) = (fall.X + ax * u + fall.DirectionX * v, fall.Z + az * u + fall.DirectionZ * v);
            hw[i] = ws[s];
            hd[i] = depth[s] * Math.Clamp((fall.HalfWidth - u) / (fall.HalfWidth - u0), 0, 1);
        }
        var kept = Enumerable.Range(0, keep).Select(At).ToArray();
        double[] Body(double[] a) => [.. kept.Select(i => a[i])];
        double[] Join(double[] body, double[] head) => fall.AtStart ? [.. head.Reverse(), .. body.Reverse()] : [.. body, .. head];
        return (Join(Body(xs), hx), Join(Body(zs), hz), Join(Body(ws), hw), Join(Body(depth), hd));
    }

    /// <summary>Every curve, with each foot river joined smoothly to the river it runs on into at each corner it does not
    /// fall at. The foot river's end lies along the foot of the face, off the corner of the rules edge under the wall, so the other
    /// river is first bent out to it (the bend fading over <see cref="RejoinReach"/>). The turn between the two is then
    /// rounded across both: their samples near the corner are smoothed as one chain, and both end at the same point with
    /// the same tangent, so their strips meet in one section instead of crossing at an angle.</summary>
    private RiverCurve[] Rejoin(RiverCurve[] curves)
    {
        for (int k = 0; k < curves.Length; k++)
            foreach (bool farAtB in new[] { true, false })
            {
                if (curves[k].Foot == null || curves[k].Fall is { } fall && fall.AtStart != farAtB) continue;
                Join(curves, k, farAtB);
            }
        return curves;
    }

    /// <summary>Joins foot river k to the river it runs on into at its end (B when <paramref name="farAtB"/>, else A); see
    /// <see cref="Rejoin"/>.</summary>
    private void Join(RiverCurve[] curves, int k, bool farAtB)
    {
        var foot = curves[k];
        int node = farAtB ? foot.Link.B : foot.Link.A;
        var others = Enumerable.Range(0, curves.Length)
            .Where(o => o != k && (curves[o].Link.A == node || curves[o].Link.B == node)).ToArray();
        // Only a plain continuation is rounded; where more rivers meet, or the next one runs along a foot or falls
        // too, the foot river keeps to the corner's own course instead.
        if (others.Length != 1 || curves[others[0]].Fall != null || curves[others[0]].Foot != null) return;
        int o = others[0];
        var other = curves[o];
        bool otherAtA = other.Link.A == node;
        double nx = farAtB ? foot.Link.BX : foot.Link.AX, nz = farAtB ? foot.Link.BZ : foot.Link.AZ;
        int fe = farAtB ? foot.Count - 1 : 0;
        other = Bend(other, otherAtA, foot.X[fe] - nx, foot.Z[fe] - nz);
        // Each curve lies in its own link's frame; the other is rounded in the foot river's, then put back in its own.
        double shift = _snapshot.Nearest(other.X[otherAtA ? 0 : other.Count - 1], foot.X[fe]) - other.X[otherAtA ? 0 : other.Count - 1];
        var (rounded, back) = Round(foot, !farAtB, Shifted(other, shift), otherAtA);
        (curves[k], curves[o]) = (rounded, Shifted(back, -shift));
    }

    /// <summary>A curve moved east by <paramref name="dx"/>, a whole number of periods.</summary>
    private static RiverCurve Shifted(RiverCurve c, double dx) => dx == 0 ? c : c with { X = [.. c.X.Select(x => x + dx)] };

    /// <summary>A curve with its samples near one end moved by (dx, dz), fully at the end and not at all from
    /// <see cref="RejoinReach"/> along it on.</summary>
    private static RiverCurve Bend(RiverCurve c, bool atA, double dx, double dz)
    {
        double[] xs = (double[])c.X.Clone(), zs = (double[])c.Z.Clone();
        double length = c.Arc[^1];
        for (int i = 0; i < c.Count; i++)
        {
            double w = 1 - SmoothStep(0, RejoinReach, atA ? c.Arc[i] : length - c.Arc[i]);
            xs[i] += dx * w; zs[i] += dz * w;
        }
        return Resampled(c, xs, zs);
    }

    /// <summary>Two curves that share an end (at a's start when <paramref name="aAtA"/>, else its end; likewise b),
    /// with the samples within <see cref="RejoinReach"/> of it smoothed as one chain through it, so the turn between them
    /// is a round arc. Both keep the shared end point, and take the chain's tangent there.</summary>
    private static (RiverCurve A, RiverCurve B) Round(RiverCurve a, bool aAtA, RiverCurve b, bool bAtA)
    {
        // The chain runs from inside a, through the shared end, into b.
        var chain = new List<(bool InA, int Index)>();
        foreach (int i in Near(a, aAtA).Reverse()) chain.Add((true, i));
        foreach (int i in Near(b, bAtA).Skip(1)) chain.Add((false, i));
        int joint = Near(a, aAtA).Count() - 1;
        var px = chain.Select(p => p.InA ? a.X[p.Index] : b.X[p.Index]).ToArray();
        var pz = chain.Select(p => p.InA ? a.Z[p.Index] : b.Z[p.Index]).ToArray();
        for (int pass = 0; pass < RoundPasses; pass++)
            for (int j = 1; j < px.Length - 1; j++)
            {
                px[j] += ((px[j - 1] + px[j + 1]) / 2 - px[j]) * 0.5;
                pz[j] += ((pz[j - 1] + pz[j + 1]) / 2 - pz[j]) * 0.5;
            }
        double[] ax = (double[])a.X.Clone(), az = (double[])a.Z.Clone(), bx = (double[])b.X.Clone(), bz = (double[])b.Z.Clone();
        for (int j = 0; j < chain.Count; j++)
            if (chain[j].InA) (ax[chain[j].Index], az[chain[j].Index]) = (px[j], pz[j]);
            else (bx[chain[j].Index], bz[chain[j].Index]) = (px[j], pz[j]);
        int be = bAtA ? 0 : b.Count - 1;
        (bx[be], bz[be]) = (px[joint], pz[joint]);
        // The tangent through the joint, along the chain (from a into b).
        var (tx, tz) = Normalize(px[joint + 1] - px[joint - 1], pz[joint + 1] - pz[joint - 1]);
        var ra = Resampled(a, ax, az);
        var rb = Resampled(b, bx, bz);
        // Each curve's tangent runs from its first sample to its last.
        int ae = aAtA ? 0 : a.Count - 1;
        double sa = aAtA ? -1 : 1, sb = bAtA ? 1 : -1;
        ra.TX[ae] = tx * sa; ra.TZ[ae] = tz * sa;
        rb.TX[be] = tx * sb; rb.TZ[be] = tz * sb;
        return (ra, rb);

        // Indices of a curve's samples within RejoinReach of its shared end, from that end inward.
        static IEnumerable<int> Near(RiverCurve c, bool atA)
        {
            double length = c.Arc[^1];
            for (int n = 0; n < c.Count; n++)
            {
                int i = atA ? n : c.Count - 1 - n;
                if ((atA ? c.Arc[i] : length - c.Arc[i]) > RejoinReach && n > 1) yield break;
                yield return i;
            }
        }
    }

    /// <summary>A curve moved to new sample positions, with its inner tangents and arc lengths recomputed from them; the
    /// tangents at its ends are kept.</summary>
    private static RiverCurve Resampled(RiverCurve c, double[] xs, double[] zs)
    {
        double[] txs = (double[])c.TX.Clone(), tzs = (double[])c.TZ.Clone();
        for (int i = 1; i < c.Count - 1; i++) (txs[i], tzs[i]) = Normalize(xs[i + 1] - xs[i - 1], zs[i + 1] - zs[i - 1]);
        var arc = new double[c.Count];
        for (int i = 1; i < c.Count; i++)
            arc[i] = arc[i - 1] + Math.Sqrt((xs[i] - xs[i - 1]) * (xs[i] - xs[i - 1]) + (zs[i] - zs[i - 1]) * (zs[i] - zs[i - 1]));
        return c with { X = xs, Z = zs, TX = txs, TZ = tzs, Arc = arc };
    }

    /// <summary>Every curve with its water narrowed and its channel made shallow towards each fall it runs into, so a river
    /// gathers into a stream no wider than the ledge it pours over and runs onto it level with its banks, instead of
    /// down a trench cut into the plateau.</summary>
    private RiverCurve[] Gather(RiverCurve[] curves)
    {
        var lips = new Dictionary<int, RiverFall>();
        foreach (var c in curves)
            if (c.Fall is { } f) lips[f.AtStart ? c.Link.A : c.Link.B] = f;
        if (lips.Count == 0) return curves;
        for (int k = 0; k < curves.Length; k++)
        {
            var c = curves[k];
            // A fall's own curve leaves it and keeps its width: the foot river comes out of the plunge pool, and a mouth
            // draws no channel.
            bool atA = lips.TryGetValue(c.Link.A, out var fa) && !Leaves(c, atStart: true);
            bool atB = lips.TryGetValue(c.Link.B, out var fb) && !Leaves(c, atStart: false);
            if (!atA && !atB) continue;
            double a = fa?.HalfWidth ?? 0, b = fb?.HalfWidth ?? 0;
            var ws = (double[])c.Width.Clone();
            var depth = (double[])c.Depth.Clone();
            double length = c.Arc[^1];
            for (int i = 0; i < c.Count; i++)
            {
                if (atA) (ws[i], depth[i]) = (Narrow(ws[i], a, c.Arc[i]), depth[i] * SmoothStep(0, ShallowReach, c.Arc[i]));
                if (atB)
                    (ws[i], depth[i]) = (Narrow(ws[i], b, length - c.Arc[i]), depth[i] * SmoothStep(0, ShallowReach, length - c.Arc[i]));
            }
            curves[k] = c with { Width = ws, Depth = depth, Feeds = atA ? fa : fb, FeedsAtStart = atA };
        }
        return curves;

        static bool Leaves(RiverCurve c, bool atStart) => c.Fall is { } f && f.AtStart == atStart;

        static double Narrow(double width, double lip, double distance) =>
            width <= lip ? width : lip + (width - lip) * SmoothStep(0, PinchReach, distance);
    }

    /// <summary>Every curve that feeds a fall turned, over its last <see cref="AimReach"/>, onto the line the fall pours
    /// along, so it reaches the brink running straight out over the ledge. A link meets its corner from the side, so
    /// otherwise the water turns sharply where it runs on onto the ledge, and both banks kink there.</summary>
    private RiverCurve[] Aim(RiverCurve[] curves)
    {
        for (int k = 0; k < curves.Length; k++)
        {
            if (curves[k].Feeds is not { } fall) continue;
            var c = curves[k];
            bool atA = c.FeedsAtStart;
            double length = c.Arc[^1];
            // The fall is in its own curve's frame, which may lie a period away from this one's.
            double fx = _snapshot.Nearest(fall.X, atA ? c.X[0] : c.X[^1]);
            double[] xs = (double[])c.X.Clone(), zs = (double[])c.Z.Clone();
            for (int i = 0; i < c.Count; i++)
            {
                double from = atA ? c.Arc[i] : length - c.Arc[i];
                double share = SmoothStep(0, 1, 1 - from / AimReach);
                if (share <= 0) continue;
                // The nearest point on the fall's line, through its corner along the way it pours.
                double along = (xs[i] - fx) * fall.DirectionX + (zs[i] - fall.Z) * fall.DirectionZ;
                xs[i] += (fx + fall.DirectionX * along - xs[i]) * share;
                zs[i] += (fall.Z + fall.DirectionZ * along - zs[i]) * share;
            }
            var aimed = Resampled(c, xs, zs);
            // The end runs along the fall: towards it at B, away from it at A, as the curve is oriented.
            int end = atA ? 0 : c.Count - 1;
            double sign = atA ? -1 : 1;
            aimed.TX[end] = sign * fall.DirectionX;
            aimed.TZ[end] = sign * fall.DirectionZ;
            curves[k] = aimed;
        }
        return curves;
    }

    /// <summary>The graph nodes where a river along a foot ends and runs on into another river: either end of each,
    /// except the one under its fall.</summary>
    private HashSet<int> FootEnds()
    {
        var ends = new HashSet<int>();
        foreach (var c in Curves.Where(c => c.Foot != null))
        {
            if (c.Fall?.AtStart != true) ends.Add(c.Link.A);
            if (c.Fall?.AtStart != false) ends.Add(c.Link.B);
        }
        return ends;
    }

    /// <summary>The share of the cliff lift a curve's channel takes away at each sample: all of it along a foot; from 1
    /// at an end where it runs on from a river along a foot, fading to none over <see cref="UnliftOn"/>; none elsewhere.
    /// A river that runs on from a foot rounds the end of the wall over the raised tile's shoulder, with no rocks beside
    /// it, so its water would otherwise run in under the shoulder there.</summary>
    private static double[] Unlifts(RiverCurve c, HashSet<int> footEnds)
    {
        var share = new double[c.Count];
        if (c.Foot != null) Array.Fill(share, 1.0);
        else if (c.Fall == null)
            for (int i = 0; i < c.Count; i++)
                share[i] = Math.Max(
                    footEnds.Contains(c.Link.A) ? 1 - SmoothStep(0, UnliftOn, c.Arc[i]) : 0,
                    footEnds.Contains(c.Link.B) ? 1 - SmoothStep(0, UnliftOn, c.Arc[^1] - c.Arc[i]) : 0);
        return share;
    }

    /// <summary>The unit direction from segment i of a curve to the cliff its river runs along the foot of, or zero.</summary>
    private static (double X, double Z) WallSide(RiverCurve c, int i)
    {
        if (c.Foot is not { } foot) return (0, 0);
        var (tx, tz) = Normalize(c.X[i + 1] - c.X[i], c.Z[i + 1] - c.Z[i]);
        double nx = -tz, nz = tx;
        return nx * foot.DirectionX + nz * foot.DirectionZ > 0 ? (-nx, -nz) : (nx, nz);
    }

    private static double SmoothStep(double edge0, double edge1, double x)
    {
        double t = Math.Clamp((x - edge0) / (edge1 - edge0), 0, 1);
        return t * t * (3 - 2 * t);
    }

    private bool DeadEnd(int node) => Graph.LinksAt(node).Count == 1 && !Graph.IsOpen(node);

    /// <summary>Whether a river ends at a node in a pond (see <see cref="PlacePonds"/>).</summary>
    private bool Pond(int node) => _ponds[node];

    /// <summary>The nodes that end in a pond: dead ends whose surroundings are all explored, except where another such dead
    /// end that comes first in an order fixed by their links' hashes lies within <see cref="PondSpacing"/>. The rule reads
    /// only dead ends within that distance, so a pond stays a local function of the snapshot (see
    /// <see cref="TerrainTuning.RiverReach"/>). A dead end on a cliff or the shore, or one link from a cliff, tapers out
    /// instead: a pond's bowl there would cut into the cliff's lip, the channel along its foot or the beach.</summary>
    private bool[] PlacePonds()
    {
        var ponds = new bool[Graph.NodeCount];
        var candidates = new List<(uint Order, int Node, double X, double Z)>();
        for (int node = 0; node < Graph.NodeCount; node++)
        {
            if (!DeadEnd(node) || Graph.IsVeiled(node) || Graph.IsPinned(node)) continue;
            var link = _links[Graph.LinksAt(node)[0]];
            bool a = link.A == node;
            // A pond's bowl reaches past the far end of its one link, so one link from a cliff it would sink the lip of the
            // fall or the channel along the foot; such a river tapers out instead.
            if (Graph.IsOnCliff(a ? link.B : link.A)) continue;
            candidates.Add((Mix(link.Hash ^ (a ? 0x68e31da4u : 0xb5297a4du)), node, a ? link.AX : link.BX, a ? link.AZ : link.BZ));
        }
        candidates.Sort((p, q) => p.Order != q.Order ? p.Order.CompareTo(q.Order) : p.X != q.X ? p.X.CompareTo(q.X) : p.Z.CompareTo(q.Z));
        for (int i = 0; i < candidates.Count; i++)
        {
            var (_, node, x, z) = candidates[i];
            bool first = true;
            for (int j = 0; j < i && first; j++)
                if (Distance(candidates[j].X, candidates[j].Z, x, z) < PondSpacing) first = false;
            ponds[node] = first;
        }
        return ponds;
    }

    /// <summary>Distance between two points, across the seam on a wrapping map.</summary>
    private double Distance(double ax, double az, double bx, double bz)
    {
        double dx = Math.Abs(ax - bx);
        if (_wrap) { dx %= _period; dx = Math.Min(dx, _period - dx); }
        return Math.Sqrt(dx * dx + (az - bz) * (az - bz));
    }

    private static uint Mix(uint h)
    {
        h ^= h >> 16; h *= 0x7feb352du;
        h ^= h >> 15; h *= 0x846ca68bu;
        return h ^ (h >> 16);
    }

    /// <summary>Water radius of a pond at the end of a link: smaller up in the hills and mountains.</summary>
    private double PondRadius(RiverLink link) => _tuning.RiverWidth * PondScale * Math.Max(link.Relief, 0.6);


    /// <summary>Water leaves sources and runs into the open water; elsewhere the renderer decides from the heights.</summary>
    private int Downstream(RiverLink link)
    {
        if (Graph.IsOpen(link.B) || Graph.LinksAt(link.A).Count == 1) return 1;
        if (Graph.IsOpen(link.A) || Graph.LinksAt(link.B).Count == 1) return -1;
        return 0;
    }

    private static double Lerp(double a, double b, double t) => a + (b - a) * t;

    private static (double X, double Z) Normalize(double x, double z)
    {
        double len = Math.Sqrt(x * x + z * z);
        return len > 1e-12 ? (x / len, z / len) : (1, 0);
    }
}
