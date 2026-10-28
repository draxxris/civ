using Ariel.Client;
using Ariel.Content;
using Ariel.Game;
using Ariel.Modding;
using S = Ariel.Content.Schema;
using Xunit;

namespace Ariel.Mods.Tests;

public sealed class FeatureIntegrationReplayTests
{
    private const string Agriculture = "research:agriculture", Fishing = "research:fishing";
    private static readonly Lazy<ContentDb> Content = new(() => BuildContent(false));
    private static readonly Lazy<ContentDb> ShiftedCalendar = new(() => BuildContent(true));
    private static T Service<T>(Simulation sim) where T : class => sim.Content.Services.Get<T>().Single();

    private static ContentDb BuildContent(bool shifted)
    {
        var loaded = new LoadedMods();
        var sources = loaded.Sources.Concat(new[] { "Diplomacy", "Espionage" }.Select(name =>
            ModLoader.FromDirectory(Path.Combine(AppContext.BaseDirectory, "optional-mods", name))));
        if (shifted) sources = sources.Append(new ModSource
        {
            Manifest = new() { Id = "probe", Version = "1", Dependencies = ["foundation"], References = ["foundation"] },
            Root = loaded.Root, Instance = new CalendarShift(loaded.Content.Get<S.RuleConstantsDef>().All.Single()),
        });
        return ModLoader.Build(sources);
    }

    private sealed class CalendarShift(S.RuleConstantsDef original) : IMod
    {
        public void Register(IModContext context) => context.Content<S.RuleConstantsDef>().Override(new()
        {
            Id = original.Id, Raw = original.Raw, Values = original.Values with { StartYear = 2000 },
        });
    }

    private static Simulation Scenario(ContentDb? content = null)
    {
        var sim = GameFactory.Scenario(content ?? Content.Value);
        GameFactory.NeutralPolitics(sim);
        sim.Settings.GameOptions["foundation:gameoption_no_revolution"] = true;
        foreach (var plot in sim.State.Plots) plot.Owner = null;
        for (int player = 0; player < 2; player++)
        {
            var city = P6RulesScenario.AddCity(sim, player, player == 0 ? 14 : 57);
            city.Extensions.Set(sim.Content, new CityRuleState { ManualWork = true });
            sim.State.PlayerById(player).Treasury = 10000;
            Service<IPlayerEconomyService>(sim).SetRates(sim, player, new(100, 0, 0, 0));
        }
        Service<IDiplomacyService>(sim).EstablishContact(sim, 0, 1);
        return sim;
    }

    private static void Apply(Simulation sim, GameCommand command) => P6RulesScenario.Apply(sim, command);
    private static EspionageTeamState Budget(Simulation sim, int team, long points)
    {
        var state = new EspionageTeamState();
        state.Points[1 - team] = points;
        state.Spent[1 - team] = 100000;
        sim.State.TeamById(team).Extensions.Set(sim.Content, state);
        return state;
    }

    private static void SameReplay(Simulation uninterrupted, Simulation resumed)
    {
        Assert.Equal(uninterrupted.ComputeHash(), resumed.ComputeHash());
        Assert.Equal(uninterrupted.Rng.State, resumed.Rng.State);
        Assert.Equal(SaveGame.Save(uninterrupted), SaveGame.Save(resumed));
    }

