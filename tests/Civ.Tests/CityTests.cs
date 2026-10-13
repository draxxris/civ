using Civ.BaseGame;
using Civ.Game;
using Xunit;

namespace Civ.Tests;

public class CityTests
{
    private static (Simulation sim, City city) NewCity()
    {
        var sim = TestGame.Sim();
        Assert.True(sim.TryApply(new FoundCityCommand { Player = 0, Position = TestGame.FindLand(sim) }, out _));
        return (sim, sim.State.Cities[0]);
    }

    private static void Grow(Simulation sim, City city, int toPopulation)
    {
        while (city.Population < toPopulation) { city.FoodStored = CityRules.FoodToGrow(city.Population) + 100; new GrowthSystem().Run(sim); }
    }

    [Fact]
    public void NewCityHasOneCitizenWorkingTheBestTile()
    {
        var (sim, city) = NewCity();
        Assert.Equal(1, city.Population);
        var tile = Assert.Single(city.Worked);
        Assert.Contains(tile, CityRules.WorkableTiles(sim, city));
        var best = CityRules.WorkableTiles(sim, city).Max(h => CityRules.TileScore(sim, h));
        Assert.Equal(best, CityRules.TileScore(sim, tile));
    }

    [Fact]
    public void YieldsComeFromWorkedTilesNotAllNeighbours()
    {
        var (sim, city) = NewCity();
        var y = Yields.OfCity(sim, city);
        var expected = new Yield(0, 1, 1) + Yields.OfTile(sim, city.Position) + Yields.OfTile(sim, city.Worked[0]);
        Assert.Equal(expected, y);
    }

    [Fact]
    public void FoodSurplusGrowsTheCityAndAddsAWorkedTile()
    {
        var (sim, city) = NewCity();
        Grow(sim, city, 3);
        Assert.Equal(3, city.Population);
        Assert.Equal(3, city.Worked.Count + CityRules.SpecialistCount(city));
    }

    [Fact]
    public void StarvationShrinksTheCity()
    {
        var (sim, city) = NewCity();
        Grow(sim, city, 3);
        // Put everyone on specialists that produce no food so the city starves.
        CityRules.Assign(city, [], new Dictionary<string, int> { ["base:citizen"] = 3 });
        sim.State.PlotAt(city.Position).Terrain = "base:desert";
        new GrowthSystem().Run(sim);
        Assert.Equal(2, city.Population);
        Assert.Equal(2, CityRules.SpecialistCount(city));
    }

    [Fact]
    public void AssignmentIsValidated()
    {
        var (sim, city) = NewCity();
        Grow(sim, city, 2);
        var free = CityRules.WorkableTiles(sim, city).Where(h => !city.Worked.Contains(h)).ToArray();

        bool Try(Hex[] tiles, Dictionary<string, int> spec) =>
            sim.TryApply(new SetCityWorkCommand { Player = 0, CityId = city.Id, Tiles = tiles, Specialists = spec }, out _);

        Assert.False(Try([free[0]], new()));                                          // one citizen unassigned
        Assert.False(Try([free[0], free[0]], new()));                                 // duplicate
        Assert.False(Try([free[0], new Hex(99, 99)], new()));                         // outside radius
        Assert.False(Try([free[0]], new() { ["base:priest"] = 1 }));                  // no priest slot
        Assert.True(Try([free[0]], new() { ["base:citizen"] = 1 }));                  // plain citizen needs no slot
        Assert.Equal([free[0]], city.Worked);
        Assert.Equal(1, city.Specialists["base:citizen"]);
    }

