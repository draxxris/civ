using System.Collections.Concurrent;
using Ariel.Client;
using Ariel.Content;
using Ariel.Game;
using Ariel.Mods.Diplomacy;
using Ariel.Modding;
using Ariel.Mods.Corporations;
using Ariel.Mods.Espionage;
using Ariel.Mods.Events;
using Xunit;
using S = Ariel.Content.Schema;

namespace Ariel.Mods.Tests;

/// <summary>Client.Core corporations, espionage and events against the optional P6 mods.</summary>
public sealed class ClientIntelligenceEventsTests
{
    private static readonly ConcurrentDictionary<string, Lazy<ContentDb>> Contents = new(StringComparer.Ordinal);
    private static ModSource Optional(string name) => ModLoader.FromDirectory(Path.Combine(AppContext.BaseDirectory, "optional-mods", name));

    /// <summary>One frozen content per optional-mod set; xunit builds a new test class instance for every test.</summary>
    private static ContentDb Content(params string[] optional) => Contents.GetOrAdd(string.Join(",", optional), _ => new(() =>
    {
        var loaded = new LoadedMods();
        return ModLoader.Build(loaded.Sources.Concat(optional.Select(Optional)));
    })).Value;

    private static GameSession Session(params string[] optional)
    {
        var sim = GameFactory.Scenario(Content(optional));
        GameFactory.NeutralPolitics(sim);
        foreach (var plot in sim.State.Plots) plot.Owner = null;
        foreach (var player in sim.State.Players) player.Treasury = 10000;
        return new GameSession(sim);
    }

    private static City AddCity(Simulation sim, int owner, int tile)
    {
        var city = new City { Id = sim.State.NextCityId++, Owner = owner, Position = sim.State.Grid.HexOf(new TileId(tile)), Population = 3 };
        sim.State.AddCity(city);
        sim.State.PlotAt(city.Position).Owner = owner;
        return city;
    }

    private static Unit AddUnit(Simulation sim, City at, string type, int? owner = null)
    {
        var unit = new Unit { Id = sim.State.NextUnitId++, Owner = owner ?? at.Owner, Type = type, Position = at.Position, MovesLeft = 60 };
        sim.State.AddUnit(unit);
        return unit;
    }

    private static string GreatPersonOf(Simulation sim, string corporation) => sim.Content.Get<CorporationDef>(corporation).RequiredGreatPersonClass;

    private static string Founder(GameSession session, City city, out string corporation)
    {
        var sim = session.Sim;
        var def = sim.Content.Get<CorporationDef>().All.OrderBy(c => c.Id, StringComparer.Ordinal)
            .First(c => c.PrereqBonuses.Length > 0 && c.FoundingTech != null);
        corporation = def.Id;
        sim.TeamOf(city.Owner).Techs.Add(def.FoundingTech!);
        var bonus = sim.Content.Get<S.BonusDef>(def.PrereqBonuses[0]);
        sim.State.PlotAt(city.Position).Bonus = bonus.Id;
        if (bonus.RevealTech is { } reveal) sim.TeamOf(city.Owner).Techs.Add(reveal);
        if (bonus.TradeTech is { } trade) sim.TeamOf(city.Owner).Techs.Add(trade);
        return def.RequiredGreatPersonClass;
    }

    private const string EspionageId = "espionage:screen";
    private const string CorporationsId = "corporations:screen";
    private const string EventsId = "events:screen";

    private static bool Select(GameSession session, int team) => ScreenProbe.Press(session, EspionageId, $"rival:{team}");

    private static long Spending(IReadOnlyList<ViewElement> body)
    {
        var match = System.Text.RegularExpressions.Regex.Match(ScreenProbe.Paragraph(body, "summary")!, @"spending forecast (\d+) points");
        Assert.True(match.Success);
        return long.Parse(match.Groups[1].Value);
    }

