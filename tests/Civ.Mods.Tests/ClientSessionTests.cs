using Civ.Client;
using Civ.Content;
using Civ.Game;
using Xunit;
using Schema = Civ.Content.Schema;

namespace Civ.Mods.Tests;

/// <summary>Client.Core behaviour against the full eight-mod ruleset, with no engine.</summary>
public sealed class ClientSessionTests(LoadedMods mods) : IClassFixture<LoadedMods>
{
    private GameSession NewSession(ulong seed = 3) => new(GameFactory.Create(mods.Content, seed));

    private static City Found(GameSession s)
    {
        var settler = s.Sim.State.Units.First(u => u.Owner == GameSession.Human && s.DefOf(u).CanFoundCity);
        s.SelectUnit(settler.Id);
        s.RunAction("found");
        return s.Sim.State.Cities.First(c => c.Owner == GameSession.Human);
    }

    [Fact]
    public void Catalog_offers_setup_choices_and_builds_settings_from_them()
    {
        var catalog = new GameCatalog(mods.Sources);
        Assert.Contains(catalog.WorldSizes, w => w.Id == "foundation:worldsize_standard");
        Assert.Equal("foundation:handicap_settler", catalog.Handicaps[0].Id);
        var leader = catalog.Leaders.First(l => l.Traits.Count > 0);
        Assert.False(string.IsNullOrEmpty(leader.CivilizationName));
        var options = catalog.DefaultOptions() with { Leader = leader.Id, Opponents = 3, Seed = 9 };
        var settings = catalog.SettingsFor(options);
        Assert.Equal(4, settings.PlayerCount);
        Assert.Equal(leader.Id, settings.Leaders[0]);
        Assert.Equal(4, settings.Civilizations.Distinct().Count());
        Assert.Equal(settings.Leaders, catalog.SettingsFor(options).Leaders);
    }

    [Fact]
    public void Start_creates_the_simulation_and_selects_a_starting_unit()
    {
        var catalog = new GameCatalog(mods.Sources);
        var session = GameSession.Start(catalog, catalog.DefaultOptions() with { WorldSize = "foundation:worldsize_duel", Opponents = 1 });
        Assert.Equal(2, session.Sim.State.Players.Count);
        Assert.NotNull(session.Unit);
        Assert.True(session.Sim.State.Units.Count(u => u.Owner == GameSession.Human) >= 2);
    }

    [Fact]
    public void Founding_a_city_through_the_unit_actions_creates_it_and_consumes_the_settler()
    {
        var s = NewSession();
        Assert.Contains(s.DescribeUnit(s.Unit)!.Actions, a => a.Id == "found" && a.Command != null);
        var city = Found(s);
        Assert.Equal("", s.Message);
        Assert.Equal(GameSession.Human, city.Owner);
        Assert.DoesNotContain(s.Sim.State.Units, u => s.DefOf(u).CanFoundCity && u.Owner == GameSession.Human);
    }

    [Fact]
    public void Selecting_cycles_through_the_units_then_the_city_on_a_tile()
    {
        var s = NewSession();
        var city = Found(s);
        s.ClearSelection();
        s.SelectAt(city.Position);
        Assert.NotNull(s.Unit);
        s.SelectAt(city.Position);
        Assert.Equal(city.Id, s.SelectedCityId);
        s.SelectAt(city.Position);
        Assert.NotNull(s.Unit);
        s.SelectAt(s.Sim.State.Grid.HexOf(new TileId(0)));
        Assert.Null(s.Unit);
    }

    [Fact]
    public void End_turn_advances_even_though_no_AI_plays_the_other_players()
    {
        var s = NewSession();
        int turn = s.Sim.State.Turn;
        s.EndTurn();
        Assert.Equal(turn + 1, s.Sim.State.Turn);
        s.EndTurn();
        Assert.Equal(turn + 2, s.Sim.State.Turn);
    }

    [Fact]
    public void Right_click_uses_the_pathfinder_and_standing_orders_continue_next_turn()
    {
        var s = NewSession();
        var warrior = s.Sim.State.Units.First(u => u.Owner == GameSession.Human && !s.DefOf(u).CanFoundCity);
        s.SelectUnit(warrior.Id);
        var target = FarLandTarget(s, warrior);
        var preview = s.PreviewAt(target)!;
        Assert.True(preview.Turns >= 2);
        Assert.Equal(target, preview.Path[^1]);
        s.RightClick(target);
        Assert.Equal("", s.Message);
        var after = warrior.Position;
        Assert.NotEqual(target, after);
        Assert.True(s.HasGoto(warrior.Id));
        s.EndTurn();
        Assert.NotEqual(after, warrior.Position);
    }

    private static Hex FarLandTarget(GameSession s, Unit unit)
    {
        var grid = s.Sim.State.Grid;
        var path = s.Sim.Content.Services.Get<IPathfindingService>().Single();
        foreach (var tile in grid.Range(grid.TileAt(unit.Position), 7).OrderByDescending(t => grid.Distance(grid.TileAt(unit.Position), t)))
            if (path.FindPath(s.Sim, unit.Id, grid.HexOf(tile)) is { } p && p.Steps.Count >= 5) return grid.HexOf(tile);
        throw new InvalidOperationException("no far land tile");
    }

