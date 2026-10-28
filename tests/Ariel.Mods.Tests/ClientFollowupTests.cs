using Ariel.Client;
using Ariel.Content;
using Ariel.Game;
using Ariel.Modding;
using Xunit;
using S = Ariel.Content.Schema;

namespace Ariel.Mods.Tests;

/// <summary>Religion screen, pedia, save/load and notifications in the client session.</summary>
public sealed class ClientFollowupTests(LoadedMods mods) : IClassFixture<LoadedMods>
{
    private const string Buddhism = "religion:buddhism";

    private GameSession Scenario()
    {
        var sim = GameFactory.Scenario(mods.Content);
        GameFactory.NeutralPolitics(sim);
        return new GameSession(sim);
    }

    private static City AddCity(Simulation sim, int tile, int owner = 0)
    {
        var city = new City { Id = sim.State.NextCityId++, Owner = owner, Position = sim.State.Grid.HexOf(new TileId(tile)), Population = 1 };
        sim.State.AddCity(city);
        return city;
    }

    private static Unit AddUnit(Simulation sim, City at, string type)
    {
        var unit = new Unit { Id = sim.State.NextUnitId++, Owner = at.Owner, Type = type, Position = at.Position, MovesLeft = 60 };
        sim.State.AddUnit(unit);
        return unit;
    }

    private static void Found(Simulation sim, string religion = Buddhism)
    {
        var tech = sim.Content.Get<S.ReligionDef>(religion).FoundingTech!;
        sim.TeamOf(0).Techs.UnionWith(sim.Content.Get<S.TechDef>().All.Where(t => t.Id != tech).Select(t => t.Id));
        sim.TeamOf(0).Techs.Remove(tech);
        Assert.True(sim.TryApply(new SetTeamResearchCommand { Player = 0, Tech = tech }, out var error), error);
        var research = sim.Content.Services.Get<IResearchService>().Single();
        research.Contribute(sim, 0, research.CalculateCost(sim, sim.TeamOf(0).Id, tech).Cost);
    }

    [Fact]
    public void Religion_screen_lists_every_religion_and_marks_the_holy_city()
    {
        var s = Scenario();
        var screen = s.DescribeReligions();
        Assert.Equal(s.Sim.Content.Get<S.ReligionDef>().All.Count(), screen.Religions.Count);
        Assert.All(screen.Religions, r => Assert.False(r.Founded));
        Assert.Null(screen.StateReligion);
        var city = AddCity(s.Sim, 0);
        Found(s.Sim);
        var row = s.DescribeReligions().Religions.Single(r => r.Id == Buddhism);
        Assert.True(row.Founded);
        Assert.Equal(city.Id, row.HolyCityId);
        Assert.Equal(city.Name, row.HolyCityName);
        var present = s.ReligionsIn(city).Single();
        Assert.True(present.Holy);
        Assert.Contains("*", s.ReligionBadge(city));
    }

    [Fact]
    public void Adopting_a_state_religion_goes_through_the_rules_and_reports_cost()
    {
        var s = Scenario();
        AddCity(s.Sim, 0);
        Found(s.Sim);
        var before = s.DescribeReligions();
        Assert.False(before.Religions.Single(r => r.Id == Buddhism).CanAdopt); // no civic allows a state religion yet
        Assert.False(s.AdoptStateReligion(Buddhism));
        Assert.StartsWith("Can't do that", s.Message);
        s.Me.Civics["government:civicoption_religion"] = "government:state_church";
        Assert.True(s.DescribeReligions().Religions.Single(r => r.Id == Buddhism).CanAdopt);
        Assert.True(s.AdoptStateReligion(Buddhism), s.Message);
        var after = s.DescribeReligions();
        Assert.Equal(Buddhism, after.StateReligion);
        Assert.True(after.Religions.Single(r => r.Id == Buddhism).IsState);
        Assert.True(after.CooldownRemaining > 0);
        Assert.Contains(s.RecentNotifications(), n => n.Kind == "religion" && n.Text.Contains("adopted"));
    }