    private static bool Locked(IReadOnlyList<ViewElement> body, string section) => ScreenProbe.Paragraph(ScreenProbe.Section(body, section), "locked") is not null;

    private static bool DetailsLocked(IReadOnlyList<ViewElement> body) => ScreenProbe.Paragraph(ScreenProbe.Section(body, "cities"), "details:locked") is not null;

    [Fact]
    public void Screens_are_empty_without_the_optional_mods()
    {
        var session = Session();
        Assert.False(ScreenProbe.Registered(session, CorporationsId));
        Assert.False(ScreenProbe.Registered(session, EspionageId));
        Assert.False(ScreenProbe.Registered(session, EventsId));
        Assert.False(session.Screens.Open(EspionageId));
        Assert.Null(session.Screens.View);
        Assert.DoesNotContain("Corporations", session.Pedia.Categories);
    }

    [Fact]
    public void Corporations_screen_city_screen_and_unit_actions_follow_founding()
    {
        var session = Session("Corporations", "Diplomacy");
        var city = AddCity(session.Sim, 0, 14);
        var great = Founder(session, city, out var corporation);
        string name = session.Sim.Content.Get<CorporationDef>(corporation).Name;
        Assert.NotNull(ScreenProbe.Button(session, CorporationsId));
        Assert.Equal("not founded", ViewWalk.Find<ListItem>(ScreenProbe.Body(session, CorporationsId), corporation)!.Detail);
        Assert.DoesNotContain(session.DescribeCity(city)!.Panels.Status, s => s.Id == "corporations:city");

        var unit = AddUnit(session.Sim, city, great);
        Assert.Contains(session.UnitActions(unit, session.DefOf(unit)), a => a.Id == "corporations:found:" + corporation);
        session.SelectUnit(unit.Id);
        session.RunAction("corporations:found:" + corporation);

        var body = ScreenProbe.Body(session, CorporationsId);
        Assert.Equal($"headquarters {city.Name}, 1 cities", ViewWalk.Find<ListItem>(body, corporation)!.Detail);
        Assert.Equal($"{city.Name}: {name}*", ScreenProbe.Paragraph(body, $"city:{city.Id}"));
        var panel = Assert.Single(session.DescribeCity(city)!.Panels.Status, s => s.Id == "corporations:city");
        var present = Assert.Single(ViewWalk.All([panel]).OfType<Paragraph>());
        Assert.Equal(corporation, present.Id);
        Assert.Contains("headquarters", present.Text);
        Assert.Contains(session.Notifications, n => n.Kind == "corporation");

        var other = AddCity(session.Sim, 0, 18);
        var executive = AddUnit(session.Sim, other, session.Sim.Content.Get<S.UnitDef>().All.First(u => u.Id.Contains("executive")).Id);
        var spread = session.UnitActions(executive, session.DefOf(executive)).Where(a => a.Id.StartsWith("corporations:spread:", StringComparison.Ordinal)).ToList();
        Assert.All(spread, a => Assert.NotNull(a.Command));
    }

