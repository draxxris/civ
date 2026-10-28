using Civ.Client;
using Civ.Content;
using Civ.Game;
using Civ.Modding;
using Xunit;
using Schema = Civ.Content.Schema;

namespace Civ.Mods.Tests;

/// <summary>Client.Core behaviour against the full eight-mod ruleset, with no engine.</summary>
public sealed class ClientSessionTests(LoadedMods mods) : IClassFixture<LoadedMods>
{
    private GameSession NewSession(ulong seed = 3) => new(GameFactory.Create(mods.Content, seed));

    [Fact]
    public void Summary_keeps_the_turn_and_uses_the_calendar_formatted_date()
    {
        var session = NewSession();
        session.Sim.State.Turn = 692;

        var summary = session.Summary();

        Assert.Equal(692, summary.Turn);
        Assert.Equal("Winter, 1845 CE", summary.CalendarDate?.FormattedDate);
    }

    [Fact]
    public void Summary_keeps_the_turn_when_the_calendar_has_no_date()
    {
        var content = ModLoader.Build(mods.Sources.Append(new ModSource
        {
            Manifest = new() { Id = "client_calendar_probe", Version = "1", Dependencies = ["foundation"] },
            Root = mods.Root,
            Instance = new EmptyCalendarSpeed(),
        }));
        var sim = Simulation.CreateScenario(content, GameFactory.Settings() with { GameSpeed = "client_calendar_probe:empty" },
            new(2, 2), Enumerable.Range(0, 4).Select(_ => new Plot { Terrain = "world:grass" }).ToArray());
        var summary = new GameSession(sim).Summary();

        Assert.Equal(1, summary.Turn);
        Assert.Null(summary.CalendarDate);
    }

    private sealed class EmptyCalendarSpeed : IMod
    {
        public void Register(IModContext context) => context.Content<Schema.GameSpeedDef>().Register(new()
        {
            Id = "client_calendar_probe:empty",
        });
    }
    // City panels need real founding and turn systems, not procedural geography or free-unit placement.
    private GameSession CitySession(params string[] leaders)
    {
        var sim = Simulation.CreateScenario(mods.Content, GameFactory.Settings(3, leaders), new(12, 8, true),
            Enumerable.Range(0, 96).Select(_ => new Plot { Terrain = "world:grass" }).ToArray(),
            game =>
            {
                var position = game.State.Grid.HexOf(new TileId(39));
                game.State.AddUnit(new Unit { Id = game.State.NextUnitId++, Owner = 0, Type = "military:settler", Position = position });
                game.State.AddUnit(new Unit { Id = game.State.NextUnitId++, Owner = 0, Type = "military:warrior", Position = position });
                foreach (var unit in game.State.Units) game.Content.Services.Get<IUnitService>().Single().Initialize(game, unit.Id);
            });
        return new GameSession(sim);
    }

    private static City Found(GameSession s)
    {
        var settler = s.Sim.State.Units.First(u => u.Owner == GameSession.Human && s.DefOf(u).CanFoundCity);
        s.SelectUnit(settler.Id);
        s.RunAction("found");
        return s.Sim.State.Cities.First(c => c.Owner == GameSession.Human);
    }

