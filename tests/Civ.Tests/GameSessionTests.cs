using Civ.BaseGame;
using Civ.Client;
using Civ.Game;
using Xunit;

namespace Civ.Tests;

public class GameSessionTests
{
    private static GameSession NewSession() => GameSession.Start([], mapRadius: 8, seed: 3);

    private static City FoundCity(GameSession s)
    {
        var settler = s.Sim.State.Units.First(u => u.Owner == GameSession.Human && s.Sim.Content.Units.Get(u.Type).CanFoundCity);
        s.Submit(new FoundCityCommand { Player = GameSession.Human, Position = settler.Position, UnitId = settler.Id });
        return s.Sim.State.Cities.First(c => c.Owner == GameSession.Human);
    }

    [Fact]
    public void StartPlacesAHumanSettlerAndWarriorAndTheAiCivsActImmediately()
    {
        var s = NewSession();
        Assert.Equal(2, s.Sim.State.Units.Count(u => u.Owner == GameSession.Human));
        Assert.Equal(2, s.Sim.State.Cities.Count); // two AI civilizations founded theirs
        Assert.NotNull(s.Unit);
    }

    [Fact]
    public void SelectingCyclesThroughUnitsThenTheCity()
    {
        var s = NewSession();
        var city = FoundCity(s); // settler consumed, warrior remains on the same tile
        s.ClearSelection();
        s.SelectAt(city.Position);
        Assert.NotNull(s.Unit);
        s.SelectAt(city.Position);
        Assert.Equal(city.Id, s.SelectedCityId);
        s.SelectAt(city.Position);
        Assert.NotNull(s.Unit);
        s.SelectAt(new Hex(100, 100));
        Assert.Null(s.Unit);
    }

    [Fact]
    public void ClickingTilesTogglesCitizensAndSwapsWhenEveryoneIsBusy()
    {
        var s = NewSession();
        var city = FoundCity(s);
        var worked = city.Worked.Single();
        s.SelectedCityId = city.Id;

        s.LeftClick(worked); // unassign -> plain citizen
        Assert.Empty(city.Worked);
        Assert.Equal(1, city.Specialists["base:citizen"]);

        var free = CityRules.WorkableTiles(s.Sim, city).First();
        s.LeftClick(free); // takes the plain citizen
        Assert.Equal([free], city.Worked);
        Assert.Empty(city.Specialists);

        var other = CityRules.WorkableTiles(s.Sim, city).First(h => h != free);
        s.LeftClick(other); // nobody free: moves the citizen
        Assert.Equal([other], city.Worked);
        Assert.Equal("", s.Message);
    }

    [Fact]
    public void SpecialistButtonsRespectSlots()
    {
        var s = NewSession();
        var city = FoundCity(s);
        s.Me.Techs.Add("base:monarchy");
        s.SelectedCityId = city.Id;

        s.ChangeSpecialist(city, "base:priest", +1);
        Assert.StartsWith("Can't do that", s.Message); // no slot yet
        Assert.Empty(city.Specialists);

        city.Buildings.Add("base:temple");
        s.ChangeSpecialist(city, "base:priest", +1);
        Assert.Equal(1, city.Specialists["base:priest"]);
        Assert.Empty(city.Worked);

        s.ChangeSpecialist(city, "base:priest", -1);
        Assert.Equal(1, city.Specialists["base:citizen"]);
    }

    [Fact]
    public void RightClickWalksAUnitTowardsATile()
    {
        var s = NewSession();
        var warrior = s.Sim.State.Units.First(u => u.Owner == GameSession.Human && s.Sim.Content.Units.Get(u.Type).Strength > 0);
        var target = warrior.Position.Neighbors().First(n => new MoveUnitCommand { Player = 0, UnitId = warrior.Id, To = n }.Validate(s.Sim) == null);
        s.SelectedUnitId = warrior.Id;
        s.RightClick(target);
        Assert.Equal(target, warrior.Position);
    }

    [Fact]
    public void FullTurnLoopThroughTheSessionAdvancesTheGame()
    {
        var s = NewSession();
        FoundCity(s);
        s.Submit(new SetResearchCommand { Player = 0, Tech = "base:mining" });
        for (int i = 0; i < 5; i++) s.EndTurn();
        Assert.Equal(6, s.Sim.State.Turn);
        var before = s.Version;
        s.Message = "x";
        Assert.NotEqual(before, s.Version);
    }
}