    [Fact]
    public void Espionage_screen_lists_met_rivals_and_weights_change_through_commands()
    {
        var session = Session("Espionage", "Diplomacy");
        AddCity(session.Sim, 0, 14); AddCity(session.Sim, 1, 57);
        Assert.NotNull(ScreenProbe.Button(session, EspionageId));
        Assert.Empty(ScreenProbe.ButtonIds(ScreenProbe.Body(session, EspionageId), "rival:"));
        session.Sim.Rules().Relations.EstablishContact(session.Sim, session.MyTeam.Id, session.Sim.TeamOf(1).Id);
        var body = ScreenProbe.Body(session, EspionageId);
        Assert.Equal(["rival:1"], ScreenProbe.ButtonIds(body, "rival:"));
        Assert.Equal("0 points", ScreenProbe.Paragraph(body, "points:1"));
        Assert.Equal("Weight 0", ScreenProbe.Paragraph(body, "weight:1"));
        // Nothing can go below zero, so the lowering steps are disabled with a reason.
        Assert.NotNull(ViewWalk.Find<ActionButton>(body, "lower:1")!.Disabled);
        for (int i = 0; i < 4; i++) Assert.True(ScreenProbe.Press(session, EspionageId, "raise:1"));
        Assert.Equal("Weight 4", ScreenProbe.Paragraph(ScreenProbe.Body(session, EspionageId), "weight:1"));
        Assert.Null(ViewWalk.Find<ActionButton>(ScreenProbe.Body(session, EspionageId), "lower:1")!.Disabled);
        // A bulk step past zero stops at zero.
        Assert.True(ScreenProbe.Press(session, EspionageId, "lower10:1"));
        Assert.Equal("Weight 0", ScreenProbe.Paragraph(ScreenProbe.Body(session, EspionageId), "weight:1"));
        var state = session.MyTeam.Extensions.Get<EspionageTeamState>(session.Sim.Content) ?? new();
        session.MyTeam.Extensions.Set(session.Sim.Content, state);
        state.Points[1] = 100000;
        Assert.Equal("100000 points", ScreenProbe.Paragraph(ScreenProbe.Body(session, EspionageId), "points:1"));
    }

    [Fact]
    public void A_weight_order_for_another_player_is_refused()
    {
        var session = Session("Espionage", "Diplomacy");
        AddCity(session.Sim, 0, 14); AddCity(session.Sim, 1, 57);
        session.Sim.Rules().Relations.EstablishContact(session.Sim, 0, 1);
        session.Screens.Open(EspionageId);
        Assert.False(session.Screens.Press(new SubmitCommand(new SetEspionageWeightCommand { Player = 1, TargetTeam = 0, Weight = 5 })));
        Assert.Equal(0, session.Sim.Service<IEspionageService>()!.Weight(session.Sim, 1, 0));
    }

    [Fact]
    public void Espionage_hides_eliminated_and_barbarian_rivals()
    {
        var session = Session("Espionage", "Diplomacy");
        var sim = session.Sim;
        AddCity(sim, 0, 14);
        AddCity(sim, 1, 57);
        // Team 2 is a barbarian horde that has met us. Barbarians are never espionage targets.
        sim.State.Players.Add(new Player { Id = 2, TeamId = 2, Treasury = 0 });
        sim.State.Teams.Add(new Team { Id = 2, Members = new() { 2 } });
        sim.State.RebuildIndexes();
        GameFactory.MakeBarbarian(sim, 2);
        AddUnit(sim, sim.State.Cities.Single(c => c.Owner == 1), "military:warrior", owner: 2);
        sim.Rules().Relations.EstablishContact(sim, session.MyTeam.Id, sim.TeamOf(1).Id);
        sim.Rules().Relations.EstablishContact(sim, session.MyTeam.Id, 2);
        Assert.Equal(["rival:1"], ScreenProbe.ButtonIds(ScreenProbe.Body(session, EspionageId), "rival:"));

        foreach (var unit in sim.State.Units.Where(u => u.Owner == 1).ToArray()) sim.State.RemoveUnit(unit.Id);
        sim.State.RemoveCity(sim.State.Cities.Single(c => c.Owner == 1).Id);
        var body = ScreenProbe.Body(session, EspionageId);
        Assert.Empty(ScreenProbe.ButtonIds(body, "rival:"));
        Assert.NotNull(ScreenProbe.Paragraph(body, "none"));
    }