    private static City CaptureForHuman(GameSession session, int population = 4)
    {
        var sim = session.Sim;
        var city = new City
        {
            Id = sim.State.NextCityId++, Owner = 1, Name = "Captured City", Population = population,
            Position = sim.State.Grid.HexOf(new TileId(70)),
        };
        city.Extensions.Set(sim.Content, new CityRuleState { TurnAcquired = sim.State.Turn, HighestPopulation = population });
        sim.State.AddCity(city);
        var plot = sim.State.PlotAt(city.Position);
        plot.Owner = city.Owner;
        plot.Culture[city.Owner] = 100;
        sim.State.SetWar(GameSession.Human, city.Owner, true);
        sim.Content.Services.Get<ICityCaptureService>().Single().Capture(sim, city.Id, GameSession.Human);
        return sim.State.CityById(city.Id);
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
    public void Resource_depletion_is_a_default_off_catalog_choice_and_survives_setup_and_reload()
    {
        const string id = "foundation:gameoption_resource_depletion";
        var catalog = new GameCatalog(mods.Sources);
        Assert.False(Assert.Single(catalog.GameOptions, o => o.Id == id).Default);
        Assert.False(catalog.SettingsFor(catalog.DefaultOptions()).GameOptions[id]);
        var options = catalog.DefaultOptions() with
        {
            WorldSize = "foundation:worldsize_duel", Opponents = 1,
            GameOptions = new Dictionary<string, bool> { [id] = true },
        };
        var sim = GameSession.Start(catalog, options).Sim;
        Assert.True(sim.IsOptionEnabled(id));
        var loaded = SaveGame.Load(catalog.Content, SaveGame.Save(sim));
        Assert.True(loaded.IsOptionEnabled(id));
        Assert.Equal(sim.ComputeHash(), loaded.ComputeHash());
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
        Assert.Equal(preview.Path.Count, preview.StepTurns.Count);
        Assert.Equal(preview.StepTurns[^1], preview.Turns);
        var reachableThisTurn = preview.Path.Where((_, i) => preview.StepTurns[i] == 1).Last();
        s.RightClick(target);
        Assert.Equal("", s.Message);
        var after = warrior.Position;
        Assert.Equal(reachableThisTurn, after);
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
    public void Workers_offer_a_build_that_dispatches()
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
            s.RunAction(builds[0].Id);
            Assert.Equal("", s.Message);
            return;
        }
        Assert.Fail("no tile offered a build");
    }

    private static IEnumerable<Hex> WorkArea(GameSession s, City city) => s.WorkArea(city).Where(h => h != city.Position);

    [Fact]
    public void Pillage_action_is_available_for_a_valid_military_unit()
    {
        var sim = GameFactory.Scenario(mods.Content);
        var type = mods.Content.Get<Schema.UnitDef>().All.First(u => u.Pillage && u.Domain == "military:domain_land").Id;
        var unit = Add(sim, type);
        var improvement = mods.Content.Get<Schema.ImprovementDef>().All.First(i => !i.Permanent && !i.Goody && i.ImprovementPillage is not null);
        var plot = sim.State.PlotAt(unit.Position);
        plot.Owner = 1; plot.Improvement = improvement.Id;
        sim.State.SetWar(0, 1, true);
        var session = new GameSession(sim);
        session.SelectUnit(unit.Id);

        var action = session.DescribeUnit(unit)!.Actions.SingleOrDefault(a => a.Id == "pillage");
        var command = Assert.IsType<PillageCommand>(action?.Command);
        Assert.Equal(unit.Id, command.Unit);
        Assert.Equal(unit.Owner, command.Player);
        session.RunAction("pillage");
        Assert.Equal(improvement.ImprovementPillage, plot.Improvement);
        var pillage = Assert.Single(sim.Events.Published.OfType<PlotPillaged>());
        Assert.Equal(unit.Id, pillage.Unit);
        Assert.Equal(unit.Owner, pillage.Player);
        Assert.Equal(sim.State.Grid.TileAt(unit.Position), pillage.Plot);
        Assert.Equal(improvement.Id, pillage.Improvement);
        Assert.Equal(improvement.ImprovementPillage, pillage.ReplacementImprovement);
    }

    /// <summary>The three activities a land combat unit can take are all offered as actions carrying the activity, and
    /// running one sets it. Intercept and blockade need aircraft and ships, and are covered by
    /// <see cref="Aircraft_panel_dispatches_patrol_and_recon_actions"/> and
    /// <c>NetworkTransitTests.Client_blockade_action_executes_a_real_order_and_disappears_when_unavailable</c>.</summary>
    [Theory]
    [InlineData("fortify", UnitActivity.Fortify)]
    [InlineData("sleep", UnitActivity.Sleep)]
    [InlineData("awake", UnitActivity.Awake)]
    public void Activity_actions_carry_their_activity_and_change_the_unit(string actionId, UnitActivity activity)
    {
        var s = NewSession();
        var warrior = s.Sim.State.Units.First(u => u.Owner == GameSession.Human && !s.DefOf(u).CanFoundCity);
        s.SelectUnit(warrior.Id);
        var offered = Assert.Single(s.DescribeUnit(warrior)!.Actions, a => a.Id == actionId);

        var command = Assert.IsType<SetUnitActivityCommand>(offered.Command);
        Assert.Equal(warrior.Id, command.Unit);
        Assert.Equal(activity, command.Activity);
        s.RunAction(actionId);

        Assert.Equal(activity, s.ActivityOf(warrior));
    }

