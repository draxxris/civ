namespace Civ.Client.Terrain;

/// <summary>What a <see cref="RiverLink"/> draws.</summary>
public enum RiverLinkKind : byte
{
    /// <summary>A river edge between two land tiles, exactly where the rules put it.</summary>
    Edge,
    /// <summary>A connection inside one river tile between the ends of its river edges.</summary>
    Bridge,
    /// <summary>A mouth from a river corner on the shore out into the neighbouring water tile.</summary>
    Mouth,
    /// <summary>A short stream across an isolated shore river edge, from the land tile into the water.</summary>
    Creek,
}

/// <summary>One curve of the visual river network between nodes <see cref="A"/> and <see cref="B"/>. Positions are in
/// one unwrapped frame (the frame of the tile that created the link), so the link never jumps across the wrap seam.
/// <see cref="Relief"/> scales its width (narrow mountain streams); <see cref="Hash"/> varies its meander.</summary>
public readonly record struct RiverLink(int A, int B, double AX, double AZ, double BX, double BZ, RiverLinkKind Kind,
    double Relief, uint Hash)
{
    /// <summary>Straight distance between the two ends.</summary>
    public double Length => Math.Sqrt((BX - AX) * (BX - AX) + (BZ - AZ) * (BZ - AZ));
}

/// <summary>A visual interpretation of the known river-edge bits as one connected network of curves.
/// <para>The map generator marks the edge crossed by each step of a tile-centre drainage chain, so consecutive river
/// edges often sit on opposite or skewed sides of a tile and do not touch. Every river edge between land tiles is kept
/// exactly where the rules put it. Inside each river tile, bridges join the separate runs of river edges across the
/// shortest gaps around the tile. A straight chain alternates the side of its bridges by tile parity, which turns it into
/// a meander; other ties prefer the choice that leaves fewest dead ends. Edges on a coast or lake become mouths.</para>
/// <para>Every decision reads only the river bits and relief of tiles within two tiles of the link, so a chunk's river
/// geometry stays a bounded, local function of the snapshot (see <see cref="TerrainTuning.RiverReach"/>).</para></summary>
public sealed class RiverGraph
{
    private static readonly double Sqrt3 = Math.Sqrt(3);
    // Corner j of a pointy-top hex lies at angle 30° − 60°·j, in units of (√3/2, 1/2): edge d spans corners d and d + 1.
    private static readonly (int X, int Z)[] CornerUnits = [(1, 1), (1, -1), (0, -2), (-1, -1), (-1, 1), (0, 2)];

    private readonly List<RiverLink> _links = [];
    private readonly List<List<int>> _nodeLinks = [];
    private readonly List<bool> _open = [];

    private RiverGraph() { }

    /// <summary>All links.</summary>
    public IReadOnlyList<RiverLink> Links => _links;
    /// <summary>Number of nodes: shared hex corners plus the open ends of mouths and creeks.</summary>
    public int NodeCount => _nodeLinks.Count;
    /// <summary>Link indices meeting at a node.</summary>
    public IReadOnlyList<int> LinksAt(int node) => _nodeLinks[node];
    /// <summary>Whether a node is the open end of a mouth or creek in a water tile, where the river widens into the water.</summary>
    public bool IsOpen(int node) => _open[node];

    /// <summary>The network for a snapshot's known river bits.</summary>
    public static RiverGraph Build(TerrainSnapshot snapshot) => new Builder(snapshot).Build();

    private int Node(Dictionary<long, int> corners, long key)
    {
        if (corners.TryGetValue(key, out int node)) return node;
        corners[key] = node = AddNode(open: false);
        return node;
    }

    private int AddNode(bool open)
    {
        _nodeLinks.Add([]);
        _open.Add(open);
        return _nodeLinks.Count - 1;
    }

    private void AddLink(RiverLink link)
    {
        _nodeLinks[link.A].Add(_links.Count);
        _nodeLinks[link.B].Add(_links.Count);
        _links.Add(link);
    }