    [Fact]
    public void Spies_in_rival_cities_get_mission_actions()
    {
        var session = Session("Espionage", "Diplomacy");
        AddCity(session.Sim, 0, 14);
        var target = AddCity(session.Sim, 1, 57);
        session.Sim.Rules().Relations.EstablishContact(session.Sim, session.MyTeam.Id, session.Sim.TeamOf(1).Id);
        var state = new EspionageTeamState();
        state.Points[1] = 100000;
        session.MyTeam.Extensions.Set(session.Sim.Content, state);
        var spy = AddUnit(session.Sim, target, session.Sim.Content.Get<S.UnitDef>().All.First(u => u.Id.EndsWith(":spy", StringComparison.Ordinal)).Id, 0);
        var actions = session.UnitActions(spy, session.DefOf(spy)).Where(a => a.Id.StartsWith("espionage:", StringComparison.Ordinal)).ToList();
        Assert.NotEmpty(actions);
        Assert.All(actions, a => Assert.IsType<PerformMissionCommand>(a.Command));
        // The screen offers the same missions for the selected spy, and the technology theft.
        session.SelectUnit(spy.Id);
        var body = ScreenProbe.Body(session, EspionageId);
        Assert.NotEmpty(ScreenProbe.ButtonIds(body, "espionage:"));
        Assert.All(ScreenProbe.ButtonIds(body, "espionage:"), id => Assert.IsType<SubmitCommand>(ViewWalk.ActionOf(body, id)));
        var home = AddUnit(session.Sim, session.Sim.State.Cities.First(c => c.Owner == 0), spy.Type, 0);
        Assert.DoesNotContain(session.UnitActions(home, session.DefOf(home)), a => a.Id.StartsWith("espionage:", StringComparison.Ordinal));
        session.SelectUnit(home.Id);
        Assert.Empty(ScreenProbe.ButtonIds(ScreenProbe.Body(session, EspionageId), "espionage:"));
    }

    [Fact]
    public void Espionage_spending_follows_a_loaded_game_on_the_same_turn_with_the_same_rates_and_city_count()
    {
        var session = IntelligenceSession(out var points, out _); var sim = session.Sim;
        points.Points[1] = 100;
        sim.Rules().Economy.SetRates(sim, 0, new(0, 0, 0, 100));
        Assert.True(ScreenProbe.Press(session, EspionageId, "raise:1"));
        long before = Spending(ScreenProbe.Body(session, EspionageId));
        Assert.True(before > 0);
        // Same turn, same rates, same number of cities: only the economy behind the budget differs.
        var richer = SaveGame.Load(sim.Content, SaveGame.Save(sim));
        foreach (var city in richer.State.Cities.Where(c => c.Owner == 0))
            city.Specialists["foundation:specialist_great_diplomat"] = 3; // Settled great people yield commerce.
        var fresh = new GameSession(richer);
        Assert.True(Select(fresh, 1));
        long expected = Spending(ScreenProbe.Body(fresh, EspionageId));
        Assert.NotEqual(before, expected);
        session.ReplaceSimulation(richer);
        // A loaded game starts the screen afresh: nothing is selected until the player picks the rival again.
        Assert.Null(ScreenProbe.Paragraph(ScreenProbe.Body(session, EspionageId), "summary"));
        Assert.True(Select(session, 1));
        Assert.Equal(expected, Spending(ScreenProbe.Body(session, EspionageId)));
    }

    private GameSession IntelligenceSession(out EspionageTeamState points, out City target)
    {
        var session = Session("Espionage", "Diplomacy");
        AddCity(session.Sim, 0, 14); target = AddCity(session.Sim, 1, 57);
        session.Sim.Rules().Relations.EstablishContact(session.Sim, 0, 1);
        points = new EspionageTeamState(); points.Spent[1] = 100000;
        session.MyTeam.Extensions.Set(session.Sim.Content, points);
        Assert.True(Select(session, 1));
        return session;
    }

