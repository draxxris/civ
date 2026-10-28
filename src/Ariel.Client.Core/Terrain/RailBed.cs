namespace Ariel.Client.Terrain;

/// <summary>
/// Heights of the beds of the ground routes, on a graph of their points (nodes) joined along the paths. Each node has a
/// floor: the ground, or a bridge deck. Roads lie on their floor. A railroad keeps to <see cref="RouteProfile.RailGrade"/>
/// in straight runs, as real lines do:
/// <list type="number">
/// <item>The lowest bed with that grade is spread from the floors (a max-plus shortest path: each floor less the grade
/// times the distance to it). Dips shallower than <see cref="RouteProfile.RailSlack"/> are filled, not spanned.</item>
/// <item>Where that bed touches a hilltop, it follows the rounded top. Each such top becomes one apex instead: its highest
/// point, raised just enough that ramps at the grade from it clear the whole top. The bed is spread again.</item>
/// <item>Between two anchors (the apexes, and the points where the bed lies on the ground off a hilltop) the bed is a
/// straight line.
/// So two adjacent hills are joined by one level-ish span, not a sag between two ramps.</item>
/// <item>Where a ramp meets the ground, the bend is rounded upwards over <see cref="RouteProfile.SagRadius"/>.</item>
/// </list>
/// Roads take the rail bed where they meet it, and leave it at the grade, so a road leaving a viaduct comes down with it.
/// </summary>
internal sealed class RailBed(int count)
{
    /// <summary>A hilltop counts when the bed beside it stands this high over the ground.</summary>
    private const float Prominence = 0.03f;
    private const float Touch = 1e-4f;
    /// <summary>A hilltop holds together over bumps that lift the bed off the ground by less than this.</summary>
    private const float TopTouch = 0.015f;

    private readonly float[] _floor = Enumerable.Repeat(float.NegativeInfinity, count).ToArray();
    private readonly bool[] _rail = new bool[count], _span = new bool[count];
    private readonly List<(int To, float Length)>[] _edges = new List<(int, float)>[count];

    private static float Grade => RouteProfile.RailGrade;

    public void Add(int node, float floor, bool rail, bool span)
    {
        _floor[node] = MathF.Max(_floor[node], floor);
        _rail[node] |= rail;
        _span[node] |= span;
    }

    public void Join(int a, int b, float length)
    {
        if (a == b || (_edges[a]?.Any(e => e.To == b) ?? false)) return;
        (_edges[a] ??= []).Add((b, length));
        (_edges[b] ??= []).Add((a, length));
    }

    public float[] Solve()
    {
        var seed = new float[count];
        for (int n = 0; n < count; n++) seed[n] = _rail[n] ? _floor[n] - RouteProfile.RailSlack : float.NegativeInfinity;
        var first = Bed(Spread(seed));
        var apex = new bool[count];
        var onTop = new bool[count];
        foreach (var top in Tops(first).ToList())
        {
            Raise(top, seed, apex);
            foreach (int n in top) onTop[n] = true;
        }
        var bed = Bed(Spread(seed));
        Straighten(bed, n => apex[n] || (!onTop[n] && Touches(bed, n)));
        RoundSags(bed);
        // Roads meeting the rails take their bed and leave it at the grade.
        var rails = new float[count];
        for (int n = 0; n < count; n++) rails[n] = _rail[n] ? bed[n] : float.NegativeInfinity;
        var spread = Spread(rails);
        for (int n = 0; n < count; n++) bed[n] = _rail[n] ? bed[n] : MathF.Max(_floor[n], spread[n]);
        return bed;
    }

    private IEnumerable<(int To, float Length)> Edges(int n) => _edges[n] ?? [];
    private IEnumerable<(int To, float Length)> RailEdges(int n) => Edges(n).Where(e => _rail[e.To]);