    /// <summary>A bridge inside a tile between corners <see cref="From"/> and <see cref="To"/> (tile-local indices).</summary>
    private readonly record struct Bridge(int From, int To);

    /// <summary>A river edge between a land tile and a water tile: direction <see cref="Direction"/> from the land tile.</summary>
    private readonly record struct Shore(int Land, int Direction, int Water);

    private sealed class Builder(TerrainSnapshot snapshot)
    {
        private readonly RiverGraph _graph = new();
        private readonly Dictionary<long, int> _corners = new();
        private readonly HashSet<(long, long)> _edges = [];
        private readonly List<(int Tile, int Direction)> _landEdges = [];
        private readonly List<Shore> _shores = [];
        private readonly HashSet<long> _shoreCorners = [];
        private readonly Dictionary<long, int> _degree = new();
        private readonly Dictionary<int, Bridge[][]> _options = new();
        private readonly Dictionary<int, int> _defaults = new();

        public RiverGraph Build()
        {
            CollectEdges();
            CollectBridgeOptions();
            foreach (var (tile, d) in _landEdges) Count(Corner(tile, d), Corner(tile, d + 1), 1);
            foreach (var (tile, options) in _options) CountBridges(tile, options[_defaults[tile]], 1);
            foreach (var (tile, d) in _landEdges) AddLink(tile, d, d + 1, RiverLinkKind.Edge, EdgeRelief(tile, d));
            foreach (var (tile, options) in _options)
                foreach (var bridge in options[Choose(tile, options)])
                    AddLink(tile, bridge.From, bridge.To, RiverLinkKind.Bridge, ReliefScale(snapshot.Relief[tile]));
            AddMouths();
            return _graph;
        }

        /// <summary>River edges between land tiles become links once each; edges on water become shores.</summary>
        private void CollectEdges()
        {
            for (int tile = 0; tile < snapshot.Rivers.Length; tile++)
            {
                if (snapshot.Rivers[tile] == 0) continue;
                for (int d = 0; d < 6; d++)
                {
                    if ((snapshot.Rivers[tile] & (1 << d)) == 0) continue;
                    CollectEdge(tile, d);
                }
            }
        }

        private void CollectEdge(int tile, int direction)
        {
            if (!snapshot.TryTile(HexOf(tile).Neighbor(direction), out int other)) return;
            bool water = IsWater(tile), otherWater = IsWater(other);
            if (water && otherWater) return;
            if (otherWater)
            {
                _shores.Add(new(tile, direction, other));
                _shoreCorners.Add(Corner(tile, direction));
                _shoreCorners.Add(Corner(tile, direction + 1));
                return;
            }
            if (!water && _edges.Add(Key(Corner(tile, direction), Corner(tile, direction + 1))))
                _landEdges.Add((tile, direction));
        }

        /// <summary>Each land tile with two or more separate runs of river edges joins them across all gaps except one of
        /// the largest. Several largest gaps make a tie, resolved later against the neighbours.</summary>
        private void CollectBridgeOptions()
        {
            for (int tile = 0; tile < snapshot.Rivers.Length; tile++)
            {
                int mask = snapshot.Rivers[tile];
                if (mask == 0 || IsWater(tile)) continue;
                var gaps = Gaps(mask);
                if (gaps.Count < 2) continue;
                int widest = gaps.Max(g => g.Length);
                var options = gaps.Select((g, i) => (g, i)).Where(x => x.g.Length == widest)
                    .Select(omit => gaps.Where((_, i) => i != omit.i).Select(g => new Bridge(g.From, g.To)).ToArray())
                    .ToArray();
                _options[tile] = options;
                _defaults[tile] = DefaultOption(tile, mask, options);
            }
        }