    [Theory]
    [InlineData(24, false, false, false, false)]
    [InlineData(25, true, false, false, false)]
    [InlineData(49, true, false, false, false)]
    [InlineData(50, true, true, false, false)]
    [InlineData(74, true, true, false, false)]
    [InlineData(75, true, true, true, false)]
    [InlineData(99, true, true, true, false)]
    [InlineData(100, true, true, true, true)]
    public void Rival_sections_obey_each_passive_threshold(long balance, bool demographics, bool cities, bool research, bool investigate)
    {
        var session = IntelligenceSession(out var points, out var target);
        points.Points[1] = balance;
        session.Sim.TeamOf(1).Researching = "research:agriculture";
        session.Sim.TeamOf(1).ResearchProgress = 17;
        target.ProductionQueue.Add(new("cities:unit", "military:warrior", 23));
        ulong hash = session.Sim.ComputeHash(); var rng = session.Sim.Rng.State;
        // The city picker only exists while cities are visible.
        Assert.Equal(cities, ScreenProbe.Press(session, EspionageId, $"city:{target.Id}"));
        var body = ScreenProbe.Body(session, EspionageId);
        Assert.Equal(new[] { demographics, cities, research, investigate },
            new[] { "Demographics", "CityVisibility", "InspectResearch", "InvestigateCity" }
                .Select(kind => ScreenProbe.Paragraph(body, $"permission:{kind}")!.Contains(": unlocked")));
        Assert.Equal(demographics, !Locked(body, "demographics"));
        Assert.Equal(cities, !Locked(body, "cities"));
        Assert.Equal(research, !Locked(body, "research"));
        if (cities) Assert.Equal(investigate, !DetailsLocked(body));
        if (research) Assert.Matches(@"^.+: 17/[1-9]\d* research$", ScreenProbe.Paragraph(ScreenProbe.Section(body, "research"), "current"));
        if (investigate) Assert.Contains("· 23/", ScreenProbe.Paragraph(ScreenProbe.Section(body, "cities"), "queue:0"));
        Assert.Equal(hash, session.Sim.ComputeHash()); Assert.Equal(rng, session.Sim.Rng.State);
    }

    [Fact]
    public void Locked_rival_sections_ignore_hidden_mutations_and_do_not_read_research_or_queues()
    {
        var session = IntelligenceSession(out var points, out var target);
        points.Points[1] = 0;
        string locked = ScreenProbe.Text(session, EspionageId);
        session.Sim.State.PlayerById(1).Treasury = 54321;
        target.Population = 9; target.FoodStored = 321;
        session.Sim.TeamOf(1).Researching = "missing:private_technology";
        target.ProductionQueue.Add(new("missing:private_kind", "missing:private_item", 999));
        ulong hash = session.Sim.ComputeHash(); var rng = session.Sim.Rng.State;
        Assert.Equal(locked, ScreenProbe.Text(session, EspionageId));
        // City presence alone must not read its invalid private queue or private statistics.
        points.Points[1] = 50;
        Assert.True(ScreenProbe.Press(session, EspionageId, $"city:{target.Id}"));
        var visible = ScreenProbe.Body(session, EspionageId);
        Assert.True(Locked(visible, "research")); Assert.True(DetailsLocked(visible));
        Assert.Contains("population 9", ScreenProbe.Paragraph(ScreenProbe.Section(visible, "demographics"), "totals"));
        points.Points[1] = 0;
        Assert.Equal(hash, session.Sim.ComputeHash()); Assert.Equal(rng, session.Sim.Rng.State);
    }

