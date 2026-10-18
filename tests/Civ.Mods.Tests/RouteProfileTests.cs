using System.Numerics;
using Civ.Client.Terrain;
using Civ.Game;
using Xunit;

namespace Civ.Mods.Tests;

/// <summary>Heights of routes laid over synthetic ground: rail grades, bridges and ramps to decks.</summary>
public sealed class RouteProfileTests
{
    /// <summary>A straight east-west line of tiles 0..count-1 on row 0, each with its look.</summary>
    private static List<RouteTilePlan> Line(params RouteLook[] looks)
    {
        var plans = new List<RouteTilePlan>();
        for (int q = 0; q < looks.Length; q++)
        {
            var links = new List<RouteLinkInput>();
            if (q + 1 < looks.Length) links.Add(new RouteLinkInput(0, looks[q + 1], Centre(q + 1), 0, 0, true));
            if (q > 0) links.Add(new RouteLinkInput(3, looks[q - 1], Centre(q - 1), 0, 0, false));
            plans.Add(RoutePlan.Plan(looks[q], Centre(q), links));
        }
        return plans;
    }

    private static Vector2 Centre(int q)
    {
        var (x, z) = new Hex(q, 0).ToPixel(1);
        return new Vector2((float)x, (float)z);
    }

    private static IEnumerable<RoutePoint> All(List<RouteTilePlan> plans, RouteProfile profile) =>
        plans.SelectMany(p => p.Paths).SelectMany(p => profile.Of(p).Points);

    /// <summary>Two hills, one per tile, with a dip between their tops.</summary>
    private static float TwoHills(float x, float z)
    {
        float a = Hill(x - Centre(3).X), b = Hill(x - Centre(4).X);
        return MathF.Max(a, b);
        static float Hill(float d) => 0.3f * MathF.Max(0, 1 - d * d / 0.6f);
    }

    [Fact]
    public void A_railroad_ramps_straight_to_each_apex_and_runs_straight_between_two_hills()
    {
        var plans = Line(Enumerable.Repeat(RouteLook.Railroad, 8).ToArray());
        var profile = new RouteProfile(plans, TwoHills, (_, _) => false, (_, _) => false);
        var points = All(plans, profile).OrderBy(p => p.Sample.Position.X).ToList();
        float first = Centre(3).X, second = Centre(4).X;

        Assert.All(points, p => Assert.True(p.Top >= p.Ground - 1e-4f));
        Assert.True(points[0].Clearance < 0.01f, "far from the hills the line is on the ground");
        // No point of a hill pokes over the line, and the line passes over each hill's apex.
        Assert.True(Near(points, first).Top >= TwoHills(first, 0) - 1e-4f);
        Assert.True(Near(points, second).Top >= TwoHills(second, 0) - 1e-4f);
        // Straight from apex to apex, over the dip between the hills.
        Straight(points.Where(p => p.Sample.Position.X > first + 0.02f && p.Sample.Position.X < second - 0.02f).ToList());
        Assert.True(Near(points, (first + second) / 2).Clearance > 0.1f, "the line spans the dip");
        // One straight ramp at the grade up to the first apex, after its rounded foot.
        var ramp = points.Where(p => p.Sample.Position.X < first - 0.02f && p.Clearance > 0.01f).ToList();
        var climb = ramp.Where(p => p.Sample.Position.X > ramp[0].Sample.Position.X + RouteProfile.SagRadius + 0.05f).ToList();
        float slope = Straight(climb);
        Assert.Equal(RouteProfile.RailGrade, slope, 2);
    }

    private static RoutePoint Near(List<RoutePoint> points, float x) => points.MinBy(p => MathF.Abs(p.Sample.Position.X - x));

    /// <summary>Asserts the tops of points ordered by x lie on one straight line, and returns its slope.</summary>
    private static float Straight(List<RoutePoint> points)
    {
        Assert.True(points.Count > 4);
        var (a, b) = (points[0], points[^1]);
        float slope = (b.Top - a.Top) / (b.Sample.Position.X - a.Sample.Position.X);
        Assert.All(points, p => Assert.Equal(a.Top + slope * (p.Sample.Position.X - a.Sample.Position.X), p.Top, 3));
        return slope;
    }

    [Fact]
    public void A_railroad_keeps_its_grade()
    {
        var plans = Line(Enumerable.Repeat(RouteLook.Railroad, 8).ToArray());
        var points = All(plans, new RouteProfile(plans, TwoHills, (_, _) => false, (_, _) => false))
            .OrderBy(p => p.Sample.Position.X).ToList();

        for (int i = 0; i < points.Count; i++)
            for (int j = i + 1; j < points.Count; j++)
            {
                float run = points[j].Sample.Position.X - points[i].Sample.Position.X;
                float rise = MathF.Abs(points[j].Top - points[i].Top);
                Assert.True(rise <= RouteProfile.RailGrade * run + RouteProfile.RailSlack + 1e-3f, $"too steep: {rise} over {run}");
            }
    }

