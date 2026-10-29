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
}

/// <summary>A point on a river's waterline and the unit direction (<see cref="AcrossX"/>, <see cref="AcrossZ"/>) straight
/// across the water from it, perpendicular to the river's course there.</summary>
public readonly record struct RiverBank(double X, double Z, double AcrossX, double AcrossZ);

/// <summary>Smooth river curves through a <see cref="RiverGraph"/>, with organic width, plus a spatial index for the
/// height field's carve. Links are cubic Hermite curves whose tangents agree at shared corners, so connected links form
/// one smooth river; edge links also meander gently about their rules edge. Widths taper at sources, narrow on hills and
/// peaks and flare at mouths. Everything is a pure function of the snapshot and seed.</summary>
public sealed class RiverNetwork
{
    /// <summary>Distance between curve samples, in world units.</summary>
    public const double SampleStep = 0.08;
    /// <summary>Widest water half width relative to <see cref="TerrainTuning.RiverWidth"/> (a mouth with the widest noise).</summary>
    public const double MaxWidthScale = MouthFlare * NoiseMax;
    private const double MouthFlare = 1.8, ShoreWiden = 1.3, SourceTaper = 0.3, NoiseMax = 1.2;
    /// <summary>Upper bound on the distance from any sample of a curve to either end of its link.</summary>
    public const double MaxLinkSpan = 2.3;
    private const double Meander = 0.07, CellSize = 0.5;
    /// <summary>Distance scale over which <see cref="Level"/> blends the floors of nearby segments.</summary>
    private const double FloorBlend = 0.04;

    private readonly TerrainTuning _tuning;
    private readonly bool _wrap;
    private readonly double _period, _x0, _z0;
    private readonly int _columns, _rows;
    private readonly int[] _cellStart;
    private readonly int[] _entrySegment;
    private readonly double[] _entryShift;
    private readonly Segment[] _segments;

    /// <summary>A straight piece between two samples; carve band, depth and valley floor interpolate along it.</summary>
    private readonly record struct Segment(double AX, double AZ, double BX, double BZ, double WA, double WB, double DA, double DB,
        double FA, double FB);

    /// <summary>The network for a snapshot. <paramref name="noise"/> supplies the wrap-safe width variation, and
    /// <paramref name="ground"/> the uncarved terrain height, which sets each sample's valley floor.</summary>
    public RiverNetwork(TerrainSnapshot snapshot, TerrainTuning tuning, PeriodicNoise noise, Func<double, double, double> ground)
    {
        _tuning = tuning;
        _wrap = snapshot.Grid.Spec.Wrap;
        _period = snapshot.Period;
        Graph = RiverGraph.Build(snapshot);
        Curves = BuildCurves(noise);
        _columns = _wrap ? Math.Max(1, (int)Math.Round(_period / CellSize)) : (int)Math.Ceiling((_period + 8) / CellSize);
        _x0 = _wrap ? 0 : -4;
        _z0 = -6;
        _rows = (int)Math.Ceiling((1.5 * (snapshot.Height - 1) + 12) / CellSize);
        _segments = Curves.SelectMany(c =>
        {
            var floor = Floors(c, ground);
            return Enumerable.Range(0, c.Count - 1).Select(i => new Segment(c.X[i], c.Z[i], c.X[i + 1], c.Z[i + 1], c.Width[i],
                c.Width[i + 1], c.Depth[i], c.Depth[i + 1], floor[i], floor[i + 1]));
        }).ToArray();
        (_cellStart, _entrySegment, _entryShift) = Index();
    }

    /// <summary>The topology the curves follow.</summary>
    public RiverGraph Graph { get; }
    /// <summary>One curve per graph link, in link order.</summary>
    public IReadOnlyList<RiverCurve> Curves { get; }