    [Fact]
    public void Shared_team_intelligence_keeps_player_treasuries_and_returns_detached_read_only_data()
    {
        var session = IntelligenceSession(out var points, out var target); var sim = session.Sim;
        sim.State.Players.Add(new Player { Id = 2, TeamId = 1, Treasury = 345 });
        sim.TeamOf(1).Members.Add(2); sim.State.RebuildIndexes();
        var second = AddCity(sim, 2, 80); second.Population = 7;
        sim.State.PlayerById(1).Treasury = 123;
        var team = sim.TeamOf(1); team.Researching = "research:agriculture"; team.ResearchProgress = 17;
        team.Extensions.Set(sim.Content, new ResearchTeamState { Queue = ["research:agriculture", "research:pottery"] });
        target.ProductionQueue.Add(new("cities:unit", "military:warrior", 23));
        points.Points[1] = 100;
        Assert.True(ScreenProbe.Press(session, EspionageId, $"city:{target.Id}"));
        var body = ScreenProbe.Body(session, EspionageId);
        var demographics = ScreenProbe.Section(body, "demographics");
        Assert.Contains("123 gold", ScreenProbe.Paragraph(demographics, "treasury:1"));
        Assert.Contains("345 gold", ScreenProbe.Paragraph(demographics, "treasury:2"));
        Assert.StartsWith("2 cities · population 10", ScreenProbe.Paragraph(demographics, "totals"));
        string Tech(string id) => sim.Content.Get<S.TechDef>(id).Name;
        Assert.Equal($"Research queue: {Tech("research:agriculture")} → {Tech("research:pottery")}",
            ScreenProbe.Paragraph(ScreenProbe.Section(body, "research"), "queue"));
        string snapshot = ViewWalk.Text(body);
        sim.State.PlayerById(2).Treasury = 999; second.Population = 12;
        team.Extensions.Get<ResearchTeamState>(sim.Content)!.Queue.Clear();
        target.ProductionQueue.Clear();
        Assert.Equal(snapshot, ViewWalk.Text(body));
        Assert.Equal("Research queue is empty.", ScreenProbe.Paragraph(ScreenProbe.Section(ScreenProbe.Body(session, EspionageId), "research"), "queue"));
        // Spending points immediately revokes city details, then all private sections.
        points.Points[1] = 99;
        Assert.True(DetailsLocked(ScreenProbe.Body(session, EspionageId)));
        points.Points[1] = 0;
        var locked = ScreenProbe.Body(session, EspionageId);
        Assert.True(Locked(locked, "cities")); Assert.True(Locked(locked, "research")); Assert.True(Locked(locked, "demographics"));
        foreach (var unit in sim.State.Units.Where(u => sim.TeamOf(u.Owner).Id == 1).ToArray()) sim.State.RemoveUnit(unit.Id);
        sim.State.RemoveCity(target.Id); sim.State.RemoveCity(second.Id);
        var gone = ScreenProbe.Body(session, EspionageId);
        Assert.Empty(ScreenProbe.ButtonIds(gone, "rival:"));
        Assert.NotNull(ScreenProbe.Paragraph(gone, "pick"));
    }

    [Fact]
    public void Spending_a_mission_budget_revokes_open_intelligence_and_counter_expiry_refreshes_status()
    {
        var session = IntelligenceSession(out var points, out var target); var sim = session.Sim;
        points.Points[1] = 100;
        sim.Rules().Economy.SetRates(sim, 0, new(0, 0, 0, 100));
        // An unset weight is zero, so the player must ask for the rival before the forecast moves.
        Assert.True(ScreenProbe.Press(session, EspionageId, "raise:1"));
        var spy = AddUnit(sim, target, "military:spy", 0);
        Assert.True(ScreenProbe.Press(session, EspionageId, $"city:{target.Id}"));
        var unlocked = ScreenProbe.Body(session, EspionageId);
        Assert.False(DetailsLocked(unlocked));
        Assert.True(Spending(unlocked) > 0);
        Assert.EndsWith("inactive", ScreenProbe.Paragraph(unlocked, "protection:ours"));
        Assert.EndsWith("inactive", ScreenProbe.Paragraph(unlocked, "protection:theirs"));
        Assert.True(sim.TryApply(EspionageMissions.Order(0, spy.Id, EspionageMissionKind.CounterEspionage), out var error), error);
        Assert.True(sim.Events.Published.OfType<EspionageMissionResult>().Single().Success);
        var refreshed = ScreenProbe.Body(session, EspionageId);
        Assert.StartsWith("50 points", ScreenProbe.Paragraph(refreshed, "summary")); // Flat 100 points at a 50% ratio.
        Assert.True(Locked(refreshed, "research")); Assert.True(DetailsLocked(refreshed));
        Assert.Contains("8 turns remaining", ScreenProbe.Paragraph(refreshed, "protection:ours"));
        Assert.Contains("cost 200% of normal", ScreenProbe.Paragraph(refreshed, "protection:ours"));
        Assert.False(DetailsLocked(unlocked)); // Existing views never alias live state.
        sim.State.Turn += 8;
        Assert.EndsWith("inactive", ScreenProbe.Paragraph(ScreenProbe.Body(session, EspionageId), "protection:ours"));
    }