    [Fact]
    public void Missionary_and_prophet_actions_appear_when_the_rules_allow_them()
    {
        var s = Scenario();
        var holy = AddCity(s.Sim, 0);
        Found(s.Sim);
        var target = AddCity(s.Sim, 5);
        var missionary = AddUnit(s.Sim, target, "military:buddhist_missionary");
        s.SelectUnit(missionary.Id);
        Assert.Contains(s.DescribeUnit(s.Unit)!.Actions, a => a.Id == "spread:" + Buddhism && a.Command != null);
        var prophet = AddUnit(s.Sim, holy, "military:prophet");
        s.SelectUnit(prophet.Id);
        Assert.Contains(s.DescribeUnit(s.Unit)!.Actions, a => a.Id == "shrine:cities:buddhist_shrine");
        s.RunAction("shrine:cities:buddhist_shrine");
        Assert.Contains("cities:buddhist_shrine", holy.Buildings);
        s.SelectUnit(missionary.Id);
        s.RunAction("spread:" + Buddhism);
        Assert.Contains(s.RecentNotifications(), n => n.Kind == "religion" && n.Text.Contains("Missionary"));
    }

    [Fact]
    public void City_screen_combines_religion_great_people_and_trade_service_data()
    {
        var original = mods.Content.Get<S.RuleConstantsDef>().All.Single();
        var probe = new ModSource
        {
            Manifest = new() { Id = "trade_probe", Version = "1", Dependencies = ["foundation"], References = ["foundation"] },
            Root = mods.Root,
            Instance = new TradeProbe(original),
        };
        var sim = GameFactory.Scenario(ModLoader.Build(mods.Sources.Append(probe)));
        GameFactory.NeutralPolitics(sim);
        var s = new GameSession(sim);
        foreach (var plot in sim.State.Plots) { plot.Route = "world:route_route_road"; plot.Revealed.Add(0); }
        var city = AddCity(sim, 0);
        city.Specialists["foundation:specialist_scientist"] = 1;
        city.Extensions.Set(sim.Content, new CityRuleState { ManualWork = true, AvoidGrowth = true });
        city.Extensions.Set(sim.Content, new GreatPeopleCityState
        {
            Progress = 12,
            SettledSpecialists = new SortedDictionary<string, int>(StringComparer.Ordinal)
            {
                ["foundation:specialist_great_artist"] = 1,
            },
        });
        sim.State.PlayerById(0).Extensions.Set(sim.Content, new GreatPeoplePlayerState { GoldenAgeTurns = 7 });
        Found(sim);
        AddCity(sim, 4);
        AddCity(sim, 7, owner: 1);

        var screen = s.DescribeCity(city)!;
        Assert.Contains(screen.Religions, religion => religion.Id == Buddhism);
        Assert.Equal(12, screen.GreatPeopleProgress);
        Assert.True(screen.GreatPeopleThreshold > 0);
        Assert.True(screen.GreatPeopleRate > 0);
        Assert.Equal(1, screen.SettledSpecialists["foundation:specialist_great_artist"]);
        Assert.Equal(7, screen.GoldenAgeTurns);
        Assert.True(screen.MaxTradeRoutes > 0);
        Assert.NotEmpty(screen.TradeRoutes);

        var greatPerson = AddUnit(sim, city, "military:artist");
        s.SelectUnit(greatPerson.Id);
        Assert.Contains(s.DescribeUnit(greatPerson)!.Actions,
            action => action.Id == "great_people:join_city" && action.Command is JoinGreatPersonCommand);
    }