        /// <summary>A straight tile (two opposite river edges) bridges on the side given by its parity along the river axis,
        /// so a chain of straight tiles alternates sides and each river edge is entered at one end and left at the other.</summary>
        private int DefaultOption(int tile, int mask, Bridge[][] options)
        {
            if (options.Length == 1) return 0;
            for (int a = 0; a < 3; a++)
            {
                if (mask != ((1 << a) | (1 << (a + 3)))) continue;
                var hex = HexOf(tile);
                bool even = ((a == 2 ? hex.R : hex.Q) & 1) == 0;
                var wanted = even ? new Bridge(a + 1, a + 3) : new Bridge(a + 4, a);
                int index = Array.FindIndex(options, o => o.Any(b => Same(b, wanted)));
                if (index >= 0) return index;
            }
            return (int)(TileHash(tile) % (uint)options.Length);
        }

        /// <summary>The option that leaves fewest dead-end corners around the tile, given every neighbour's default.</summary>
        private int Choose(int tile, Bridge[][] options)
        {
            int chosen = _defaults[tile];
            if (options.Length == 1) return chosen;
            CountBridges(tile, options[chosen], -1);
            int best = int.MaxValue;
            for (int i = 0; i < options.Length; i++)
            {
                CountBridges(tile, options[i], 1);
                int ends = Enumerable.Range(0, 6).Count(j => DeadEnd(Corner(tile, j)));
                CountBridges(tile, options[i], -1);
                if (ends < best || (ends == best && i == _defaults[tile])) (best, chosen) = (ends, i);
            }
            CountBridges(tile, options[_defaults[tile]], 1);
            return chosen;
        }

        private bool DeadEnd(long corner) => _degree.GetValueOrDefault(corner) == 1 && !_shoreCorners.Contains(corner);

        private void CountBridges(int tile, Bridge[] bridges, int delta)
        {
            foreach (var b in bridges) Count(Corner(tile, b.From), Corner(tile, b.To), delta);
        }

        private void Count(long a, long b, int delta)
        {
            _degree[a] = _degree.GetValueOrDefault(a) + delta;
            _degree[b] = _degree.GetValueOrDefault(b) + delta;
        }

        /// <summary>Each shore edge opens a mouth into the water from every one of its corners the river reaches; a shore edge
        /// the river reaches at neither corner becomes a short creek across it.</summary>
        private void AddMouths()
        {
            var opened = new HashSet<(int Node, int Water)>();
            foreach (var shore in _shores)
            {
                var (cx, cz) = Centre(shore.Land);
                var (dx, dz) = Offset(shore.Direction);
                double wx = cx + dx, wz = cz + dz;
                bool reached = false;
                foreach (int j in new[] { shore.Direction, shore.Direction + 1 })
                {
                    if (!_corners.TryGetValue(Corner(shore.Land, j), out int node) || _graph.LinksAt(node).Count == 0) continue;
                    reached = true;
                    if (!opened.Add((node, shore.Water))) continue;
                    var (x, z) = CornerPosition(shore.Land, j);
                    var (ex, ez) = Toward(x, z, wx, wz, 0.6);
                    _graph.AddLink(new(node, _graph.AddNode(open: true), x, z, ex, ez, RiverLinkKind.Mouth,
                        ReliefScale(snapshot.Relief[shore.Land]), Hash(node, shore.Water)));
                }
                if (reached) continue;
                double mx = cx + dx / 2, mz = cz + dz / 2;
                var (sx, sz) = Toward(mx, mz, cx, cz, 0.4);
                var (tx, tz) = Toward(mx, mz, wx, wz, 0.45);
                _graph.AddLink(new(_graph.AddNode(open: false), _graph.AddNode(open: true), sx, sz, tx, tz, RiverLinkKind.Creek,
                    ReliefScale(snapshot.Relief[shore.Land]), TileHash(shore.Land) ^ (uint)shore.Direction));
            }
        }

        private void AddLink(int tile, int from, int to, RiverLinkKind kind, double relief)
        {
            long a = Corner(tile, from), b = Corner(tile, to);
            var (ax, az) = CornerPosition(tile, from);
            var (bx, bz) = CornerPosition(tile, to);
            _graph.AddLink(new(_graph.Node(_corners, a), _graph.Node(_corners, b), ax, az, bx, bz, kind, relief, Hash(a, b)));
        }