    [Fact]
    public void Aircraft_panel_dispatches_patrol_and_recon_actions()
    {
        var sim = GameFactory.Scenario(mods.Content);
        var plane = new Unit { Id = sim.State.NextUnitId++, Owner = 0, Type = "military:fighter", Position = sim.State.Grid.HexOf(new(39)) };
        sim.State.AddUnit(plane);
        sim.Content.Services.Get<IUnitService>().Single().Initialize(sim, plane.Id);
        sim.Content.Services.Get<IVisibilityService>().Single().Recompute(sim);
        var session = new GameSession(sim);
        session.SelectUnit(plane.Id);
        Assert.Contains(session.DescribeUnit(plane)!.Actions, a => a.Id == "intercept");
        session.RunAction("intercept");
        Assert.Equal(UnitActivity.Intercept, session.ActivityOf(plane));
        session.SelectUnit(plane.Id);
        var recon = session.DescribeUnit(plane)!.Actions.First(a => a.Command is AirMissionCommand { Kind: AirMissionKind.Recon });
        session.RunAction(recon.Id);
        Assert.Equal("", session.Message); Assert.Equal(0, plane.MovesLeft);
        Assert.Single(sim.Events.Published.OfType<AirMissionResolved>());
    }

    private static Unit Add(Simulation sim, string type, int tile = 39, int owner = 0)
    {
        var unit = new Unit { Id = sim.State.NextUnitId++, Owner = owner, Type = type, Position = sim.State.Grid.HexOf(new(tile)) };
        sim.State.AddUnit(unit); sim.Content.Services.Get<IUnitService>().Single().Initialize(sim, unit.Id);
        sim.Content.Services.Get<IVisibilityService>().Single().Recompute(sim); return unit;
    }
    private static City AddCity(Simulation sim, int tile = 39, int owner = 0)
    {
        var city = new City { Id = sim.State.NextCityId++, Owner = owner, Position = sim.State.Grid.HexOf(new(tile)) };
        sim.State.AddCity(city); sim.State.PlotAt(city.Position).Owner = owner; return city;
    }
    private static void Dispatch<T>(GameSession session, Unit unit) where T : GameCommand
    {
        session.SelectUnit(unit.Id);
        var action = session.DescribeUnit(unit)!.Actions.First(a => a.Command is T);
        session.RunAction(action.Id); Assert.Equal("", session.Message);
    }