    [Fact]
    public void Pedia_indexes_every_category_and_searches_by_text()
    {
        var s = Scenario();
        var pedia = s.Pedia;
        foreach (var category in new[] { "Technologies", "Buildings", "Units", "Promotions", "Civics", "Religions", "Resources",
                     "Improvements", "Terrain", "Features", "Specialists", "Civilizations", "Leaders", "Traits" })
            Assert.Contains(category, pedia.Categories);
        Assert.Equal(s.Sim.Content.Get<S.TechDef>().All.Count(), pedia.Search("Technologies").Count);
        var buddhism = pedia.Find(Buddhism)!;
        Assert.Equal("Religions", buddhism.Category);
        Assert.Contains(buddhism.Prerequisites, p => p.StartsWith("Founded by"));
        Assert.Contains(buddhism, pedia.Search("Religions", buddhism.Title.ToLowerInvariant()));
        Assert.Empty(pedia.Search("Units", "zzzz-no-such-article"));
        var tech = pedia.Search("Technologies").First(e => e.Cost > 0);
        Assert.False(string.IsNullOrEmpty(tech.Title));
        Assert.Contains(pedia.Search("Buildings"), e => e.Cost > 0);
        // Cross-category search must list matches in title order.
        var everywhere = pedia.Search(query: "a");
        Assert.True(everywhere.Select(e => e.Category).Distinct().Count() > 1);
        var titleOrder = everywhere.OrderBy(e => e.Title, StringComparer.Ordinal).ThenBy(e => e.Id, StringComparer.Ordinal).Select(e => e.Id).ToList();
        Assert.Equal(titleOrder, everywhere.Select(e => e.Id).ToList());
    }

    [Fact]
    public void Save_and_load_round_trip_keeps_state_and_refreshes_views()
    {
        var s = Scenario();
        var city = AddCity(s.Sim, 0);
        var json = s.SaveToJson();
        ulong hash = s.Sim.ComputeHash();
        s.Sim.State.CityAt(city.Position)!.Population = 7;
        s.SelectCity(city.Id);
        int revision = s.Revision, world = s.WorldRevision;
        Assert.True(s.LoadFromJson(json), s.Message);
        Assert.Equal(hash, s.Sim.ComputeHash());
        Assert.Equal(1, s.Sim.State.Cities.Single().Population);
        Assert.True(s.Revision > revision && s.WorldRevision > world);
        Assert.Null(s.City);

        var path = Path.Combine(Path.GetTempPath(), $"civ-client-{Guid.NewGuid():N}", "slot.json");
        try
        {
            Assert.True(s.SaveGame(path), s.Message);
            var other = Scenario();
            Assert.True(other.LoadGame(path), other.Message);
            Assert.Equal(hash, other.Sim.ComputeHash());
        }
        finally { Directory.Delete(Path.GetDirectoryName(path)!, true); }
    }

    [Fact]
    public void Loading_a_bad_save_keeps_the_current_game()
    {
        var s = Scenario();
        ulong hash = s.Sim.ComputeHash();
        Assert.False(s.LoadFromJson("{ not json"));
        Assert.StartsWith("Can't load", s.Message);
        Assert.False(s.LoadGame(Path.Combine(Path.GetTempPath(), "civ-missing-save.json")));
        Assert.Equal(hash, s.Sim.ComputeHash());
    }

    [Fact]
    public void Turn_events_become_notifications_for_the_local_player_only()
    {
        var s = new GameSession(GameFactory.Create(mods.Content, 3));
        var settler = s.Sim.State.Units.First(u => u.Owner == GameSession.Human && s.DefOf(u).CanFoundCity);
        s.SelectUnit(settler.Id);
        s.RunAction("found");
        Assert.Contains(s.Notifications, n => n.Kind == "city" && n.Text.StartsWith("Founded"));
        Assert.DoesNotContain(s.Notifications, n => n.Text.Contains("Player 1"));
        int turn = s.Sim.State.Turn;
        s.EndTurn();
        Assert.Equal(turn + 1, s.Sim.State.Turn);
        Assert.Contains(s.RecentNotifications(), n => n.Turn == turn && n.Kind == "city");
        Assert.All(s.RecentNotifications(1), n => Assert.Equal(s.Sim.State.Turn, n.Turn));
    }

