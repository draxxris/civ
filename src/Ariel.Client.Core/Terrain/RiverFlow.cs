namespace Ariel.Client.Terrain;

/// <summary>Which way the water runs along every link of a <see cref="RiverGraph"/>, and an order to walk the links from the
/// sources down. The map's river bits say where rivers run but not which way, so the flow is read off the network: water
/// runs towards the nearest open node (a mouth into the sea or a lake), counted in links, as the world generator drains
/// every river to the water. A network that reaches no open water in plain sight (its mouth hidden, or a hand-made scene)
/// drains to its lowest node instead.</summary>
public sealed class RiverFlow
{
    private RiverFlow(int[] downstream, int[] order) => (Downstream, Order) = (downstream, order);

    /// <summary>Per link, +1 when water runs from its A node to its B node and -1 for the reverse.</summary>
    public IReadOnlyList<int> Downstream { get; }

    /// <summary>Every link once, each after all the links that flow into its upstream node, so a walk in this order sees
    /// everything above a link before the link. Links on a loop, which a river network should not have, come last.</summary>
    public IReadOnlyList<int> Order { get; }

    /// <summary>The node water enters a link at.</summary>
    public int Upstream(RiverLink link, int index) => Downstream[index] > 0 ? link.A : link.B;

    /// <summary>The node water leaves a link at.</summary>
    public int Downhill(RiverLink link, int index) => Downstream[index] > 0 ? link.B : link.A;

    /// <summary>The flow over a graph. <paramref name="known"/> holds a sign per link where its way is already settled
    /// (+1, -1, such as a waterfall's or a source's) and 0 elsewhere; <paramref name="height"/> gives a node's ground
    /// height, which picks the outlet of a network with no open node and breaks ties.</summary>
    public static RiverFlow Of(RiverGraph graph, IReadOnlyList<int> known, Func<int, double> height)
    {
        var hops = Hops(graph, height);
        var links = graph.Links;
        var downstream = new int[links.Count];
        for (int l = 0; l < links.Count; l++)
        {
            var link = links[l];
            downstream[l] = known[l] != 0 ? Math.Sign(known[l])
                : hops[link.A] != hops[link.B] ? (hops[link.A] > hops[link.B] ? 1 : -1)
                : height(link.A) >= height(link.B) ? 1 : -1;
        }
        return new(downstream, Sorted(graph, downstream));
    }

    /// <summary>Links from every node to its network's outlet: the open nodes, or the lowest node of a network without one.</summary>
    private static int[] Hops(RiverGraph graph, Func<int, double> height)
    {
        int count = graph.NodeCount;
        var hops = new int[count];
        Array.Fill(hops, -1);
        var queue = new Queue<int>();
        for (int n = 0; n < count; n++)
            if (graph.IsOpen(n)) { hops[n] = 0; queue.Enqueue(n); }
        Spread(graph, hops, queue);
        for (int n = 0; n < count; n++)
        {
            if (hops[n] >= 0 || graph.LinksAt(n).Count == 0) continue;
            // A network with no open water: its lowest node is the outlet.
            var members = new List<int> { n };
            hops[n] = int.MaxValue;
            for (int i = 0; i < members.Count; i++)
                foreach (int other in Neighbours(graph, members[i]))
                    if (hops[other] == -1) { hops[other] = int.MaxValue; members.Add(other); }
            int outlet = members.MinBy(height);
            foreach (int m in members) hops[m] = -1;
            hops[outlet] = 0;
            queue.Enqueue(outlet);
            Spread(graph, hops, queue);
        }
        return hops;
    }

    private static void Spread(RiverGraph graph, int[] hops, Queue<int> queue)
    {
        while (queue.Count > 0)
        {
            int node = queue.Dequeue();
            foreach (int other in Neighbours(graph, node))
                if (hops[other] == -1) { hops[other] = hops[node] + 1; queue.Enqueue(other); }
        }
    }

    private static IEnumerable<int> Neighbours(RiverGraph graph, int node) =>
        graph.LinksAt(node).Select(l => graph.Links[l].A == node ? graph.Links[l].B : graph.Links[l].A);

    /// <summary>The links in flow order: a node is ready once every link into it has been walked.</summary>
    private static int[] Sorted(RiverGraph graph, int[] downstream)
    {
        var links = graph.Links;
        var into = new int[graph.NodeCount];
        for (int l = 0; l < links.Count; l++) into[downstream[l] > 0 ? links[l].B : links[l].A]++;
        var ready = new Queue<int>();
        for (int n = 0; n < graph.NodeCount; n++)
            if (into[n] == 0) ready.Enqueue(n);
        var order = new List<int>(links.Count);
        var walked = new bool[links.Count];
        while (ready.Count > 0)
        {
            int node = ready.Dequeue();
            foreach (int l in graph.LinksAt(node))
            {
                if (walked[l] || (downstream[l] > 0 ? links[l].A : links[l].B) != node) continue;
                walked[l] = true;
                order.Add(l);
                int below = downstream[l] > 0 ? links[l].B : links[l].A;
                if (--into[below] == 0) ready.Enqueue(below);
            }
        }
        for (int l = 0; l < links.Count; l++)
            if (!walked[l]) order.Add(l);
        return [.. order];
    }
}