        private double EdgeRelief(int tile, int d)
        {
            double relief = ReliefScale(snapshot.Relief[tile]);
            return snapshot.TryTile(HexOf(tile).Neighbor(d), out int other) && snapshot.Relief[other] != ReliefClass.Hidden
                ? Math.Min(relief, ReliefScale(snapshot.Relief[other])) : relief;
        }

        private bool IsWater(int tile) => snapshot.Relief[tile] is ReliefClass.DeepWater or ReliefClass.ShallowWater;

        private Hex HexOf(int tile) => snapshot.Grid.HexOf(new TileId(tile));

        private (double X, double Z) Centre(int tile) => HexOf(tile).ToPixel(1);

        private (double X, double Z) CornerPosition(int tile, int corner)
        {
            var (cx, cz) = Centre(tile);
            var (ux, uz) = CornerUnits[corner % 6];
            return (cx + ux * Sqrt3 / 2, cz + uz * 0.5);
        }

        /// <summary>A corner's map-wide key: its position in units of (√3/2, 1/2), wrapped east-west.</summary>
        private long Corner(int tile, int corner)
        {
            var hex = HexOf(tile);
            var (ux, uz) = CornerUnits[corner % 6];
            long x = 2L * hex.Q + hex.R + ux, z = 3L * hex.R + uz;
            if (snapshot.Grid.Spec.Wrap) x = ((x % (2L * snapshot.Width)) + 2L * snapshot.Width) % (2L * snapshot.Width);
            return (z << 32) ^ (x & 0xffffffffL);
        }

        private uint TileHash(int tile) => Mix((uint)tile * 0x9e3779b9u + 0x7f4a7c15u);
    }

    /// <summary>Corner pairs of the gaps between runs of set bits in a six-bit edge mask, in order around the tile:
    /// <c>From</c> ends one run, <c>To</c> starts the next, <c>Length</c> counts the edges between them.</summary>
    private static List<(int From, int To, int Length)> Gaps(int mask)
    {
        var gaps = new List<(int, int, int)>();
        if (mask == 63) return gaps;
        for (int start = 0; start < 6; start++)
        {
            if (!Set(mask, start) || Set(mask, start + 5)) continue;
            int end = start;
            while (Set(mask, end + 1)) end++;
            int next = end + 2;
            while (!Set(mask, next)) next++;
            gaps.Add(((end + 1) % 6, next % 6, next - end - 1));
        }
        return gaps;
    }

    private static bool Set(int mask, int edge) => (mask & (1 << (edge % 6))) != 0;

    private static bool Same(Bridge a, Bridge b) =>
        (a.From % 6 == b.From % 6 && a.To % 6 == b.To % 6) || (a.From % 6 == b.To % 6 && a.To % 6 == b.From % 6);

    private static (double X, double Z) Offset(int d)
    {
        var dir = Hex.Directions[d % 6];
        return (Sqrt3 * (dir.Q + dir.R / 2.0), 1.5 * dir.R);
    }

    private static (double X, double Z) Toward(double x, double z, double tx, double tz, double distance)
    {
        double dx = tx - x, dz = tz - z, len = Math.Sqrt(dx * dx + dz * dz);
        return (x + dx / len * distance, z + dz / len * distance);
    }

    /// <summary>Width scale by relief: mountain streams are narrow.</summary>
    private static double ReliefScale(ReliefClass relief) => relief switch
    {
        ReliefClass.Peak => 0.5,
        ReliefClass.Hills => 0.75,
        _ => 1,
    };

    private static (long, long) Key(long a, long b) => a < b ? (a, b) : (b, a);

    private static uint Hash(long a, long b)
    {
        var (lo, hi) = Key(a, b);
        return Mix((uint)lo ^ Mix((uint)(lo >> 32) ^ Mix((uint)hi ^ Mix((uint)(hi >> 32)))));
    }

    private static uint Mix(uint h)
    {
        h ^= h >> 16; h *= 0x7feb352du;
        h ^= h >> 15; h *= 0x846ca68bu;
        return h ^ (h >> 16);
    }
}