    [Fact]
    public void Attack_preview_reports_win_odds_for_an_adjacent_enemy()
    {
        var s = NewSession();
        var warrior = s.Sim.State.Units.First(u => u.Owner == GameSession.Human && !s.DefOf(u).CanFoundCity);
        var spot = s.Sim.State.Grid.Neighbors(warrior.Position).First(h => s.Sim.Content.Services.Get<IMovementService>().Single()
            .CanEnter(s.Sim, warrior.Id, s.Sim.State.Grid.TileAt(warrior.Position), s.Sim.State.Grid.TileAt(h)) == null);
        var enemy = new Unit { Id = s.Sim.State.NextUnitId++, Owner = 1, Type = warrior.Type, Position = spot, MovesLeft = 120 };
        s.Sim.State.AddUnit(enemy);
        s.Sim.Content.Services.Get<IUnitService>().Single().Initialize(s.Sim, enemy.Id);
        s.Sim.State.SetWar(0, 1, true);
        s.SelectUnit(warrior.Id);
        var preview = s.PreviewAt(spot)!;
        Assert.NotNull(preview.Attack);
        Assert.InRange(preview.Attack!.WinPercent, 1, 99);
        Assert.StartsWith("Attack", preview.Summary);
        s.RightClick(spot);
        Assert.Equal("", s.Message);
        Assert.Single(s.Sim.Content.Services.Get<ICombatService>().Single().History(s.Sim));
    }

    [Fact]
    public void Workers_list_only_builds_the_world_service_accepts()
    {
        var s = NewSession();
        var city = Found(s);
        var worker = new Unit { Id = s.Sim.State.NextUnitId++, Owner = GameSession.Human, Type = "military:worker", Position = city.Position, MovesLeft = 120 };
        s.Sim.State.AddUnit(worker);
        s.Sim.Content.Services.Get<IUnitService>().Single().Initialize(s.Sim, worker.Id);
        foreach (var hex in WorkArea(s, city))
        {
            s.Sim.State.MoveUnit(worker.Id, hex);
            s.SelectUnit(worker.Id);
            var builds = s.DescribeUnit(worker)!.Actions.Where(a => a.Id.StartsWith("build:")).ToList();
            if (builds.Count == 0) continue;
            Assert.All(builds, b => Assert.Null(b.Command!.Validate(s.Sim)));
            s.RunAction(builds[0].Id);
            Assert.Equal("", s.Message);
            return;
        }
        Assert.Fail("no tile offered a build");
    }

    private static IEnumerable<Hex> WorkArea(GameSession s, City city) => s.WorkArea(city).Where(h => h != city.Position);

    [Fact]
    public void Fortify_changes_the_unit_activity()
    {
        var s = NewSession();
        var warrior = s.Sim.State.Units.First(u => u.Owner == GameSession.Human && !s.DefOf(u).CanFoundCity);
        s.SelectUnit(warrior.Id);
        s.RunAction("fortify");
        Assert.Equal(UnitActivity.Fortify, s.ActivityOf(warrior));
    }

    [Fact]
    public void Earned_promotions_are_available_actions_and_can_be_applied()
    {
        var s = NewSession();
        var warrior = s.Sim.State.Units.First(u => u.Owner == GameSession.Human && !s.DefOf(u).CanFoundCity);
        warrior.Experience = 20;
        warrior.Health = 70;
        s.SelectUnit(warrior.Id);
        var action = s.DescribeUnit(warrior)!.Actions.First(a => a.Id.StartsWith("promote:"));
        s.RunAction(action.Id);
        Assert.Equal("", s.Message);
        Assert.Contains(action.Id[8..], warrior.Promotions);
        Assert.True(warrior.Health > 70);
    }

    [Fact]
    public void Civics_panel_previews_without_mutation_and_adoption_tracks_anarchy_through_session_turns()
    {
        var s = new GameSession(GameFactory.Create(mods.Content, 3, "peoples:leader_julius_caesar", "peoples:leader_gjergj_kastrioti"));
        var city = Found(s);
        s.MyTeam.Techs.Add("research:code_of_laws");
        var before = s.Sim.ComputeHash();
        var panel = s.Government(["government:despotism"]);
        var government = panel.Groups.Single(g => g.Id == "government:civicoption_government");
        Assert.Equal("government:despotism", government.Selected);
        var preview = s.PreviewCivics(["government:despotism"]);
        Assert.Null(preview.Blocked);
        Assert.True(preview.AnarchyTurns > 0);
        Assert.Equal(before, s.Sim.ComputeHash());
        Assert.True(s.AdoptCivics(["government:despotism"]));
        Assert.Equal("government:despotism", s.Government().Groups.Single(g => g.Id == government.Id).Selected);
        Assert.Equal(preview.AnarchyTurns, s.Government().AnarchyTurns);
        Assert.True(s.DescribeCity(city)!.Disorder);
        Assert.False(s.AdoptCivics(["government:slavery"]));
        Assert.Contains("anarchy", s.Message);
        for (int i = 0; i < preview.AnarchyTurns; i++) s.EndTurn();
        Assert.Equal(0, s.Government().AnarchyTurns);
        Assert.False(s.DescribeCity(city)!.Disorder);
    }