    /// <summary>Max-plus Dijkstra: at every node, the highest seed less the grade times the distance to it.</summary>
    private float[] Spread(float[] seed)
    {
        var grade = (float[])seed.Clone();
        var queue = new PriorityQueue<int, float>();
        for (int n = 0; n < count; n++)
            if (grade[n] > float.NegativeInfinity) queue.Enqueue(n, -grade[n]);
        while (queue.TryDequeue(out int n, out float priority))
        {
            if (-priority < grade[n]) continue;
            foreach (var (to, length) in Edges(n))
            {
                float h = grade[n] - Grade * length;
                if (h <= grade[to]) continue;
                grade[to] = h;
                queue.Enqueue(to, -h);
            }
        }
        return grade;
    }

    private float[] Bed(float[] grade) => grade.Select((g, n) => MathF.Max(_floor[n], g)).ToArray();

    private bool Touches(float[] bed, int n) => bed[n] - _floor[n] < Touch;

    /// <summary>Connected rail nodes that pass a test, grown from each unvisited one.</summary>
    private List<List<int>> Components(Func<int, bool> member)
    {
        var seen = new bool[count];
        var parts = new List<List<int>>();
        for (int n = 0; n < count; n++)
        {
            if (seen[n] || !_rail[n] || !member(n)) continue;
            var part = new List<int>();
            var stack = new Stack<int>([n]);
            seen[n] = true;
            while (stack.TryPop(out int m))
            {
                part.Add(m);
                foreach (var (to, _) in RailEdges(m))
                    if (!seen[to] && member(to)) { seen[to] = true; stack.Push(to); }
            }
            parts.Add(part);
        }
        return parts;
    }

    /// <summary>Stretches where the graded bed lies on a hilltop: on the ground (not a bridge), with the bed falling away
    /// on every side into a viaduct at least <see cref="Prominence"/> high.</summary>
    private IEnumerable<List<int>> Tops(float[] bed)
    {
        foreach (var part in Components(n => bed[n] - _floor[n] < TopTouch))
        {
            if (part.Any(n => _span[n]) || !part.Any(n => Touches(bed, n))) continue;
            var inside = part.ToHashSet();
            var sides = part.SelectMany(n => RailEdges(n).Where(e => !inside.Contains(e.To)).Select(e => (From: n, e.To))).ToList();
            if (sides.Count == 0 || sides.Any(s => bed[s.To] >= bed[s.From])) continue;
            if (sides.Max(s => Height(bed, s.To)) >= Prominence) yield return part;
        }
    }

    /// <summary>The greatest clearance of the viaduct a raised node is on.</summary>
    private float Height(float[] bed, int start)
    {
        float high = 0;
        var seen = new HashSet<int> { start };
        var stack = new Stack<int>([start]);
        while (stack.TryPop(out int n))
        {
            high = MathF.Max(high, bed[n] - _floor[n]);
            foreach (var (to, _) in RailEdges(n))
                if (bed[to] - _floor[to] >= TopTouch && seen.Add(to)) stack.Push(to);
        }
        return high;
    }

    /// <summary>Makes a hilltop one apex: the node from which ramps at the grade clear the whole top with the least height,
    /// seeded at that height.</summary>
    private void Raise(List<int> top, float[] seed, bool[] apex)
    {
        var inside = top.ToHashSet();
        var (peak, height) = top.Select(n => (Node: n, Height: Clearing(n, inside))).MinBy(c => c.Height);
        foreach (int n in top) seed[n] = float.NegativeInfinity;
        seed[peak] = height;
        apex[peak] = true;
    }

    /// <summary>The least height at a node from which the grade clears every floor of a top.</summary>
    private float Clearing(int from, HashSet<int> top)
    {
        var distance = new Dictionary<int, float> { [from] = 0 };
        var queue = new PriorityQueue<int, float>();
        queue.Enqueue(from, 0);
        float height = float.NegativeInfinity;
        while (queue.TryDequeue(out int n, out float d))
        {
            if (d > distance[n]) continue;
            height = MathF.Max(height, _floor[n] + Grade * d);
            foreach (var (to, length) in RailEdges(n))
                if (top.Contains(to) && (!distance.TryGetValue(to, out float old) || d + length < old))
                {
                    distance[to] = d + length;
                    queue.Enqueue(to, d + length);
                }
        }
        return height;
    }