    [Fact]
    public void A_road_follows_the_hills()
    {
        var plans = Line(Enumerable.Repeat(RouteLook.Highway, 8).ToArray());
        var profile = new RouteProfile(plans, TwoHills, (_, _) => false, (_, _) => false);

        Assert.All(All(plans, profile), p => Assert.Equal(p.Ground, p.Top, 4));
    }

    [Theory]
    [InlineData(RouteLook.Road, BridgeKind.Wood)]
    [InlineData(RouteLook.Highway, BridgeKind.Stone)]
    [InlineData(RouteLook.ElectricRailroad, BridgeKind.Steel)]
    public void A_ground_route_crosses_a_river_on_one_bridge_over_the_water(RouteLook look, BridgeKind kind)
    {
        float river = (Centre(1).X + Centre(2).X) / 2;
        bool Wet(float x, float z) => MathF.Abs(x - river) < 0.15f;
        bool Crosses(Vector2 a, Vector2 b) => (a.X - river) * (b.X - river) <= 0;
        var plans = Line(look, look, look, look);
        var profile = new RouteProfile(plans, (x, z) => Wet(x, z) ? -0.05f : 0.03f, Wet, Crosses);
        var bridges = plans.SelectMany(p => p.Paths).Select(p => profile.Of(p).Bridge).OfType<RouteBridge>().ToList();

        var bridge = Assert.Single(bridges);
        Assert.Equal(kind, bridge.Kind);
        Assert.True(bridge.Points[0].Sample.Position.X < river - 0.15f && bridge.Points[^1].Sample.Position.X > river + 0.15f
            || bridge.Points[^1].Sample.Position.X < river - 0.15f && bridge.Points[0].Sample.Position.X > river + 0.15f);
        Assert.All(bridge.Points, p => Assert.True(p.Top > 0.03f));
    }

    [Fact]
    public void A_route_beside_a_river_bend_needs_no_bridge()
    {
        // The route runs through a river's bank by a bend, but never over its centre line.
        float bend = (Centre(1).X + Centre(2).X) / 2;
        bool Wet(float x, float z) => MathF.Abs(x - bend) < 0.15f;
        var plans = Line(RouteLook.Road, RouteLook.Road, RouteLook.Road, RouteLook.Road);
        var profile = new RouteProfile(plans, (_, _) => 0.03f, Wet, (_, _) => false);

        Assert.All(plans.SelectMany(p => p.Paths), p => Assert.Null(profile.Of(p).Span));
        Assert.All(All(plans, profile), p => Assert.Equal(p.Ground, p.Top, 4));
    }

    [Fact]
    public void A_deck_needs_no_bridge()
    {
        float river = (Centre(1).X + Centre(2).X) / 2;
        bool Wet(float x, float z) => MathF.Abs(x - river) < 0.15f;
        bool Crosses(Vector2 a, Vector2 b) => (a.X - river) * (b.X - river) <= 0;
        var plans = Line(RouteLook.Maglev, RouteLook.Maglev, RouteLook.Maglev, RouteLook.Maglev);
        var profile = new RouteProfile(plans, (x, z) => Wet(x, z) ? -0.05f : 0.03f, Wet, Crosses);

        Assert.All(plans.SelectMany(p => p.Paths), p => Assert.Null(profile.Of(p).Bridge));
        Assert.Null(RouteProfile.BridgeOf(RouteLook.Maglev, RouteLook.Jumplane));
    }

    [Theory]
    [InlineData(RouteLook.Highway, RouteLook.Jumplane)]
    [InlineData(RouteLook.Railroad, RouteLook.Maglev)]
    [InlineData(RouteLook.ElectricRailroad, RouteLook.Maglev)]
    public void A_ground_route_climbs_to_meet_a_descending_deck_at_the_edge(RouteLook ground, RouteLook deck)
    {
        var plans = Line(ground, ground, ground, deck, deck, deck);
        var profile = new RouteProfile(plans, (_, _) => 0.03f, (_, _) => false, (_, _) => false);
        var low = plans[2].Paths.Single(p => p.Other == deck);
        var high = plans[3].Paths.Single(p => p.Other == ground);
        var a = profile.Of(low).Points[0];
        var b = profile.Of(high).Points[0];

        Assert.Equal(a.Top, b.Top, 3);
        Assert.True(a.Clearance > 0.05f, "the ground route has left the ground at the edge");
        Assert.True(profile.Of(high).Points[^1].Top > b.Top + 0.05f, "the deck is higher inside its tile");
        Assert.Equal(a.Ground, profile.Of(low).Points[^1].Top, 3);
    }
}
