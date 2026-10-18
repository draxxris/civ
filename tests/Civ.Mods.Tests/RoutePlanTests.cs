using System.Numerics;
using Civ.Client.Terrain;
using Civ.Content.Schema;
using Civ.Game;
using Xunit;

namespace Civ.Mods.Tests;

public sealed class RoutePlanTests(LoadedMods mods) : IClassFixture<LoadedMods>
{
    [Fact]
    public void Every_land_route_in_the_mods_has_its_own_look()
    {
        var routes = mods.Content.Get<RouteDef>().All.Where(route => !route.SeaTunnel).ToArray();
        var looks = routes.Select(route => RouteLooks.Of(mods.Content, route.Id)).ToArray();

        Assert.Equal(Enum.GetValues<RouteLook>().Length, routes.Length);
        Assert.Equal(looks.Length, looks.Distinct().Count());
    }

    public static TheoryData<RouteLook, RouteLook> LookPairs()
    {
        var data = new TheoryData<RouteLook, RouteLook>();
        foreach (var a in Enum.GetValues<RouteLook>())
            foreach (var b in Enum.GetValues<RouteLook>())
                data.Add(a, b);
        return data;
    }

    /// <summary>Two tiles of a straight line of route, each also linked onward by one of several shapes (dead end, through,
    /// bend, junction), so every way a tile builds its half of the link is met from both sides.</summary>
    [Theory]
    [MemberData(nameof(LookPairs))]
    public void Halves_of_a_link_meet_at_the_shared_edge(RouteLook west, RouteLook east)
    {
        int[][] onward = [[], [3], [2], [1], [2, 4], [1, 3, 5]];
        foreach (var westMore in onward)
            foreach (var eastMore in onward)
            {
                var westCentre = Centre(0, 0);
                var eastCentre = Centre(1, 0);
                // West is tile 0 (direction 0 = east), east is tile 1 (direction 3 = west).
                var westPlan = Plan(west, 0, 0, [(0, east, 1, 0)], westMore.Select(d => (d, west)));
                var eastPlan = Plan(east, 1, 0, [(3, west, 0, 0)], eastMore.Select(d => ((d + 3) % 6, east)));
                var a = westPlan.Paths.Single(p => Vector2.Distance(p.Samples[0].Position, (westCentre + eastCentre) / 2) < 0.3f).Samples[0];
                var b = eastPlan.Paths.Single(p => Vector2.Distance(p.Samples[0].Position, (westCentre + eastCentre) / 2) < 0.3f).Samples[0];

                Assert.True(Vector2.Distance(a.Position, b.Position) < 1e-4f, $"{west}/{east}: edges {a.Position} and {b.Position}");
                Assert.True(Vector2.Dot(a.Tangent, b.Tangent) < -0.9999f, $"{west}/{east}: tangents {a.Tangent} and {b.Tangent}");
                Assert.Equal(0f, a.Along);
                Assert.Equal(0f, b.Along);
                Assert.Equal(west == east ? 0f : 1f, a.Blend + b.Blend, 4);
                // A deck meeting another deck stays up; one meeting the ground comes down to it at the edge.
                float lift = RouteLooks.Elevated(west) && RouteLooks.Elevated(east) ? 1 : 0;
                Assert.Equal(lift, a.Lift, 4);
                Assert.Equal(lift, b.Lift, 4);
            }
    }

    [Fact]
    public void A_ground_route_reaches_the_edge_of_an_elevated_tile_at_full_strength()
    {
        var road = Plan(RouteLook.Highway, 0, 0, [(0, RouteLook.Maglev, 1, 0)], []).Paths[0];
        var deck = Plan(RouteLook.Maglev, 1, 0, [(3, RouteLook.Highway, 0, 0)], [(0, RouteLook.Maglev)]).Paths
            .First(p => p.Other == RouteLook.Highway);

        Assert.All(road.Samples, s => Assert.Equal(0f, s.Blend, 3));
        Assert.Equal(1f, deck.Samples[0].Blend, 3);
        Assert.Equal(0f, deck.Samples[^1].Blend, 3);
        Assert.True(deck.Samples.Zip(deck.Samples.Skip(1)).All(p => p.Second.Lift >= p.First.Lift), "the ramp only climbs");
        Assert.Equal(1f, deck.Samples[^1].Lift, 2);
    }

    [Theory]
    [InlineData(RouteLook.Railroad, new[] { 0, 3 })]
    [InlineData(RouteLook.Railroad, new[] { 0, 2 })]
    [InlineData(RouteLook.Maglev, new[] { 0, 1 })]
    [InlineData(RouteLook.Highway, new[] { 0, 2, 3, 4 })]
    public void Engineered_routes_run_smoothly_through_a_tile_and_repeat_patterns_whole(RouteLook look, int[] directions)
    {
        var plan = Plan(look, 0, 0, [], directions.Select(d => (d, look)));
        var through = plan.Paths.Where(p => p.End == RouteEnd.Through).ToArray();

        Assert.Equal(2, through.Length);
        Assert.Equal(1, through.Count(p => p.Primary));
        var (a, b) = (through[0].Samples[^1], through[1].Samples[^1]);
        Assert.True(Vector2.Distance(a.Position, b.Position) < 1e-4f);
        Assert.True(Vector2.Dot(a.Tangent, b.Tangent) < -0.999f, "the two halves of a through curve meet head on");
        foreach (var path in plan.Paths)
        {
            float units = path.Length / RoutePlan.PatternUnit;
            Assert.Equal(MathF.Round(units), units, 3);
            for (int i = 1; i < path.Samples.Count; i++)
                Assert.True(Vector2.Dot(path.Samples[i - 1].Tangent, path.Samples[i].Tangent) > 0.8f, $"{look}: kink in a path");
        }
        Assert.Equal(directions.Length - 2, plan.Paths.Count(p => p.End == RouteEnd.Merge));
    }