    // Turn/save boundary: the earlier overflow test changes technologies, but not the queued tech's
    // rate after saving. This catches stale rate normalization and a calendar year entering timeline math.
    // Independent values: 53*88/100=46; floor(50*125/100)=62; floor(16*100/125)=12.
    [Fact]
    public void Calendar_boundary_replay_consumes_overflow_at_the_new_rate_not_the_displayed_year()
    {
        var sim = Scenario(); var shifted = Scenario(ShiftedCalendar.Value);
        foreach (var game in new[] { sim, shifted })
        {
            game.Settings.GameOptions["foundation:gameoption_no_tech_diffusion"] = true;
            game.State.Turn = 18; // 17 elapsed turns; first calendar step ends at 18.
            game.TeamOf(1).Techs.Add(Agriculture);
            Apply(game, new SetTeamResearchCommand { Player = 0, Tech = Agriculture, Queue = [Fishing] });
            var research = Service<IResearchService>(game);
            Assert.Equal(46, research.CalculateCost(game, 0, Agriculture).Cost);
            Assert.Equal(125, research.RateModifier(game, 0, Agriculture));
            research.Contribute(game, 0, 50);
            Assert.Contains(Agriculture, game.TeamOf(0).Techs);
            Assert.Equal(12, game.TeamOf(0).Extensions.Get<ResearchTeamState>(game.Content)!.Overflow);
            Assert.Equal(100, research.RateModifier(game, 0, Fishing));
        }
        var resumed = SaveGame.Load(sim.Content, SaveGame.Save(sim));
        var games = new[] { sim, resumed, shifted };
        foreach (var game in games)
        {
            Service<IResearchService>(game).AddProgress(game, 1, Fishing, long.MaxValue);
            Assert.Equal(125, Service<IResearchService>(game).RateModifier(game, 0, Fishing));
            Assert.Equal(0, game.TeamOf(0).ResearchProgress);
        }
        long[] years = [-4725, -4650, -4575, -4552]; // Inclusive endpoint retains one final 900-month step.
        long[] percents = [90, 90, 90, 89]; // floor((floor(100*4^(1/36 - elapsed*7/1200))+10)*10/11).
        for (int step = 0; step < years.Length; step++)
        {
            foreach (var game in games)
            {
                var session = new GameSession(game);
                Assert.Equal(years[step] + (game == shifted ? 8000 : 0), session.Summary().CalendarDate!.Year);
                var cost = Service<IResearchService>(game).CalculateCost(game, 0, Fishing);
                Assert.Equal(percents[step], cost.Ledger.Total("research:timeline_percent"));
                Assert.Equal(47, cost.Cost);
                Assert.Equal(step == 0 ? 0 : 15 + step, game.TeamOf(0).ResearchProgress);
            }
            SameReplay(sim, resumed);
            Assert.Equal(sim.ComputeHash(), shifted.ComputeHash());
            if (step == years.Length - 1) break;
            foreach (var game in games) game.AdvanceTurn(); // One base beaker + 12 overflow at 125% => 1 + 15.
        }
        Assert.Equal(0, sim.TeamOf(0).Extensions.Get<ResearchTeamState>(sim.Content)!.Overflow);
        Assert.DoesNotContain(Fishing, sim.TeamOf(0).Techs);
    }

