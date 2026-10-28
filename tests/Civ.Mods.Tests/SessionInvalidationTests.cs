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

    /// <summary>
    /// The silent half of the counter: an action that writes the value the session already holds must leave the
    /// revision alone. Each step repeats what the step before it settled, so one shared session proves the whole set
    /// instead of building a simulation per case.
    /// </summary>
    [Fact]
    public void Actions_that_repeat_state_the_session_already_holds_leave_the_revision_alone()
    {
        var s = StackedSession();
        var unit = s.Unit!;

        s.SetMessage("Hold the line");
        int settled = s.Revision;
        s.SetMessage("Hold the line");
        Assert.Equal("Hold the line", s.Message);
        Assert.Equal(settled, s.Revision);

        s.SelectUnit(unit.Id);
        settled = s.Revision;
        s.SelectUnit(unit.Id);
        Assert.Equal(unit.Id, s.SelectedUnitId);
        Assert.Equal(settled, s.Revision);

        var city = Found(s);
        s.SelectCity(city.Id);
        settled = s.Revision;
        s.SelectCity(city.Id);
        Assert.Equal(city.Id, s.SelectedCityId);
        Assert.Equal(settled, s.Revision);

        s.ClearSelection();
        settled = s.Revision;
        s.ClearSelection();
        Assert.Null(s.Unit);
        Assert.Null(s.City);
        Assert.Equal(settled, s.Revision);
    }

    /// <summary>A click on the only unit of a tile selects what is already selected, so it is silent as well.</summary>
    [Fact]
    public void Clicking_the_same_single_unit_tile_twice_leaves_the_revision_alone()
    {
        var s = NewSession("military:warrior");
        var clicked = s.Unit!;
        s.SelectAt(clicked.Position);
        int settled = s.Revision;
        s.SelectAt(clicked.Position);
        Assert.Equal(clicked.Id, s.SelectedUnitId);
        Assert.Equal(settled, s.Revision);
    }

    /// <summary>The other half: an action that really changes the session bumps exactly once, in one shared session.</summary>
    [Fact]
    public void Actions_that_change_the_selection_or_the_message_bump_exactly_once()
    {
        var s = StackedSession();
        int settled = s.Revision;
        s.SetMessage("No path there");
        Assert.Equal("No path there", s.Message);
        Assert.Equal(settled + 1, s.Revision);

        var current = s.SelectedUnitId!.Value;
        var other = s.Sim.State.Units.First(u => u.Id != current);
        settled = s.Revision;
        s.SelectUnit(other.Id);
        Assert.Equal(other.Id, s.SelectedUnitId);
        Assert.Equal(settled + 1, s.Revision);

        var found = Found(s);
        var otherCity = new City { Id = s.Sim.State.NextCityId++, Owner = GameSession.Human, Position = s.Sim.State.Grid.HexOf(new TileId(41)) };
        s.Sim.State.AddCity(otherCity);
        s.SelectCity(found.Id);
        settled = s.Revision;
        s.SelectCity(otherCity.Id);
        Assert.Equal(otherCity.Id, s.SelectedCityId);
        Assert.Equal(settled + 1, s.Revision);

        s.SelectUnit(s.Sim.State.Units.First(u => u.Owner == GameSession.Human).Id);
        settled = s.Revision;
        s.ClearSelection();
        Assert.Null(s.Unit);
        Assert.Equal(settled + 1, s.Revision);
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