    [Fact]
    public void Rival_protection_revokes_intelligence_until_its_exclusive_expiry_turn()
    {
        var session = IntelligenceSession(out var points, out var target); var sim = session.Sim;
        points.Points[1] = 100;
        Assert.True(ScreenProbe.Press(session, EspionageId, $"city:{target.Id}"));
        Assert.False(Locked(ScreenProbe.Body(session, EspionageId), "research"));
        var defense = new EspionageTeamState();
        defense.CounterEspionage[0] = new(sim.State.Turn + 1, 200);
        sim.TeamOf(1).Extensions.Set(sim.Content, defense);
        var guarded = ScreenProbe.Body(session, EspionageId);
        Assert.EndsWith("inactive", ScreenProbe.Paragraph(guarded, "protection:ours"));
        Assert.Contains("1 turns remaining", ScreenProbe.Paragraph(guarded, "protection:theirs"));
        Assert.Contains("200%", ScreenProbe.Paragraph(guarded, "protection:theirs"));
        Assert.True(Locked(guarded, "research")); Assert.True(DetailsLocked(guarded));
        sim.State.Turn++;
        var expired = ScreenProbe.Body(session, EspionageId);
        Assert.EndsWith("inactive", ScreenProbe.Paragraph(expired, "protection:theirs"));
        Assert.False(Locked(expired, "research"));
        Assert.False(DetailsLocked(expired));
    }

    [Fact]
    public void Pending_events_are_listed_and_chosen()
    {
        var session = Session("Events");
        var sim = session.Sim;
        AddCity(sim, 0, 14);
        sim.TeamOf(0).Techs.Add("research:agriculture");
        Assert.Null(ScreenProbe.Button(session, EventsId));
        var service = sim.Content.Services.Get<IEventService>().Single();
        for (int i = 0; i < 3000 && service.Pending(sim, 0).Count == 0; i++) sim.Content.TurnSystems.Get("events:turn").Run(sim);
        var pending = service.Pending(sim, 0)[0];
        Assert.NotNull(ScreenProbe.Button(session, EventsId));
        var body = ScreenProbe.Body(session, EventsId);
        var shown = ScreenProbe.Section(body, $"event:{pending.Id}");
        var option = ViewWalk.All(shown).OfType<ActionButton>().First(b => b.Enabled);
        session.Screens.Open(EventsId);
        Assert.True(session.Screens.Press(option.Action));
        Assert.Null(ViewWalk.Find<Section>(ScreenProbe.Body(session, EventsId), $"event:{pending.Id}"));
        Assert.Contains(session.Notifications, n => n.Kind == "event");
        // The button drawn before the choice is stale now: pressing it again does nothing.
        Assert.False(session.Screens.Press(option.Action));
    }

    [Fact]
    public void Pedia_indexes_corporations_projects_victories_and_events()
    {
        var session = Session("Diplomacy", "Corporations", "Events", "ProjectsVictory");
        foreach (var category in new[] { "Corporations", "Projects", "Victories", "Events" })
        {
            Assert.Contains(category, session.Pedia.Categories);
            Assert.NotEmpty(session.Pedia.Search(category));
        }
        var corp = session.Sim.Content.Get<CorporationDef>().All.First();
        Assert.Equal("Corporations", session.Pedia.Find(corp.Id)!.Category);
    }
}