    // Mission + settlement + reload lifecycle, not another timer test: spend at the last active turn,
    // observe immediate permission loss, then cross expiry and compare RNG, gold and budgets every turn.
    [Fact]
    public void Mid_protection_reload_replays_spending_revocation_and_expiry_with_identical_balances()
    {
        var sim = Scenario(); Budget(sim, 0, 400); Budget(sim, 1, 400);
        var home = sim.State.Cities.Single(c => c.Owner == 0);
        var target = sim.State.Cities.Single(c => c.Owner == 1);
        var defender = P6RulesScenario.AddUnit(sim, 1, home, "military:spy");
        Assert.NotNull(new GameSession(sim).DescribeRivalIntelligence(1)!.Research);
        Apply(sim, new ExecuteEspionageMissionCommand
            { Player = 1, Unit = defender.Id, City = home.Id, Mission = EspionageMissionKind.CounterEspionage });
        Assert.True(sim.Events.Published.OfType<EspionageMissionResult>().Single().Success);
        Assert.Equal(316, Service<IEspionageService>(sim).Points(sim, 1, 0));
        int expiry = sim.State.Turn + 8;
        for (int i = 0; i < 3; i++) sim.AdvanceTurn();
        var resumed = SaveGame.Load(sim.Content, SaveGame.Save(sim));
        var sessions = new[] { new GameSession(sim), new GameSession(resumed) };
        while (sim.State.Turn < expiry)
        {
            foreach (var session in sessions)
            {
                var game = session.Sim;
                var intel = session.DescribeRivalIntelligence(1)!;
                Assert.Equal(new CounterEspionageEffect(expiry, 200), intel.RivalProtection);
                Assert.Equal(new long[] { 100, 200, 300, 400 }, intel.Permissions.Select(p => p.Threshold));
                Assert.NotNull(Assert.Single(intel.Cities!).Details);
                if (game.State.Turn != expiry - 1) continue;
                var spy = P6RulesScenario.AddUnit(game, 0, game.State.CityById(target.Id), "military:spy");
                long victim = game.State.PlayerById(1).Treasury, own = game.State.PlayerById(0).Treasury;
                Apply(game, new ExecuteEspionageMissionCommand
                    { Player = 0, Unit = spy.Id, City = target.Id, Mission = EspionageMissionKind.StealTreasury });
                Assert.True(game.Events.Published.OfType<EspionageMissionResult>().Last().Success);
                Assert.Equal(own + victim / 4, game.State.PlayerById(0).Treasury);
                Assert.Equal(victim - victim / 4, game.State.PlayerById(1).Treasury);
                var spent = session.DescribeRivalIntelligence(1)!;
                Assert.Equal(300, spent.Points); // 50 base * neutral ratio * 200% protection.
                Assert.Null(Assert.Single(spent.Cities!).Details);
                Assert.NotNull(Assert.Single(intel.Cities!).Details); // Detached old snapshot.
            }
            SameReplay(sim, resumed);
            sim.AdvanceTurn(); resumed.AdvanceTurn();
            SameReplay(sim, resumed);
        }
        foreach (var session in sessions)
        {
            var intel = session.DescribeRivalIntelligence(1)!;
            Assert.Null(intel.RivalProtection);
            Assert.Equal(300, intel.Points);
            Assert.NotNull(Assert.Single(intel.Cities!).Details);
            Assert.Equal(50, Service<IEspionageService>(session.Sim).MissionCost(session.Sim, 0, target.Id, EspionageMissionKind.StealTreasury));
            foreach (var unit in session.Sim.State.Units.Where(u => u.Owner == 1).ToArray()) session.Sim.State.RemoveUnit(unit.Id);
            session.Sim.State.RemoveCity(target.Id);
            Assert.Null(session.DescribeRivalIntelligence(1));
            Assert.Empty(session.DescribeEspionage().Rivals);
        }
        SameReplay(sim, resumed);
    }

    // Separate command owners can accidentally share an overbroad gate. The existing diplomacy and
    // theft tests never combine them. A rejected trade must neither grant knowledge nor poison theft.
    [Theory]
    [InlineData(false, "no team has researched the technology enabling technology trading")]
    [InlineData(true, "technology trading is disabled")]
    public void Denied_technology_trade_does_not_block_independently_eligible_theft(bool disabled, string reason)
    {
        var sim = Scenario(); Budget(sim, 0, 100000);
        sim.Settings.GameOptions["foundation:gameoption_no_tech_trading"] = disabled;
        if (disabled) sim.TeamOf(1).Techs.Add(sim.Content.Get<S.TechDef>().All.First(t => t.TechTrading).Id);
        sim.TeamOf(1).Techs.UnionWith([Agriculture, "research:assembly_line"]);
        var target = sim.State.Cities.Single(c => c.Owner == 1);
        var spy = P6RulesScenario.AddUnit(sim, 0, target, "military:spy");
        var rng = sim.Rng.State;
        Assert.Equal(reason, P6RulesScenario.Reject(sim, new ProposeDealCommand
        {
            Player = 1, Target = 0, Items = [new(1, TradeItemKind.Technology, Agriculture)],
        }));
        Assert.Equal(rng, sim.Rng.State);
        Assert.Empty(Service<IDiplomacyService>(sim).Deals(sim));
        Assert.DoesNotContain(Agriculture, sim.TeamOf(0).Techs);
        Assert.Contains("prerequisites", P6RulesScenario.Reject(sim, new ExecuteEspionageMissionCommand
            { Player = 0, Unit = spy.Id, City = target.Id, Mission = EspionageMissionKind.StealTech, Tech = "research:assembly_line" }));
        var resumed = SaveGame.Load(sim.Content, SaveGame.Save(sim));
        foreach (var game in new[] { sim, resumed })
        {
            Apply(game, new ExecuteEspionageMissionCommand
                { Player = 0, Unit = spy.Id, City = target.Id, Mission = EspionageMissionKind.StealTech, Tech = Agriculture });
            Assert.True(game.Events.Published.OfType<EspionageMissionResult>().Single().Success);
            Assert.Contains(Agriculture, game.TeamOf(0).Techs);
            Assert.DoesNotContain("research:assembly_line", game.TeamOf(0).Techs);
            Assert.Empty(Service<IDiplomacyService>(game).Deals(game));
        }
        SameReplay(sim, resumed);
    }