    [Fact]
    public void BuildingsAndCivicsProvideSpecialistSlots()
    {
        var (sim, city) = NewCity();
        Grow(sim, city, 3);
        Assert.Equal(0, CityRules.Slots(sim, city, "base:scientist"));

        city.Buildings.Add("base:library");
        Assert.Equal(2, CityRules.Slots(sim, city, "base:scientist"));

        Assert.False(sim.TryApply(new SetCivicCommand { Player = 0, Civic = "base:caste_system" }, out _)); // needs writing
        sim.TeamOf(0).Techs.Add("base:writing");
        Assert.True(sim.TryApply(new SetCivicCommand { Player = 0, Civic = "base:caste_system" }, out _));
        Assert.Equal(int.MaxValue, CityRules.Slots(sim, city, "base:scientist"));
        Assert.Equal("base:caste_system", sim.State.Players[0].Civics["labor"]);
        Assert.Equal("base:despotism", sim.State.Players[0].Civics["government"]); // other categories untouched

        // Unassign everyone into scientists and check the yield.
        var before = Yields.OfCity(sim, city).Commerce;
        Assert.True(sim.TryApply(new SetCityWorkCommand { Player = 0, CityId = city.Id, Tiles = [], Specialists = new() { ["base:scientist"] = 3 } }, out var err), err);
        Assert.Equal(Yields.OfTile(sim, city.Position).Commerce + 1 + 2 + 9, // floor + library + 3 scientists x 3
             Yields.OfCity(sim, city).Commerce);
        Assert.NotEqual(before, Yields.OfCity(sim, city).Commerce);
    }

    [Fact]
    public void LosingSlotsTurnsSpecialistsBackIntoCitizens()
    {
        var (sim, city) = NewCity();
        Grow(sim, city, 2);
        sim.TeamOf(0).Techs.Add("base:writing");
        sim.TryApply(new SetCivicCommand { Player = 0, Civic = "base:caste_system" }, out _);
        Assert.True(sim.TryApply(new SetCityWorkCommand { Player = 0, CityId = city.Id, Tiles = [], Specialists = new() { ["base:artist"] = 2 } }, out _));

        sim.TryApply(new SetCivicCommand { Player = 0, Civic = "base:tribalism" }, out _);
        Assert.False(city.Specialists.ContainsKey("base:artist"));
        Assert.Equal(2, city.Specialists["base:citizen"]);
    }

    [Fact]
    public void ArtistsGenerateCulture()
    {
        var (sim, city) = NewCity();
        Grow(sim, city, 2);
        sim.TeamOf(0).Techs.Add("base:writing");
        sim.TryApply(new SetCivicCommand { Player = 0, Civic = "base:caste_system" }, out _);
        sim.TryApply(new SetCityWorkCommand { Player = 0, CityId = city.Id, Tiles = [], Specialists = new() { ["base:artist"] = 2 } }, out _);
        new CultureSystem().Run(sim);
        Assert.Equal(1 + 6, city.Culture);
    }

    [Fact]
    public void CitiesCannotWorkEachOthersTiles()
    {
        var sim = TestGame.Sim();
        var a = TestGame.FindLand(sim);
        var b = sim.State.Grid.Tiles.Select(sim.State.Grid.HexOf).Where(h => h.DistanceTo(a) == 3 && !sim.TerrainAt(h).IsWater && sim.TerrainAt(h).Passable)
            .OrderBy(h => h.Q).ThenBy(h => h.R).FirstOrDefault();
        if (b == default && !sim.State.Grid.Contains(b)) return; // map happens to have no land 3 tiles away
        sim.TryApply(new FoundCityCommand { Player = 0, Position = a }, out _);
        sim.TryApply(new FoundCityCommand { Player = 0, Position = b }, out _);
        var worked = sim.State.Cities[0].Worked.Concat(sim.State.Cities[1].Worked).ToList();
        Assert.Equal(worked.Count, worked.Distinct().Count());
        Assert.DoesNotContain(sim.State.Cities[1].Position, CityRules.WorkableTiles(sim, sim.State.Cities[0]));
    }

    [Fact]
    public void CityCommandsRoundTripThroughCodec()
    {
        var sim = TestGame.Sim();
        var codec = new CommandCodec(sim.Content);
        var cmd = new SetCityWorkCommand { Player = 0, CityId = 1, Tiles = [new Hex(1, 0)], Specialists = new() { ["base:priest"] = 1 } };
        var back = Assert.IsType<SetCityWorkCommand>(codec.Decode(codec.Encode(cmd)));
        Assert.Equal(cmd.Tiles, back.Tiles);
        Assert.Equal(1, back.Specialists["base:priest"]);
        Assert.IsType<SetCivicCommand>(codec.Decode(codec.Encode(new SetCivicCommand { Player = 0, Civic = "base:monarchy" })));
    }
}
