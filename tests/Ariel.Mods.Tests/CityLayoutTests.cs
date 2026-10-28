using Ariel.Client.Terrain;
using Ariel.Game;
using Xunit;

namespace Ariel.Mods.Tests;

public sealed class CityLayoutTests
{
    /// <summary>A hex ring of thin wall strips at hex distance 0.745, with a gate toward 120 degrees.</summary>
    private static CityWalls Walls()
    {
        var obstacles = new List<(double X, double Z)[]>();
        for (int k = 0; k < 6; k++)
        {
            double a0 = (30 + 60 * k) * Math.PI / 180, a1 = (90 + 60 * k) * Math.PI / 180;
            (double, double) P(double a, double r) => (Math.Cos(a) * r, Math.Sin(a) * r);
            obstacles.Add([P(a0, 0.83), P(a1, 0.83), P(a1, 0.89), P(a0, 0.89)]);
        }
        return new CityWalls(obstacles, 0.745, 0.78, 2 * Math.PI / 3);
    }

    private static CityPlan Plan(int population, CityStyle style = CityStyle.Huts, CityWalls? walls = null, int roads = 0,
        Func<double, double, bool>? buildable = null) =>
        CityLayout.Plan(new CityLayoutInput(42, population, style, walls, roads, buildable ?? ((_, _) => true)));

    [Fact]
    public void The_same_input_gives_the_same_layout()
    {
        Assert.Equal(Plan(14, walls: Walls()).Lots, Plan(14, walls: Walls()).Lots);
    }

    [Fact]
    public void Larger_cities_have_more_buildings()
    {
        int[] counts = [Plan(3).Lots.Count, Plan(8).Lots.Count, Plan(13).Lots.Count, Plan(20).Lots.Count];
        Assert.True(counts.Zip(counts.Skip(1)).All(p => p.First < p.Second), string.Join(",", counts));
    }

    [Theory]
    [InlineData(CityStyle.Huts)]
    [InlineData(CityStyle.Victorian)]
    [InlineData(CityStyle.Modern)]
    public void No_building_overlaps_a_wall_or_another_building(CityStyle style)
    {
        var walls = Walls();
        var lots = Plan(20, style, walls).Lots;
        foreach (var lot in lots)
            Assert.DoesNotContain(walls.Obstacles, o => CityLayout.Overlaps(lot.Corners(), o));
        for (int i = 0; i < lots.Count; i++)
            for (int j = i + 1; j < lots.Count; j++)
                Assert.False(CityLayout.Overlaps(lots[i].Corners(), lots[j].Corners()));
    }

    [Theory]
    [InlineData(8, false)]
    [InlineData(13, true)]
    [InlineData(20, true)]
    public void Buildings_spill_outside_the_walls_only_from_size_11(int population, bool spills)
    {
        var walls = Walls();
        var lots = Plan(population, walls: walls).Lots;
        Assert.Equal(spills, lots.Any(l => l.Outside));
        foreach (var lot in lots)
            Assert.All(lot.Corners(), c =>
                Assert.True(lot.Outside ? CityLayout.HexDistance(c.X, c.Z) > walls.Outer : CityLayout.HexDistance(c.X, c.Z) < walls.Radius));
    }

    [Fact]
    public void Size_16_spills_more_than_size_11_and_open_cities_never_spill()
    {
        Assert.True(Plan(20, walls: Walls()).Lots.Count(l => l.Outside) > Plan(12, walls: Walls()).Lots.Count(l => l.Outside));
        Assert.All(Plan(20).Lots, l => Assert.False(l.Outside));
    }

    [Fact]
    public void Size_16_has_taller_buildings()
    {
        foreach (var style in Enum.GetValues<CityStyle>())
            Assert.True(Plan(20, style).Lots.Max(l => l.Floors) > Plan(13, style).Lots.Max(l => l.Floors), style.ToString());
    }

    [Fact]
    public void Streets_toward_roads_and_the_gate_stay_clear()
    {
        var lots = Plan(20, CityStyle.Modern, Walls(), roads: 1).Lots;
        foreach (double angle in new[] { 0, 2 * Math.PI / 3 })
        {
            double ux = Math.Cos(angle), uz = Math.Sin(angle);
            Assert.DoesNotContain(lots, l => l.Corners().Any(c =>
                c.X * ux + c.Z * uz > 0 && Math.Abs(c.X * uz - c.Z * ux) < CityLayout.StreetHalfWidth));
        }
    }

    [Fact]
    public void Buildings_only_stand_where_the_ground_allows()
    {
        var lots = Plan(13, buildable: (x, _) => x < 0).Lots;
        Assert.NotEmpty(lots);
        Assert.All(lots, l => Assert.All(l.Corners(), c => Assert.True(c.X < 0)));
    }
}

public sealed class CityAppearanceTests(LoadedMods mods) : IClassFixture<LoadedMods>
{
    [Fact]
    public void Every_shipped_era_has_a_city_look()
    {
        var services = mods.Content.Services.Get<ICityAppearanceService>();
        Assert.All(mods.Content.Get<Ariel.Content.Schema.EraDef>().All, era =>
            Assert.Contains(services, s => s.Era(era.Id) != null));
    }
}