    [Fact]
    public void Tech_discovery_notifies_only_the_discovering_team()
    {
        var s = Scenario();
        AddCity(s.Sim, 0);
        Found(s.Sim);
        s.SetMessage(""); // Founding was driven directly on the simulation; the next command collects its events.
        s.SelectNextUnit();
        var missionary = AddUnit(s.Sim, s.Sim.State.Cities[0], "military:buddhist_missionary");
        s.Submit(new SetUnitActivityCommand { Player = 0, Unit = missionary.Id, Activity = UnitActivity.Sleep });
        Assert.Contains(s.Notifications, n => n.Kind == "tech");
        Assert.Contains(s.Notifications, n => n.Kind == "religion" && n.Text.Contains("was founded"));

        // "only" needs the negative: the filter is team equality, so a rival team's discovery must stay silent.
        string rivalTech = "research:bronze_working";
        string rivalName = s.Sim.Content.Get<S.TechDef>(rivalTech).Name;
        Assert.DoesNotContain(rivalName, s.Notifications.First(n => n.Kind == "tech").Text);
        s.Sim.Events.Publish(new TechDiscovered(1, rivalTech));
        s.Submit(new SetUnitActivityCommand { Player = 0, Unit = missionary.Id, Activity = UnitActivity.Sleep });
        Assert.DoesNotContain(s.Notifications, n => n.Kind == "tech" && n.Text.Contains(rivalName));
    }

    private static Unit AddFighter(Simulation sim, int owner, int tile, string type = "military:warrior")
    {
        var unit = new Unit { Id = sim.State.NextUnitId++, Owner = owner, Type = type, Position = sim.State.Grid.HexOf(new TileId(tile)) };
        sim.State.AddUnit(unit);
        sim.Content.Services.Get<IUnitService>().Single().Initialize(sim, unit.Id);
        sim.Content.Services.Get<IVisibilityService>().Single().Recompute(sim);
        return unit;
    }

    /// <summary>Advance the random stream until the next combat round goes to the defender (or the attacker).</summary>
    private static void RigNextRound(Simulation sim, Unit a, Unit d, bool defenderHits)
    {
        var preview = sim.Content.Services.Get<ICombatService>().Single().Preview(sim, a.Id, d.Id);
        while (true)
        {
            var (w, x, y, z) = sim.Rng.State;
            if ((DeterministicRng.Restore(w, x, y, z).Next(preview.DieSides) < preview.DefenderRoundOdds) == defenderHits) break;
            sim.Rng.NextUInt64();
        }
    }

    private (GameSession Session, Unit Attacker, Unit Defender, BattlePlayback Battle) Fight(bool defenderHits, Action<Unit, Unit>? tweak = null)
    {
        var s = Scenario();
        var a = AddFighter(s.Sim, 0, 39); var d = AddFighter(s.Sim, 1, 40, "military:archer");
        s.Sim.State.SetWar(0, 1, true);
        var (attackerHex, defenderHex) = (a.Position, d.Position);
        s.Submit(new SetUnitActivityCommand { Player = GameSession.Human, Unit = a.Id, Activity = UnitActivity.Awake }); // The session notes where everyone stands.
        tweak?.Invoke(a, d);
        RigNextRound(s.Sim, a, d, defenderHits);
        s.Sim.Content.Services.Get<ICombatService>().Single().Resolve(s.Sim, a.Id, d.Id);
        s.SetMessage("");
        s.Submit(new SetUnitActivityCommand { Player = GameSession.Human, Unit = a.Id, Activity = UnitActivity.Sleep });
        var battle = Assert.IsType<BattlePlayback>(s.DequeueBattle());
        Assert.Equal(attackerHex, battle.AttackerHex);
        Assert.Equal(defenderHex, battle.DefenderHex);
        Assert.Equal("military:warrior", battle.AttackerType);
        Assert.Equal("military:archer", battle.DefenderType);
        Assert.Equal((0, 1), (battle.AttackerOwner, battle.DefenderOwner));
        Assert.NotEmpty(battle.Rounds);
        return (s, a, d, battle);
    }

    [Fact]
    public void Battle_playback_records_a_defender_that_died()
    {
        var (s, a, d, battle) = Fight(defenderHits: false, (_, def) => def.Health = 1);
        Assert.Equal(CombatOutcome.AttackerWon, battle.Outcome);
        Assert.Equal((a.Id, d.Id), (battle.WinnerId, battle.LoserId));
        Assert.True(battle.LoserDied);
        Assert.Null(s.DequeueBattle());
    }

    [Fact]
    public void Battle_playback_records_an_attacker_that_died()
    {
        var (_, a, d, battle) = Fight(defenderHits: true, (att, _) => att.Health = 1);
        Assert.Equal(CombatOutcome.DefenderWon, battle.Outcome);
        Assert.Equal((d.Id, a.Id), (battle.WinnerId, battle.LoserId));
        Assert.True(battle.LoserDied);
    }

