using Civ.Client;
using Civ.Game;
using Xunit;

namespace Civ.Mods.Tests;

/// <summary>The revision is the front end's only "something changed" signal, so a setter that would write the value it
/// already holds must stay silent. <c>Submit</c> is the exception: even a rejected command writes <c>Message</c>.</summary>
public sealed class SessionInvalidationTests(LoadedMods mods) : IClassFixture<LoadedMods>
{
    // Scenario geography puts every unit on one tile, so a map click cycles a deterministic stack.
    private GameSession NewSession(params string[] types)
    {
        var sim = Simulation.CreateScenario(mods.Content, GameFactory.Settings(3), new(12, 8, true),
            Enumerable.Range(0, 96).Select(_ => new Plot { Terrain = "world:grass" }).ToArray(),
            game =>
            {
                var position = game.State.Grid.HexOf(new TileId(39));
                foreach (var type in types)
                    game.State.AddUnit(new Unit { Id = game.State.NextUnitId++, Owner = GameSession.Human, Type = type, Position = position });
                foreach (var unit in game.State.Units) game.Content.Services.Get<IUnitService>().Single().Initialize(game, unit.Id);
            });
        return new GameSession(sim);
    }

    private GameSession StackedSession() => NewSession("military:settler", "military:warrior");

    private static City Found(GameSession s)
    {
        var settler = s.Sim.State.Units.First(u => u.Owner == GameSession.Human && s.DefOf(u).CanFoundCity);
        s.SelectUnit(settler.Id);
        s.RunAction("found");
        return s.Sim.State.Cities.First(c => c.Owner == GameSession.Human);
    }

    /// <summary>Each row repeats the action it has just performed, so the revision must not move on the repeat.</summary>
    [Theory]
    [InlineData("set the same message")]
    [InlineData("clear an empty selection")]
    [InlineData("select the current unit")]
    [InlineData("select the current city")]
    [InlineData("click the same single-unit tile twice")]
    public void An_action_that_changes_nothing_leaves_the_revision_alone(string action)
    {
        var s = action is "set the same message" or "click the same single-unit tile twice"
            ? NewSession("military:warrior")
            : StackedSession();
        int revision;
        switch (action)
        {
            case "set the same message":
                s.SetMessage("Hold the line");
                revision = s.Revision;
                s.SetMessage("Hold the line");
                Assert.Equal("Hold the line", s.Message);
                break;
            case "clear an empty selection":
                s.ClearSelection();
                revision = s.Revision;
                s.ClearSelection();
                Assert.Null(s.Unit);
                Assert.Null(s.City);
                break;
            case "select the current unit":
                var unit = s.Unit!;
                s.SelectUnit(unit.Id);
                revision = s.Revision;
                s.SelectUnit(unit.Id);
                Assert.Equal(unit.Id, s.SelectedUnitId);
                break;
            case "select the current city":
                var city = Found(s);
                s.SelectCity(city.Id);
                revision = s.Revision;
                s.SelectCity(city.Id);
                Assert.Equal(city.Id, s.SelectedCityId);
                break;
            case "click the same single-unit tile twice":
                var clicked = s.Unit!;
                s.SelectAt(clicked.Position);
                revision = s.Revision;
                s.SelectAt(clicked.Position);
                Assert.Equal(clicked.Id, s.SelectedUnitId);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(action), action, "unknown case");
        }

        Assert.Equal(revision, s.Revision);
    }

    /// <summary>Each row changes the selection or the message once, which is exactly one revision.</summary>
    [Theory]
    [InlineData("select a different unit")]
    [InlineData("select a different city")]
    [InlineData("clear an active selection")]
    [InlineData("set a new message")]
    public void An_action_that_changes_something_bumps_once(string action)
    {
        var s = action == "set a new message" ? NewSession("military:warrior") : StackedSession();
        int revision;
        switch (action)
        {
            case "select a different unit":
                var current = s.SelectedUnitId!.Value;
                var other = s.Sim.State.Units.First(u => u.Id != current);
                revision = s.Revision;
                s.SelectUnit(other.Id);
                Assert.Equal(other.Id, s.SelectedUnitId);
                break;
            case "select a different city":
                var found = Found(s);
                var otherCity = new City { Id = s.Sim.State.NextCityId++, Owner = GameSession.Human, Position = s.Sim.State.Grid.HexOf(new TileId(41)) };
                s.Sim.State.AddCity(otherCity);
                s.SelectCity(found.Id);
                revision = s.Revision;
                s.SelectCity(otherCity.Id);
                Assert.Equal(otherCity.Id, s.SelectedCityId);
                break;
            case "clear an active selection":
                s.SelectUnit(s.Unit!.Id);
                revision = s.Revision;
                s.ClearSelection();
                Assert.Null(s.Unit);
                break;
            case "set a new message":
                revision = s.Revision;
                s.SetMessage("No path there");
                Assert.Equal("No path there", s.Message);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(action), action, "unknown case");
        }

        Assert.Equal(revision + 1, s.Revision);
    }

    // --- Submit is the exception, and a click that cycles or a turn that advances always bumps ---

    [Fact]
    public void A_rejected_command_still_bumps()
    {
        var s = NewSession("military:warrior");
        int revision = s.Revision;

        bool ok = s.Submit(new MoveUnitPathCommand { Player = GameSession.Human, Unit = 999_999, Path = [] });

        Assert.False(ok);
        Assert.StartsWith("Can't do that:", s.Message);
        Assert.Equal(revision + 1, s.Revision);
    }

    [Fact]
    public void Clicking_a_tile_with_two_units_cycles_the_stack_and_bumps_on_every_click()
    {
        var s = StackedSession();
        var tile = s.Sim.State.Units.First(u => u.Owner == GameSession.Human).Position;
        s.ClearSelection();
        int revision = s.Revision;
        var seen = new List<int>();

        for (int click = 0; click < 2; click++)
        {
            s.SelectAt(tile);
            Assert.Equal(revision + click + 1, s.Revision);
            seen.Add(s.SelectedUnitId!.Value);
        }

        s.SelectAt(tile); // Back to the first unit of the stack.
        Assert.Equal(revision + 3, s.Revision);
        Assert.Equal(2, seen.Distinct().Count());
        Assert.Equal(seen[0], s.SelectedUnitId);
    }

    [Fact]
    public void Ending_the_turn_bumps_even_though_the_same_unit_stays_selected()
    {
        var s = NewSession("military:warrior");
        var selected = s.SelectedUnitId!.Value;
        int revision = s.Revision;

        s.EndTurn();

        Assert.Equal(selected, s.SelectedUnitId);
        // EndTurn's Submit and ContinueOrders bump; the unchanged selection does not add a third.
        Assert.Equal(revision + 2, s.Revision);
    }
}