    [Fact]
    public void Visibility_shows_the_start_and_hides_the_far_side()
    {
        var s = NewSession();
        var start = s.Sim.State.Units.First(u => u.Owner == GameSession.Human).Position;
        Assert.Equal(Sight.Visible, s.SightOf(start));
        var far = s.Sim.State.Plots.Select((p, i) => (p, i)).First(t => s.SightOf(t.p) == Sight.Hidden);
        Assert.Equal("Unexplored", s.DescribeTile(s.Sim.State.Grid.HexOf(new TileId(far.i)))!.Title);
        Assert.Contains(s.VisibleUnits(), u => u.Owner == GameSession.Human);
    }

    [Fact]
    public void Picking_resolves_wrap_images_to_the_canonical_tile()
    {
        var s = NewSession();
        var grid = s.Sim.State.Grid;
        var hex = grid.HexOf(grid.TileAt(5, 7));
        var (x, z) = hex.ToPixel(1);
        Assert.Equal(hex, s.Pick(x, z));
        Assert.Equal(hex, s.Pick(x + s.WrapPeriod, z));
        Assert.Equal(hex, s.Pick(x - s.WrapPeriod, z));
        Assert.Null(s.Pick(x, -10));
    }

    [Fact]
    public void City_screen_reports_ledgers_and_the_production_queue_can_be_edited()
    {
        var s = NewSession();
        var city = Found(s);
        var screen = s.DescribeCity(city)!;
        Assert.Equal(1, screen.Population);
        Assert.NotEmpty(screen.FoodLedger);
        Assert.True(screen.FoodThreshold > 0);
        var choices = s.ProductionChoices(city);
        var available = choices.Where(c => c.Available).ToList();
        Assert.Contains(available, c => c.Kind == "cities:unit");
        Assert.Contains(choices, c => !c.Available && c.Blockers.Count > 0 && c.Tooltip.Length > 0);

        var first = available[0]; var second = available[1]; var third = available[2];
        Assert.True(s.Enqueue(city, first.Kind, first.Id, append: false));
        Assert.True(s.Enqueue(city, second.Kind, second.Id, append: true));
        Assert.True(s.Enqueue(city, third.Kind, third.Id, append: true));
        Assert.Equal([first.Id, second.Id, third.Id], city.ProductionQueue.Select(o => o.ContentId));
        s.EndTurn();
        long progress = city.ProductionQueue[0].Progress;
        Assert.True(progress > 0);
        Assert.True(s.MoveInQueue(city, 0, 1));
        Assert.Equal(progress, city.ProductionQueue[1].Progress);
        Assert.True(s.MoveInQueue(city, 1, -1));
        Assert.True(s.MoveInQueue(city, 2, -1));
        Assert.Equal([first.Id, third.Id, second.Id], city.ProductionQueue.Select(o => o.ContentId));
        Assert.True(s.RemoveFromQueue(city, 0));
        Assert.Equal([third.Id, second.Id], city.ProductionQueue.Select(o => o.ContentId));
        Assert.Equal(2, s.DescribeCity(city)!.Queue.Count);
    }

    [Fact]
    public void Worked_tiles_and_specialists_can_be_reassigned()
    {
        var s = NewSession();
        var city = Found(s);
        var worked = city.Worked.First();
        s.ToggleTile(city, worked);
        Assert.Equal("", s.Message);
        Assert.DoesNotContain(worked, city.Worked);
        Assert.Equal(1, city.Specialists.GetValueOrDefault(GameSession.CitizenSpecialist));
        s.ToggleTile(city, worked);
        Assert.Contains(worked, city.Worked);
        Assert.Equal(TileMark.Worked, MarkWithCity(s, city, worked));
    }

    private static TileMark MarkWithCity(GameSession s, City city, Hex hex) { s.SelectCity(city.Id); return s.MarkOf(hex); }

    [Fact]
    public void Research_choices_set_the_team_target_and_the_summary_tracks_it()
    {
        var s = NewSession();
        Assert.Null(s.Summary().Research);
        var choice = s.ResearchChoices()[0];
        Assert.True(s.Research(choice.Id));
        var research = s.Summary().Research!;
        Assert.Equal(choice.Id, research.Id);
        Assert.Equal(choice.Cost, research.Cost);
    }

    [Fact]
    public void Shifting_the_commerce_slider_moves_percent_between_gold_and_research()
    {
        var s = NewSession();
        var before = s.Summary().Rates;
        Assert.True(s.ShiftRate(RateKind.Research, 10));
        var after = s.Summary().Rates;
        Assert.Equal(before.Research + 10, after.Research);
        Assert.Equal(before.Gold - 10, after.Gold);
        Assert.False(s.ShiftRate(RateKind.Research, 7));
        Assert.NotEqual("", s.Message);
    }
}