    [Fact]
    public void Battle_playback_for_a_withdrawal_keeps_the_loser_alive()
    {
        var (s, a, _, battle) = Fight(defenderHits: true, (att, _) =>
        {
            att.Health = 1;
            att.Promotions.UnionWith(["military:escape1", "military:escape2", "military:tactics"]);
        });
        Assert.Equal(CombatOutcome.Withdrawn, battle.Outcome);
        Assert.False(battle.LoserDied);
        Assert.NotNull(s.Sim.State.FindUnit(a.Id));
    }

    [Fact]
    public void Unit_trained_and_fought_in_one_turn_roll_reports_the_battle_from_the_same_batch()
    {
        var s = Scenario();
        var city = AddCity(s.Sim, 0);
        var enemy = AddUnit(s.Sim, AddCity(s.Sim, 5, owner: 1), "military:warrior");
        // Both units join the roster after the session last read it, then fight within the same batch.
        var victor = AddUnit(s.Sim, city, "military:warrior");
        var casualty = AddUnit(s.Sim, city, "military:warrior");
        s.Sim.Events.Publish(new UnitCreated(victor.Id, GameSession.Human, victor.Type, victor.Position));
        s.Sim.Events.Publish(new UnitCreated(casualty.Id, GameSession.Human, casualty.Type, casualty.Position));
        s.Sim.Events.Publish(new CombatResolved(victor.Id, enemy.Id, victor.Id));
        s.Sim.Events.Publish(new CombatResolved(enemy.Id, casualty.Id, enemy.Id));
        s.Submit(new SetUnitActivityCommand { Player = GameSession.Human, Unit = victor.Id, Activity = UnitActivity.Sleep });
        Assert.Contains(s.Notifications, n => n.Kind == "combat" && n.Text == "Your unit won a battle");
        Assert.Contains(s.Notifications, n => n.Kind == "combat" && n.Text == "Your unit was lost in battle");
    }

    [Fact]
    public void Multi_step_attack_records_the_hex_it_attacked_from_not_where_the_walk_started()
    {
        var s = Scenario();
        var grid = s.Sim.State.Grid;
        var target = grid.HexOf(new TileId(40));
        var start = grid.Ring(target, 2).First(h => grid.Neighbors(h).Any(n => grid.Distance(n, target) == 1));
        var attacker = AddFighter(s.Sim, 0, grid.TileAt(start).Value, "military:horseman"); // Two moves: one step, then the attack.
        var defender = AddFighter(s.Sim, 1, grid.TileAt(target).Value, "military:archer");
        s.Sim.State.SetWar(0, 1, true);
        s.Submit(new SetUnitActivityCommand { Player = GameSession.Human, Unit = attacker.Id, Activity = UnitActivity.Awake }); // The session notes where everyone stands before the walk.
        s.SelectUnit(attacker.Id);
        var path = s.PreviewAt(target)!.Path;
        Assert.Equal(2, path.Count);
        s.RightClick(target);
        var battle = Assert.IsType<BattlePlayback>(s.DequeueBattle());
        Assert.Equal((attacker.Id, defender.Id), (battle.AttackerId, battle.DefenderId));
        Assert.Equal(path[0], battle.AttackerHex);
        Assert.NotEqual(start, battle.AttackerHex);
        Assert.Equal(target, battle.DefenderHex);
        Assert.NotEmpty(battle.Rounds);
    }