    /// <summary>Between two anchors the bed is a straight line, on every simple stretch of line with an anchor at each
    /// end. Anchors are the apexes and the points where the bed lies on the ground off a hilltop (the ramp from an apex
    /// may graze its top elsewhere). The graded bed lies under that line and over the floor, so the line clears the floor
    /// too.</summary>
    private void Straighten(float[] bed, Func<int, bool> anchor)
    {
        foreach (var part in Components(n => !anchor(n)))
        {
            if (part.Any(n => RailEdges(n).Select(e => e.To).Distinct().Count() != 2)) continue;
            var inside = part.ToHashSet();
            var ends = part.SelectMany(n => RailEdges(n).Where(e => !inside.Contains(e.To)).Select(e => (From: n, e.To, e.Length)))
                .ToList();
            if (ends.Count != 2) continue;
            // Walk from one anchor to the other, noting the distance along the way.
            var walk = new List<(int Node, float At)> { (ends[0].To, 0), (ends[0].From, ends[0].Length) };
            while (walk[^1].Node != ends[1].To)
            {
                int n = walk[^1].Node, back = walk[^2].Node;
                var (next, length) = RailEdges(n).First(e => e.To != back);
                walk.Add((next, walk[^1].At + length));
            }
            float h0 = bed[walk[0].Node], h1 = bed[walk[^1].Node], total = walk[^1].At;
            for (int i = 1; i < walk.Count - 1; i++) bed[walk[i].Node] = h0 + (h1 - h0) * walk[i].At / total;
        }
    }

    /// <summary>Rounds the bends where the bed's slope grows along the line (the feet of ramps, and the meeting with the
    /// ground beyond a span), by raising the bed to its triangle-weighted average over <see cref="RouteProfile.SagRadius"/>
    /// each way. The average is exact for the piecewise straight bed, so straight runs keep their line. A crest (an apex)
    /// is left sharp: rounding it would cut into the hill.</summary>
    private void RoundSags(float[] bed)
    {
        var round = (float[])bed.Clone();
        for (int n = 0; n < count; n++)
        {
            if (!_rail[n] || _span[n]) continue;
            var arms = RailEdges(n).Select(e => e.To).Distinct().ToList();
            if (arms.Count != 2) continue;
            var sides = arms.Select(arm => Reach(bed, n, arm)).ToList();
            if (sides.All(side => side != null)) round[n] = MathF.Max(bed[n], sides.Sum(side => Weighed(side!)));
        }
        Array.Copy(round, bed, count);
    }

    /// <summary>The bed along one arm of a node out to SagRadius, as (distance, height) corners; null when a junction or
    /// an end of the line comes first.</summary>
    private List<(float D, float H)>? Reach(float[] bed, int from, int arm)
    {
        float radius = RouteProfile.SagRadius;
        var line = new List<(float D, float H)> { (0, bed[from]) };
        int back = from, at = arm;
        float d = RailEdges(from).First(e => e.To == arm).Length;
        while (true)
        {
            if (d >= radius)
            {
                var (d0, h0) = line[^1];
                line.Add((radius, h0 + (bed[at] - h0) * (radius - d0) / (d - d0)));
                return line;
            }
            line.Add((d, bed[at]));
            var next = RailEdges(at).Where(e => e.To != back).ToList();
            if (next.Select(e => e.To).Distinct().Count() != 1) return null;
            (back, at, d) = (at, next[0].To, d + next[0].Length);
        }
    }

    /// <summary>The integral of a piecewise straight height times the triangle kernel (R - d) / R² over one side; Simpson's
    /// rule is exact for the product of two straight pieces.</summary>
    private static float Weighed(List<(float D, float H)> line)
    {
        float radius = RouteProfile.SagRadius, sum = 0;
        for (int i = 1; i < line.Count; i++)
        {
            var (d0, h0) = line[i - 1];
            var (d1, h1) = line[i];
            float dm = (d0 + d1) / 2, hm = (h0 + h1) / 2;
            float K(float d) => (radius - d) / (radius * radius);
            sum += (d1 - d0) / 6 * (h0 * K(d0) + 4 * hm * K(dm) + h1 * K(d1));
        }
        return sum;
    }
}