    [Theory]
    [InlineData("lead")]
    [InlineData("construct")]
    [InlineData("trade")]
    [InlineData("inquisition")]
    public void Person_and_religious_action_buttons_dispatch_the_selected_command(string action)
    {
        var sim = GameFactory.Scenario(mods.Content); GameFactory.NeutralPolitics(sim);
        var city = AddCity(sim, owner: action == "trade" ? 1 : 0); var session = new GameSession(sim);
        string type = action switch { "lead" => "military:great_general", "construct" => "military:scientist", "trade" => "military:merchant", _ => "military:inquisitor" };
        var person = Add(sim, type);
        switch (action)
        {
            case "lead":
                var soldier = Add(sim, "military:warrior");
                Dispatch<LeadGreatPersonCommand>(session, person);
                Assert.Contains("military:leader", soldier.Promotions);
                break;
            case "construct":
                Dispatch<ConstructGreatPersonCommand>(session, person); Assert.Contains("cities:academy", city.Buildings);
                break;
            case "trade":
                long before = sim.State.PlayerById(0).Treasury;
                Dispatch<TradeGreatPersonCommand>(session, person); Assert.True(sim.State.PlayerById(0).Treasury > before);
                break;
            default:
                sim.State.PlayerById(0).Civics["government:civicoption_religion"] = "government:intolerant";
                sim.State.PlayerById(0).Extensions.Set(sim.Content, new ReligionPlayerState { LastStateReligion = "religion:buddhism" });
                city.Extensions.Set(sim.Content, new ReligionCityState { Religions = ["religion:buddhism", "religion:hinduism"] });
                Dispatch<PerformInquisitionCommand>(session, person); Assert.Single(sim.Events.Published.OfType<InquisitionAttempt>());
                break;
        }
        Assert.Null(sim.State.FindUnit(person.Id));
        Assert.DoesNotContain(session.DescribeUnit(session.Unit)?.Actions ?? [], a => a.Command is GreatPersonCommand c && c.UnitId == person.Id);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Artillery_buttons_dispatch_bombardment_and_ranged_fire(bool bombard)
    {
        var sim = GameFactory.Scenario(mods.Content); var siege = Add(sim, "military:catapult");
        Add(sim, "military:warrior", 40, 1); var city = AddCity(sim, 40, 1); city.Buildings.Add("cities:walls");
        sim.State.SetWar(0, 1, true); var session = new GameSession(sim);
        if (bombard) { Dispatch<BombardCityCommand>(session, siege); Assert.Single(sim.Events.Published.OfType<CityBombarded>()); }
        else { Dispatch<RangedStrikeCommand>(session, siege); Assert.Single(sim.Content.Services.Get<ICombatService>().Single().History(sim)); }
        Assert.True(siege.Extensions.Get<MilitaryUnitState>(sim.Content)!.Attacked);
    }

    [Fact]
    public void Cargo_panel_changes_from_load_to_unload_and_dispatches_both()
    {
        var sim = GameFactory.Scenario(mods.Content); AddCity(sim);
        var ship = Add(sim, "military:cog"); var cargo = Add(sim, "military:warrior"); var session = new GameSession(sim);
        Dispatch<LoadUnitCommand>(session, cargo);
        Assert.Equal(ship.Id, cargo.Extensions.Get<MilitaryUnitState>(sim.Content)!.TransportId);
        Assert.DoesNotContain(session.DescribeUnit(cargo)!.Actions, a => a.Command is LoadUnitCommand);
        Dispatch<UnloadUnitCommand>(session, cargo); Assert.Null(cargo.Extensions.Get<MilitaryUnitState>(sim.Content)!.TransportId);
    }

    [Theory]
    [InlineData(AirMissionKind.Rebase)]
    [InlineData(AirMissionKind.AirStrike)]
    [InlineData(AirMissionKind.AirBomb)]
    public void Aircraft_panel_dispatches_targeted_missions(AirMissionKind kind)
    {
        var sim = GameFactory.Scenario(mods.Content); var plane = Add(sim, "military:bomber");
        var city = AddCity(sim, 40, kind == AirMissionKind.Rebase ? 0 : 1); city.Buildings.Add("cities:walls");
        if (kind != AirMissionKind.Rebase) { Add(sim, "military:warrior", 40, 1); sim.State.SetWar(0, 1, true); }
        var session = new GameSession(sim); session.SelectUnit(plane.Id);
        var action = session.DescribeUnit(plane)!.Actions.Single(a => a.Command is AirMissionCommand c && c.Kind == kind && c.Target == city.Position);
        session.RunAction(action.Id); Assert.Equal("", session.Message); Assert.Equal(0, plane.MovesLeft);
        Assert.Contains(sim.Events.Published.OfType<AirMissionResolved>(), e => e.Report.Kind == kind);
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
        var s = CitySession("peoples:leader_julius_caesar", "peoples:leader_gjergj_kastrioti");
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

    [Theory]
    [InlineData("world:peak", PlotKind.Peak, "Mountain")]
    [InlineData("world:grass", PlotKind.Hills, "Hills Grassland")]
    [InlineData("world:plains", PlotKind.Flat, "Plains")]
    public void Tile_titles_use_standalone_mountains_and_preserve_other_relief(string terrain, PlotKind kind, string title)
    {
        var sim = GameFactory.Scenario(mods.Content);
        var tile = sim.State.Grid.TileAt(3, 3);
        var plot = sim.State.Plots[tile.Value];
        plot.Terrain = terrain;
        plot.Kind = kind;
        var visibility = sim.Content.Services.Get<IVisibilityService>().Single();
        // Explicit map observation establishes memory without granting current sight.
        visibility.RevealPlot(sim, tile, sim.TeamOf(GameSession.Human).Id);
        var session = new GameSession(sim);
        Assert.Equal(Sight.Fogged, session.SightOf(plot));
        Assert.Equal(terrain, session.PerceivedPlot(tile)!.Terrain);
        Assert.Equal(kind, session.PerceivedPlot(tile)!.Kind);
        Assert.Equal(title, session.DescribeTile(sim.State.Grid.HexOf(tile))!.Title);
        plot.Terrain = "world:ocean";
        plot.Kind = PlotKind.Flat;
        Assert.Equal(title, session.DescribeTile(sim.State.Grid.HexOf(tile))!.Title);
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
    public void City_screen_reports_conquest_state_and_annex_availability()
    {
        var s = CitySession();
        var city = Found(s);
        var rules = city.Extensions.Get<CityRuleState>(s.Sim.Content)!;
        var ordinary = s.DescribeCity(city)!;
        Assert.False(ordinary.IsPuppet);
        Assert.Equal(0, ordinary.OccupationTurns);
        Assert.False(ordinary.PendingConquestChoice);
        Assert.False(ordinary.CanAnnex);

        rules.IsPuppet = true;
        rules.OccupationTurns = 3;
        var puppet = s.DescribeCity(city)!;
        Assert.True(puppet.IsPuppet);
        Assert.Equal(3, puppet.OccupationTurns);
        Assert.False(puppet.PendingConquestChoice);
        Assert.True(puppet.CanAnnex);

        rules.IsPuppet = false;
        rules.PendingConquestChoice = true;
        Assert.True(s.DescribeCity(city)!.CanAnnex);
    }

    [Fact]
    public void Pending_conquests_describe_owned_choices_and_raze_validity()
    {
        var s = CitySession();
        var ordinary = Found(s);
        var foreign = AddCity(s.Sim, 80, owner: 1);
        foreign.Extensions.Set(s.Sim.Content, new CityRuleState { IsPuppet = true, PendingConquestChoice = true });
        var city = CaptureForHuman(s);

        var conquest = Assert.Single(s.DescribePendingConquests());

        Assert.Equal(city.Id, conquest.CityId);
        Assert.Equal(city.Name, conquest.Name);
        Assert.Equal(city.Population, conquest.Population);
        Assert.InRange(conquest.OccupationTurns, 1, 4); // Captured population is three, with no captor culture.
        Assert.Equal(city.Extensions.Get<CityRuleState>(s.Sim.Content)!.OccupationTurns, conquest.OccupationTurns);
        Assert.True(conquest.CanRaze);
        Assert.False(s.DescribeCity(ordinary)!.PendingConquestChoice);
        Assert.True(s.DescribeCity(city)!.Disorder);

        city.Buildings.Add("cities:palace");
        Assert.False(Assert.Single(s.DescribePendingConquests()).CanRaze);
        var before = s.Sim.ComputeHash();
        Assert.False(s.ResolveCityConquest(city.Id, CityConquestOption.Raze));
        Assert.Equal(before, s.Sim.ComputeHash());
    }

    /// <summary>Annex, puppet and raze all apply whether the city was just captured (a pending choice) or is already a
    /// puppet being annexed outright, which is the no-pending-choice path the city screen's Annex button takes.</summary>
    [Theory]
    [InlineData(CityConquestOption.Annex, false)]
    [InlineData(CityConquestOption.Puppet, false)]
    [InlineData(CityConquestOption.Raze, false)]
    [InlineData(CityConquestOption.Annex, true)]
    public void Session_applies_each_city_conquest_outcome(CityConquestOption option, bool existingPuppet)
    {
        var s = CitySession();
        var city = existingPuppet ? Found(s) : CaptureForHuman(s);
        var rules = city.Extensions.Get<CityRuleState>(s.Sim.Content)!;
        if (existingPuppet) rules.IsPuppet = true;

        // The existing-puppet row goes through AnnexCity, the entry point the city screen's Annex button uses.
        Assert.True(existingPuppet ? s.AnnexCity(city) : s.ResolveCityConquest(city.Id, option), s.Message);
        Assert.Empty(s.DescribePendingConquests());
        Assert.False(rules.PendingConquestChoice);
        if (option == CityConquestOption.Raze)
        {
            Assert.Null(s.Sim.State.FindCity(city.Id));
            return;
        }
        Assert.Equal(option == CityConquestOption.Puppet, rules.IsPuppet);
        var screen = s.DescribeCity(city)!;
        Assert.Equal(option == CityConquestOption.Puppet, screen.IsPuppet);
        Assert.Equal(option == CityConquestOption.Puppet, screen.CanAnnex);
        Assert.Equal(!existingPuppet, screen.Disorder); // The conquest choice does not end occupation, and a puppet was never occupied.
    }

    [Fact]
    public void City_choices_split_wonders_out_of_the_building_tab_and_the_all_tab_gathers_everything()
    {
        var s = CitySession();
        var city = Found(s);

        var tabs = s.ProductionTabs;
        Assert.Equal(["All", "Buildings", "Wonders", "Units", "Processes"], tabs.Select(t => t.Label));
        ProductionTab Tab(string label) => tabs.Single(t => t.Label == label);
        // A kind's categories sit right after it, so the two building lists read as one group.
        Assert.Equal(tabs.ToList().IndexOf(Tab("Buildings")) + 1, tabs.ToList().IndexOf(Tab("Wonders")));

        var all = s.ProductionChoices(city, Tab("All"));
        var buildings = s.ProductionChoices(city, Tab("Buildings"));
        var wonders = s.ProductionChoices(city, Tab("Wonders"));
        string[] BuildingIds(string kind) => all.Where(c => c.Kind == kind).Select(c => c.Id).ToArray();

        // Every building has one home tab: the two tabs hold the whole kind, and no building is in both.
        Assert.Equal(BuildingIds("cities:building").Order(StringComparer.Ordinal),
            buildings.Concat(wonders).Select(c => c.Id).Order(StringComparer.Ordinal));
        Assert.Empty(buildings.Select(c => c.Id).Intersect(wonders.Select(c => c.Id), StringComparer.Ordinal));

        // A world wonder and a national wonder land in the wonder tab; a building whose limits declare NoLimit keeps its
        // own copies (the corporation buildings) and stays an ordinary building.
        Assert.Contains(wonders, c => c.Id == "cities:abu_simbel");
        Assert.Contains(wonders, c => c.Id == "cities:amusement_park");
        Assert.Contains(buildings, c => c.Id == "cities:aluminum_factory");
        Assert.DoesNotContain(buildings, c => c.Id == "cities:abu_simbel");
        Assert.DoesNotContain(wonders, c => c.Id == "cities:aluminum_factory");

        // The other kinds keep one tab each, so the wonder split never empties them.
        Assert.Equal(BuildingIds("cities:unit").Order(StringComparer.Ordinal),
            s.ProductionChoices(city, Tab("Units")).Select(c => c.Id).Order(StringComparer.Ordinal));
    }

    [Fact]
    public void Filtered_rows_keep_an_available_match_ahead_of_the_blocked_cap_and_report_what_matched()
    {
        var s = CitySession();
        var city = Found(s);
        var tab = s.ProductionTabs.Single(t => t.Label == "Buildings");
        var choices = s.ProductionChoices(city, tab);

        // A name-filtered row is offered even when it is blocked and hundreds of other blocked rows come first: the
        // cap applies to what survives the filter, never to the tab.
        var hidden = choices.Where(c => !c.Available).Skip(60).First(c => c.Matches(c.Name));
        var filtered = s.ProductionRows(city, tab, hidden.Name, showBlocked: true);
        Assert.Contains(filtered.Rows, c => c.Id == hidden.Id);

        // The screen shows blocked rows only when asked, but the match count still reports them, so it can say that
        // something matched and is merely unavailable.
        var available = s.ProductionRows(city, tab, hidden.Name, showBlocked: false);
        Assert.DoesNotContain(available.Rows, c => c.Id == hidden.Id);
        Assert.Equal(filtered.Matches, available.Matches);
        Assert.True(available.Matches > available.Rows.Count);

        // A blank filter matches every choice in the tab, and a name no content has matches none.
        Assert.Equal(choices.Count, s.ProductionRows(city, tab, "", showBlocked: false).Matches);
        Assert.Equal(0, s.ProductionRows(city, tab, "zzzz no such building", showBlocked: true).Matches);

        // Case is ignored, so a player need not match the content's capitals.
        Assert.Equal(filtered.Matches, s.ProductionRows(city, tab, hidden.Name.ToUpperInvariant(), showBlocked: true).Matches);
    }


    [Fact]
    public void A_queued_building_leaves_the_choices_while_units_stay_repeatable()
    {
        var s = CitySession();
        var city = Found(s);
        var building = s.ProductionChoices(city, "cities:building").First(c => c.Available);
        Assert.True(s.Enqueue(city, building.Kind, building.Id, append: true));
        var afterEnqueue = Assert.Single(s.ProductionChoices(city, "cities:building"), c => c.Id == building.Id);
        Assert.False(afterEnqueue.Available);
        Assert.Equal(["Already in queue"], afterEnqueue.Blockers);

        // A unit is trained again and again: having one queued does not use up the choice.
        var unit = s.ProductionChoices(city, "cities:unit").First(c => c.Available);
        Assert.True(s.Enqueue(city, unit.Kind, unit.Id, append: true));
        Assert.True(Assert.Single(s.ProductionChoices(city, "cities:unit"), c => c.Id == unit.Id).Available);

        // Removing the entry puts the building back on offer.
        Assert.True(s.RemoveFromQueue(city, 0));
        Assert.True(Assert.Single(s.ProductionChoices(city, "cities:building"), c => c.Id == building.Id).Available);

        // A continuous process converts production and stays on offer however often it is queued.
        var process = s.ProductionChoices(city, "economy:process").First(c => c.Available);
        Assert.True(s.Enqueue(city, process.Kind, process.Id, append: true));
        Assert.True(Assert.Single(s.ProductionChoices(city, "economy:process"), c => c.Id == process.Id).Available);
    }

    [Fact]
    public void City_screen_reports_ledgers_and_the_production_queue_can_be_edited()
    {
        var s = CitySession();
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
        // The row picked first is a building, which is what makes the duplicate check below meaningful.
        Assert.Equal("cities:building", first.Kind);
        Assert.True(s.Enqueue(city, first.Kind, first.Id, append: false));
        Assert.True(s.Enqueue(city, second.Kind, second.Id, append: true));
        Assert.True(s.Enqueue(city, third.Kind, third.Id, append: true));
        Assert.Equal([first.Id, second.Id, third.Id], city.ProductionQueue.Select(o => o.ContentId));

        // A copy that is already queued is refused by the command, so a caller that never drew the city screen cannot
        // queue a second one either.
        Assert.False(s.Enqueue(city, first.Kind, first.Id, append: true));
        Assert.Contains("Already in queue", s.Message);

        s.EndTurn();
        long progress = city.ProductionQueue[0].Progress;
        Assert.True(progress > 0);
        int settled = s.Revision;
        Assert.True(s.MoveInQueue(city, 0, 1));
        Assert.Equal(progress, city.ProductionQueue[1].Progress);
        // A move is one command, so it bumps once: the old clear-and-re-enqueue loop bumped for every entry.
        Assert.Equal(settled + 1, s.Revision);
        Assert.True(s.MoveInQueue(city, 1, -1));
        Assert.True(s.MoveInQueue(city, 2, -1));
        Assert.Equal([first.Id, third.Id, second.Id], city.ProductionQueue.Select(o => o.ContentId));
        Assert.True(s.RemoveFromQueue(city, 0));
        Assert.Equal([third.Id, second.Id], city.ProductionQueue.Select(o => o.ContentId));
        Assert.Equal(2, s.DescribeCity(city)!.Queue.Count);

        // Replacing the queue with an entry it already holds is allowed: a replacement clears the queue first, so the
        // copies it counts against the content's room are gone by the time it adds.
        Assert.True(s.Enqueue(city, third.Kind, third.Id, append: false));
        Assert.Equal([third.Id], city.ProductionQueue.Select(o => o.ContentId));
    }

    [Fact]
    public void Map_nameplate_reads_live_growth_and_production_from_an_observed_city()
    {
        var s = CitySession();
        var city = Found(s);
        var observed = Assert.Single(s.KnownCities(), c => c.Id == city.Id);
        var choices = s.ProductionChoices(city).Where(c => c.Available && !c.IsContinuous && c.Cost > 10).Take(2).ToArray();
        Assert.Equal(2, choices.Length);
        Assert.True(s.Enqueue(city, choices[0].Kind, choices[0].Id, append: false));
        city.ProductionQueue[0] = city.ProductionQueue[0] with { Progress = 1 };
        city.FoodStored = 7;

        var plate = s.PlateOf(observed);

        Assert.Equal(choices[0].Id, plate.ProductionId);
        Assert.InRange(plate.Growth, 0.001f, 1f);
        Assert.InRange(plate.Production!.Value, 0.00001f, 0.99f);
        Assert.True(s.Enqueue(city, choices[1].Kind, choices[1].Id, append: false));
        Assert.Equal(choices[1].Id, s.PlateOf(observed).ProductionId);
        Assert.True(s.RemoveFromQueue(city, 0));
        Assert.Null(s.PlateOf(observed).ProductionId);
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
        Assert.Equal(1, city.Specialists.GetValueOrDefault(s.CitizenSpecialist));
        s.ToggleTile(city, worked);
        Assert.Contains(worked, city.Worked);
        s.SelectCity(city.Id);
        Assert.Equal(TileMark.Worked, s.TileMarks()[worked]);
    }
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