    [Fact]
    public void End_of_turn_enemy_attack_keeps_its_rounds_and_loser_after_the_turn_rolls_over()
    {
        var strike = new TurnEndStrike();
        var probe = new ModSource
        {
            Manifest = new() { Id = "turn_end_probe", Version = "1", Dependencies = ["foundation"], References = ["foundation"] },
            Root = mods.Root,
            Instance = strike,
        };
        var sim = GameFactory.Scenario(ModLoader.Build(mods.Sources.Append(probe)));
        GameFactory.NeutralPolitics(sim);
        var s = new GameSession(sim);
        var defender = AddFighter(sim, 0, 40, "military:archer");
        var attacker = AddFighter(sim, 1, 39, "military:warrior");
        sim.State.SetWar(0, 1, true);
        defender.Health = 1;
        s.Submit(new SetUnitActivityCommand { Player = GameSession.Human, Unit = defender.Id, Activity = UnitActivity.Awake }); // The session notes where everyone stands before the turn ends.
        strike.Pending = (attacker.Id, defender.Id);
        int turn = sim.State.Turn;
        s.EndTurn();
        Assert.Equal(turn + 1, sim.State.Turn);
        var battle = Assert.IsType<BattlePlayback>(s.DequeueBattle());
        Assert.Equal((attacker.Id, defender.Id), (battle.AttackerId, battle.DefenderId));
        Assert.Equal(CombatOutcome.AttackerWon, battle.Outcome);
        Assert.True(battle.LoserDied);
        Assert.Null(sim.State.FindUnit(defender.Id));
        Assert.NotEmpty(battle.Rounds);
        Assert.Contains(s.RecentNotifications(), n => n.Turn == turn && n.Kind == "combat" && n.Text == "Your unit was lost in battle");
    }

    [Fact]
    public void Setup_default_that_names_unregistered_content_falls_back_to_the_first_choice()
    {
        var catalog = new GameCatalog([new ModSource
        {
            Manifest = new() { Id = "setup_probe", Version = "1" }, Root = AppContext.BaseDirectory, Instance = new SetupProbe(),
        }]);
        var options = catalog.DefaultOptions();
        Assert.Equal("setup_probe:small", options.WorldSize);
        Assert.Equal("setup_probe:quick", options.GameSpeed);
        Assert.Equal("setup_probe:easy", options.Handicap);
    }

    /// <summary>Test-only turn system that lands one enemy attack in the end-of-turn phase, before TurnEnded and TurnStarted.</summary>
    private sealed class TurnEndStrike : IMod, ITurnSystem
    {
        public (int Attacker, int Defender)? Pending { get; set; }
        public string Id => "turn_end_probe:strike";
        public int Order => TurnPhase.Map;
        public void Register(IModContext context) => context.TurnSystems.Register(this);
        public void Run(Simulation sim)
        {
            if (Pending is not { } pair) return;
            Pending = null;
            sim.Content.Services.Get<ICombatService>().Single().Resolve(sim, pair.Attacker, pair.Defender);
        }
    }

    private sealed class SetupProbe : IMod
    {
        public void Register(IModContext ctx)
        {
            ctx.Content<S.WorldSizeDef>().Register(new() { Id = "setup_probe:small", Name = "Small", GridWidth = 2, GridHeight = 2 });
            ctx.Content<S.GameSpeedDef>().Register(new() { Id = "setup_probe:quick", Name = "Quick" });
            ctx.Content<S.HandicapDef>().Register(new() { Id = "setup_probe:easy", Name = "Easy" });
            ctx.Content<S.LeaderDef>().Register(new() { Id = "setup_probe:guide", Name = "Guide" });
            ctx.Content<S.CivilizationDef>().Register(new()
            {
                Id = "setup_probe:people", Name = "People", Playable = true,
                Leaders = System.Collections.Immutable.ImmutableDictionary<string, bool>.Empty.Add("setup_probe:guide", true),
            });
            ctx.RegisterService<ISetupPolicy>("setup", new MissingDefaults());
        }
    }

    /// <summary>Names setup defaults that no mod registers, as a misconfigured ruleset would.</summary>
    private sealed class MissingDefaults : ISetupPolicy
    {
        public string? Default(SetupField field) => field is SetupField.WorldSize or SetupField.GameSpeed or SetupField.Handicap ? "setup_probe:missing" : null;
    }

    private sealed class TradeProbe(S.RuleConstantsDef original) : IMod
    {
        public void Register(IModContext context) =>
            context.Content<S.RuleConstantsDef>().Override(new()
            {
                Id = original.Id,
                Values = original.Values,
                Raw = original.Raw.SetItem("INITIAL_TRADE_ROUTES", "2").SetItem("MAX_TRADE_ROUTES", "3"),
            });
    }
}