    private static Vector2 Centre(int q, int r)
    {
        var (x, z) = new Hex(q, r).ToPixel(1);
        return new Vector2((float)x, (float)z);
    }

    [Theory]
    [InlineData(RouteLook.Highway, new[] { 0, 2, 4 })]
    [InlineData(RouteLook.Railroad, new[] { 0, 3, 5 })]
    [InlineData(RouteLook.Paved, new[] { 0, 2, 3, 5 })]
    [InlineData(RouteLook.Jumplane, new[] { 0, 1, 2, 3, 4, 5 })]
    [InlineData(RouteLook.Maglev, new[] { 0, 1, 3, 4 })]
    public void Every_point_a_junction_covers_belongs_to_a_path_that_covers_it(RouteLook look, int[] directions)
    {
        for (int q = 0; q < 4; q++)
        {
            var plan = Plan(look, q, 0, [], directions.Select(d => (d, look)));
            Assert.True(RouteJunction.Overlaps(plan));
            var strips = plan.Paths.Select(p => RouteJunction.Resample(p, 0.02f)).ToList();
            var lines = plan.Paths.Select(p => p.Samples.Select(s => s.Position).ToArray()).ToList();
            var rivals = plan.Paths.Select(p => RouteJunction.Others(plan, p)).ToList();
            var centre = Centre(q, 0);
            for (float x = -0.4f; x <= 0.4f; x += 0.01f)
                for (float y = -0.4f; y <= 0.4f; y += 0.01f)
                {
                    var p = centre + new Vector2(x, y);
                    var covering = Enumerable.Range(0, plan.Paths.Count).Where(i => Covers(strips[i], p, 0.05f)).ToList();
                    if (covering.Count == 0) continue;
                    Assert.True(covering.Any(i => RouteJunction.Margin(lines[i], rivals[i], p) >= RouteJunction.Yield(plan.Paths[i])),
                        $"{look} tile {q}: no path owns {x:F2}, {y:F2}");
                }
        }
    }

    /// <summary>Whether a point lies on the strip swept along samples, half wide on either side.</summary>
    private static bool Covers(IReadOnlyList<RouteSample> samples, Vector2 p, float half)
    {
        for (int i = 1; i < samples.Count; i++)
        {
            Vector2 Corner(RouteSample f, float k) => f.Position + new Vector2(-f.Tangent.Y, f.Tangent.X) * half * k;
            Vector2[] quad = [Corner(samples[i - 1], -1), Corner(samples[i - 1], 1), Corner(samples[i], 1), Corner(samples[i], -1)];
            bool inside = false;
            for (int a = 0, b = 3; a < 4; b = a++)
                if ((quad[a].Y > p.Y) != (quad[b].Y > p.Y)
                    && p.X < (quad[b].X - quad[a].X) * (p.Y - quad[a].Y) / (quad[b].Y - quad[a].Y) + quad[a].X)
                    inside = !inside;
            if (inside) return true;
        }
        return false;
    }

    /// <summary>A tile plan with explicit links (direction, neighbour look, neighbour q, r) and extra links to unnamed
    /// neighbours (direction, look). Tile ids are q + 10·r, as the hashes and the First flag need.</summary>
    private static RouteTilePlan Plan(RouteLook look, int q, int r, (int Direction, RouteLook Look, int Q, int R)[] named,
        IEnumerable<(int Direction, RouteLook Look)> more)
    {
        int id = q + 10 * r;
        var links = new List<RouteLinkInput>();
        foreach (var (d, other, nq, nr) in named) links.Add(Link(id, d, other, nq, nr));
        foreach (var (d, other) in more)
        {
            if (named.Any(n => n.Direction == d)) continue;
            var n = new Hex(q, r).Neighbor(d);
            links.Add(Link(id, d, other, n.Q, n.R));
        }
        return RoutePlan.Plan(look, Centre(q, r), links.OrderBy(l => l.Direction).ToList());
    }

    private static RouteLinkInput Link(int id, int direction, RouteLook other, int q, int r)
    {
        int otherId = q + 10 * r;
        int low = Math.Min(id, otherId), high = Math.Max(id, otherId);
        // Any fixed hash of the unordered pair will do.
        float bow = MathF.Sin(low * 12.9898f + high * 78.233f), wiggle = MathF.Cos(low * 39.346f + high * 11.135f);
        return new RouteLinkInput(direction, other, Centre(q, r), bow, wiggle, id < otherId);
    }
}