    /// <summary>Carve depth (≥ 0) at a world point: a smooth trough of half width
    /// <see cref="TerrainTuning.RiverBankRatio"/> times the water half width around every curve.</summary>
    public double Carve(double x, double z)
    {
        double best = 0;
        var (from, to) = Entries(ref x, z);
        for (int e = from; e < to; e++)
        {
            var (s, t, distance) = Closest(e, x, z);
            double band = Lerp(s.WA, s.WB, t) * _tuning.RiverBankRatio;
            if (distance >= band) continue;
            double u = 1 - distance / band * (distance / band);
            best = Math.Max(best, Lerp(s.DA, s.DB, t) * u * u);
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
            var (s, t, distance) = Closest(e, x, z);
            nearest = Math.Min(nearest, Math.Max(0, distance - Lerp(s.WA, s.WB, t) * _tuning.RiverBankRatio));
        }
        if (nearest >= reach) return ground;
        double weights = 0, floors = 0;
        for (int e = from; e < to; e++)
        {
            var (s, t, distance) = Closest(e, x, z);
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
            var (s, t, distance) = Closest(e, x + shift, z);
            if (distance >= best) continue;
            double ax = s.AX + _entryShift[e] - shift, bx = s.BX + _entryShift[e] - shift;
            (best, px, pz, width) = (distance, Lerp(ax, bx, t), Lerp(s.AZ, s.BZ, t), Lerp(s.WA, s.WB, t));
            (tx, tz) = Normalize(bx - ax, s.BZ - s.AZ);
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
            var (s, t, distance) = Closest(e, x, z);
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
    private (Segment Segment, double T, double Distance) Closest(int entry, double x, double z)
    {
        var s = _segments[_entrySegment[entry]];
        double ax = s.AX + _entryShift[entry], bx = s.BX + _entryShift[entry];
        double vx = bx - ax, vz = s.BZ - s.AZ, len2 = vx * vx + vz * vz;
        double t = len2 > 0 ? Math.Clamp(((x - ax) * vx + (z - s.AZ) * vz) / len2, 0, 1) : 0;
        double dx = x - ax - t * vx, dz = z - s.AZ - t * vz;
        return (s, t, Math.Sqrt(dx * dx + dz * dz));
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

    /// <summary>Unit direction from a node along a link's chord.</summary>
    private (double X, double Z) Away(int node, int link)
    {
        var l = Graph.Links[link];
        return l.A == node ? Normalize(l.BX - l.AX, l.BZ - l.AZ) : Normalize(l.AX - l.BX, l.AZ - l.BZ);
    }

    /// <summary>Water half width at a node, before noise: the mean relief scale of its links, tapered at a source, widened
    /// where a mouth leaves the shore and flared at the open end in the water.</summary>
    private double NodeWidth(int node)
    {
        var links = Graph.LinksAt(node);
        double width = _tuning.RiverWidth * links.Average(l => Graph.Links[l].Relief);
        if (Graph.IsOpen(node)) return _tuning.RiverWidth * MouthFlare;
        if (links.Count == 1) return width * SourceTaper;
        if (links.Any(l => Graph.Links[l].Kind == RiverLinkKind.Mouth)) return width * ShoreWiden;
        return width;
    }

    private RiverCurve Curve(int index, Dictionary<(int, int), (double X, double Z)> tangents, double[] widths, PeriodicNoise noise)
    {
        var link = Graph.Links[index];
        double length = link.Length;
        var (t0x, t0z) = tangents[(link.A, index)];
        var (t1x, t1z) = tangents[(link.B, index)];
        // Shorter handles where a tangent turns hard against the chord keep the curve from looping.
        double cx = (link.BX - link.AX) / length, cz = (link.BZ - link.AZ) / length;
        double m0 = length * Math.Clamp(0.5 + t0x * cx + t0z * cz, 0.4, 1);
        double m1 = length * Math.Clamp(0.5 - (t1x * cx + t1z * cz), 0.4, 1);
        double meander = link.Kind == RiverLinkKind.Edge ? Meander * ((link.Hash & 0xffff) / 32767.5 - 1) : 0;
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
        return new(link, xs, zs, txs, tzs, ws, arc, depth, Downstream(link)) { StartsDry = DeadEnd(link.A), EndsDry = DeadEnd(link.B) };
    }

    private bool DeadEnd(int node) => Graph.LinksAt(node).Count == 1 && !Graph.IsOpen(node);

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