    // Differential lifecycle boundary: neither ordinary hidden edits nor presentation queries on only
    // one peer may enter remembered overlays or locked intelligence after reload and turn settlement.
    [Fact]
    public void Hidden_world_and_queue_edits_stay_private_through_reload_turns_and_presentation_changes()
    {
        var sim = Scenario(); Budget(sim, 0, 0);
        var sight = Service<IVisibilityService>(sim);
        var target = sim.State.Cities.Single(c => c.Owner == 1);
        var tile = sim.State.Grid.TileAt(target.Position);
        var bonus = sim.Content.Get<S.BonusDef>().All.First(b => b.RevealTech == null);
        sim.State.Plots[tile.Value].Bonus = bonus.Id;
        var observer = P6RulesScenario.AddUnit(sim, 0, target, "military:warrior");
        sight.Recompute(sim);
        sim.State.MoveUnit(observer.Id, sim.State.Cities.Single(c => c.Owner == 0).Position);
        sight.Recompute(sim);
        var baseline = new GameSession(sim);
        Assert.Equal(new TileOverlay(Ariel.Client.Sight.Fogged, bonus.Id, 1, null), baseline.OverlayOf(tile));
        string locked = CanonicalJson.Serialize(baseline.DescribeRivalIntelligence(1));
        var remembered = baseline.OverlayOf(tile);
        var mutated = SaveGame.Load(sim.Content, SaveGame.Save(sim));
        var hidden = mutated.State.Plots[tile.Value];
        hidden.Bonus = null; hidden.Owner = 0; hidden.Terrain = "world:ocean";
        hidden.Kind = PlotKind.Peak; hidden.Feature = "world:jungle";
        mutated.State.CityById(target.Id).ProductionQueue.Add(new("missing:private_kind", "missing:private_item", 999));
        var privateSnapshot = new GameSession(SaveGame.Load(mutated.Content, SaveGame.Save(mutated)));
        Assert.Equal(locked, CanonicalJson.Serialize(privateSnapshot.DescribeRivalIntelligence(1)));
        Assert.Equal(remembered, privateSnapshot.OverlayOf(tile));
        // Invalid private queue proves the locked query never resolves hidden content. Restore a valid
        // queue for settlement; turn systems legitimately own and evaluate the real production queue.
        mutated.State.CityById(target.Id).ProductionQueue.Clear();
        mutated.State.CityById(target.Id).ProductionQueue.Add(new("cities:unit", "military:warrior", 9));
        var withUi = new GameSession(SaveGame.Load(mutated.Content, SaveGame.Save(mutated)));
        var withoutUi = SaveGame.Load(mutated.Content, SaveGame.Save(mutated));
        for (int turn = 0; turn < 3; turn++)
        {
            withUi.Overlays = new(turn % 2 == 0, false, true, false);
            withUi.Summary(); withUi.DescribeEspionage(); withUi.DescribeRivalIntelligence(1);
            Assert.Equal(remembered, withUi.OverlayOf(tile));
            Assert.Equal(locked, CanonicalJson.Serialize(withUi.DescribeRivalIntelligence(1)));
            SameReplay(withoutUi, withUi.Sim);
            withoutUi.AdvanceTurn(); withUi.EndTurn(); // Presentation changes on one peer only.
            SameReplay(withoutUi, withUi.Sim);
        }
        Assert.Equal(remembered, withUi.OverlayOf(tile));
        Assert.Equal(new MapOverlays(true, false, true, false), withUi.Overlays);
        Assert.NotEqual(sim.ComputeHash(), withUi.Sim.ComputeHash());
    }
}
