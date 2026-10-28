using System.Collections.Concurrent;
using Ariel.Client;
using Ariel.Content;
using Ariel.Game;
using Ariel.Modding;
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

    private static string GreatPersonOf(Simulation sim, string corporation) => sim.Content.Get<S.CorporationDef>(corporation).RequiredGreatPersonClass;

    private static string Founder(GameSession session, City city, out string corporation)
    {
        var sim = session.Sim;
        var def = sim.Content.Get<S.CorporationDef>().All.OrderBy(c => c.Id, StringComparer.Ordinal)
            .First(c => c.PrereqBonuses.Length > 0 && c.FoundingTech != null);
        corporation = def.Id;
        sim.TeamOf(city.Owner).Techs.Add(def.FoundingTech!);
        var bonus = sim.Content.Get<S.BonusDef>(def.PrereqBonuses[0]);
        sim.State.PlotAt(city.Position).Bonus = bonus.Id;
        if (bonus.RevealTech is { } reveal) sim.TeamOf(city.Owner).Techs.Add(reveal);
        if (bonus.TradeTech is { } trade) sim.TeamOf(city.Owner).Techs.Add(trade);
        return def.RequiredGreatPersonClass;
    }

    [Fact]
    public void Screens_are_empty_without_the_optional_mods()
    {
        var session = Session();
        Assert.False(session.DescribeCorporations().Available);
        Assert.False(session.DescribeEspionage().Available);
        Assert.Null(session.DescribeRivalIntelligence(1));
        Assert.Empty(session.DescribePendingEvents());
        Assert.False(session.ChooseEventOption("none", "none"));
        Assert.DoesNotContain("Corporations", session.Pedia.Categories);
    }

    [Fact]
    public void Corporations_screen_city_screen_and_unit_actions_follow_founding()
    {
        var session = Session("Corporations", "Diplomacy");
        var city = AddCity(session.Sim, 0, 14);
        var great = Founder(session, city, out var corporation);
        Assert.All(session.DescribeCorporations().Corporations, c => Assert.False(c.Founded));
        Assert.Empty(session.DescribeCity(city)!.Corporations);

        var unit = AddUnit(session.Sim, city, great);
        Assert.Contains(session.UnitActions(unit, session.DefOf(unit)), a => a.Id == "found_corp:" + corporation);
        session.SelectUnit(unit.Id);
        session.RunAction("found_corp:" + corporation);

        var row = session.DescribeCorporations().Corporations.Single(c => c.Id == corporation);
        Assert.True(row.Founded); Assert.Equal(city.Id, row.Headquarters); Assert.Equal(1, row.Branches); Assert.Equal(city.Name, row.HeadquartersName);
        var inCity = Assert.Single(session.DescribeCity(city)!.Corporations);
        Assert.Equal(corporation, inCity.Id); Assert.True(inCity.Headquarters);
        Assert.Equal(city.Id, Assert.Single(session.DescribeCorporations().Cities).CityId);
        Assert.Contains(session.Notifications, n => n.Kind == "corporation");

        var other = AddCity(session.Sim, 0, 18);
        var executive = AddUnit(session.Sim, other, session.Sim.Content.Get<S.UnitDef>().All.First(u => u.Id.Contains("executive")).Id);
        var spread = session.UnitActions(executive, session.DefOf(executive)).Where(a => a.Id.StartsWith("spread_corp:", StringComparison.Ordinal)).ToList();
        Assert.All(spread, a => Assert.NotNull(a.Command));
    }

    [Fact]
    public void Espionage_screen_lists_met_rivals_and_weights_change_through_commands()
    {
        var session = Session("Espionage", "Diplomacy");
        AddCity(session.Sim, 0, 14); AddCity(session.Sim, 1, 57);
        Assert.True(session.DescribeEspionage().Available);
        Assert.Empty(session.DescribeEspionage().Rivals);
        session.Sim.Content.Services.Get<IDiplomacyService>().Single().EstablishContact(session.Sim, session.MyTeam.Id, session.Sim.TeamOf(1).Id);
        var rival = Assert.Single(session.DescribeEspionage().Rivals);
        Assert.Equal(1, rival.TeamId); Assert.Equal(0, rival.Points); Assert.False(rival.Demographics);
        Assert.True(session.SetEspionageWeight(1, 4));
        Assert.Equal(4, Assert.Single(session.DescribeEspionage().Rivals).Weight);
        Assert.False(session.SetEspionageWeight(1, -3));
        var state = session.MyTeam.Extensions.Get<EspionageTeamState>(session.Sim.Content) ?? new();
        session.MyTeam.Extensions.Set(session.Sim.Content, state);
        state.Points[1] = 100000;
        Assert.Equal(100000, session.DescribeEspionage().Rivals[0].Points);
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
        sim.State.PlayerById(2).Extensions.Set(sim.Content, new MilitaryPlayerState { Barbarian = true });
        AddUnit(sim, sim.State.Cities.Single(c => c.Owner == 1), "military:warrior", owner: 2);
        var diplomacy = sim.Content.Services.Get<IDiplomacyService>().Single();
        diplomacy.EstablishContact(sim, session.MyTeam.Id, sim.TeamOf(1).Id);
        diplomacy.EstablishContact(sim, session.MyTeam.Id, 2);
        Assert.Equal(1, Assert.Single(session.DescribeEspionage().Rivals).TeamId);

        foreach (var unit in sim.State.Units.Where(u => u.Owner == 1).ToArray()) sim.State.RemoveUnit(unit.Id);
        sim.State.RemoveCity(sim.State.Cities.Single(c => c.Owner == 1).Id);
        Assert.Empty(session.DescribeEspionage().Rivals);
    }

    [Fact]
    public void Spies_in_rival_cities_get_mission_actions()
    {
        var session = Session("Espionage", "Diplomacy");
        AddCity(session.Sim, 0, 14);
        var target = AddCity(session.Sim, 1, 57);
        session.Sim.Content.Services.Get<IDiplomacyService>().Single().EstablishContact(session.Sim, session.MyTeam.Id, session.Sim.TeamOf(1).Id);
        var state = new EspionageTeamState();
        state.Points[1] = 100000;
        session.MyTeam.Extensions.Set(session.Sim.Content, state);
        var spy = AddUnit(session.Sim, target, session.Sim.Content.Get<S.UnitDef>().All.First(u => u.Id.EndsWith(":spy", StringComparison.Ordinal)).Id, 0);
        var actions = session.UnitActions(spy, session.DefOf(spy)).Where(a => a.Id.StartsWith("spy:", StringComparison.Ordinal)).ToList();
        Assert.NotEmpty(actions);
        Assert.All(actions, a => { Assert.EndsWith(":" + target.Id, a.Id); Assert.IsType<ExecuteEspionageMissionCommand>(a.Command); });
        var home = AddUnit(session.Sim, session.Sim.State.Cities.First(c => c.Owner == 0), spy.Type, 0);
        Assert.DoesNotContain(session.UnitActions(home, session.DefOf(home)), a => a.Id.StartsWith("spy:", StringComparison.Ordinal));
    }

    private GameSession IntelligenceSession(out EspionageTeamState points, out City target)
    {
        var session = Session("Espionage", "Diplomacy");
        AddCity(session.Sim, 0, 14); target = AddCity(session.Sim, 1, 57);
        session.Sim.Content.Services.Get<IDiplomacyService>().Single().EstablishContact(session.Sim, 0, 1);
        points = new EspionageTeamState(); points.Spent[1] = 100000;
        session.MyTeam.Extensions.Set(session.Sim.Content, points);
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
        var dto = session.DescribeRivalIntelligence(1)!;
        Assert.Equal(new[] { demographics, cities, research, investigate }, dto.Permissions.Select(p => p.Unlocked));
        Assert.Equal(demographics, dto.Demographics is not null);
        Assert.Equal(cities, dto.Cities is not null);
        Assert.Equal(research, dto.Research is not null);
        if (dto.Cities is { } known) Assert.Equal(investigate, Assert.Single(known).Details is not null);
        if (research) { Assert.Equal("research:agriculture", dto.Research!.Current!.Id); Assert.Equal(17, dto.Research.Progress); Assert.True(dto.Research.Cost > 0); }
        if (investigate) Assert.Equal(23, Assert.Single(Assert.Single(dto.Cities!).Details!.Queue).Progress);
        Assert.Equal(hash, session.Sim.ComputeHash()); Assert.Equal(rng, session.Sim.Rng.State);
    }

    [Fact]
    public void Locked_rival_sections_ignore_hidden_mutations_and_do_not_read_research_or_queues()
    {
        var session = IntelligenceSession(out var points, out var target);
        points.Points[1] = 0;
        string locked = CanonicalJson.Serialize(session.DescribeRivalIntelligence(1));
        session.Sim.State.PlayerById(1).Treasury = 54321;
        target.Population = 9; target.FoodStored = 321;
        session.Sim.TeamOf(1).Researching = "missing:private_technology";
        target.ProductionQueue.Add(new("missing:private_kind", "missing:private_item", 999));
        ulong hash = session.Sim.ComputeHash(); var rng = session.Sim.Rng.State;
        Assert.Equal(locked, CanonicalJson.Serialize(session.DescribeRivalIntelligence(1)));
        // City presence alone must not read its invalid private queue or private statistics.
        points.Points[1] = 50;
        var visible = session.DescribeRivalIntelligence(1)!;
        Assert.Null(visible.Research); Assert.Null(Assert.Single(visible.Cities!).Details);
        Assert.Equal(9, visible.Demographics!.Population);
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
        var dto = session.DescribeRivalIntelligence(1)!;
        Assert.Equal(new[] { 1, 2 }, dto.Demographics!.Treasuries.Select(t => t.PlayerId));
        Assert.Equal(new long[] { 123, 345 }, dto.Demographics.Treasuries.Select(t => t.Gold));
        Assert.Equal(2, dto.Demographics.Cities); Assert.Equal(10, dto.Demographics.Population);
        Assert.Equal(new[] { "research:agriculture", "research:pottery" }, dto.Research!.Queue.Select(t => t.Id));
        string snapshot = CanonicalJson.Serialize(dto);
        sim.State.PlayerById(2).Treasury = 999; second.Population = 12;
        team.Extensions.Get<ResearchTeamState>(sim.Content)!.Queue.Clear();
        target.ProductionQueue.Clear();
        Assert.Equal(snapshot, CanonicalJson.Serialize(dto));
        Assert.Empty(session.DescribeRivalIntelligence(1)!.Research!.Queue);
        // Spending points immediately revokes city details, then all private sections.
        points.Points[1] = 99;
        Assert.All(session.DescribeRivalIntelligence(1)!.Cities!, c => Assert.Null(c.Details));
        points.Points[1] = 0;
        var locked = session.DescribeRivalIntelligence(1)!;
        Assert.Null(locked.Cities); Assert.Null(locked.Research); Assert.Null(locked.Demographics);
        foreach (var unit in sim.State.Units.Where(u => sim.TeamOf(u.Owner).Id == 1).ToArray()) sim.State.RemoveUnit(unit.Id);
        sim.State.RemoveCity(target.Id); sim.State.RemoveCity(second.Id);
        Assert.Null(session.DescribeRivalIntelligence(1));
        Assert.Null(session.DescribeRivalIntelligence(0)); Assert.Null(session.DescribeRivalIntelligence(99));
    }

    [Fact]
    public void Spending_a_mission_budget_revokes_open_intelligence_and_counter_expiry_refreshes_status()
    {
        var session = IntelligenceSession(out var points, out var target); var sim = session.Sim;
        points.Points[1] = 100;
        sim.Content.Services.Get<IPlayerEconomyService>().Single().SetRates(sim, 0, new(0, 0, 0, 100));
        var spy = AddUnit(sim, target, "military:spy", 0);
        var unlocked = session.DescribeRivalIntelligence(1)!;
        Assert.NotNull(Assert.Single(unlocked.Cities!).Details);
        Assert.True(unlocked.Spending > 0);
        Assert.Null(unlocked.ProtectionAgainstRival); Assert.Null(unlocked.RivalProtection);
        Assert.True(sim.TryApply(new ExecuteEspionageMissionCommand
            { Player = 0, Unit = spy.Id, City = target.Id, Mission = EspionageMissionKind.CounterEspionage }, out var error), error);
        Assert.True(sim.Events.Published.OfType<EspionageMissionResult>().Single().Success);
        var refreshed = session.DescribeRivalIntelligence(1)!;
        Assert.Equal(58, refreshed.Points); // floor(84*50/100) = 42 points spent.
        Assert.Null(refreshed.Research); Assert.Null(Assert.Single(refreshed.Cities!).Details);
        Assert.Equal(new CounterEspionageEffect(sim.State.Turn + 8, 200), refreshed.ProtectionAgainstRival);
        Assert.NotNull(Assert.Single(unlocked.Cities!).Details); // Existing snapshots never alias live state.
        sim.State.Turn += 8;
        Assert.Null(session.DescribeRivalIntelligence(1)!.ProtectionAgainstRival);
    }

    [Fact]
    public void Rival_protection_revokes_intelligence_until_its_exclusive_expiry_turn()
    {
        var session = IntelligenceSession(out var points, out _); var sim = session.Sim;
        points.Points[1] = 100;
        Assert.NotNull(session.DescribeRivalIntelligence(1)!.Research);
        var defense = new EspionageTeamState();
        defense.CounterEspionage[0] = new(sim.State.Turn + 1, 200);
        sim.TeamOf(1).Extensions.Set(sim.Content, defense);
        var protectedRival = session.DescribeRivalIntelligence(1)!;
        Assert.Null(protectedRival.ProtectionAgainstRival);
        Assert.Equal(new CounterEspionageEffect(sim.State.Turn + 1, 200), protectedRival.RivalProtection);
        Assert.Null(protectedRival.Research); Assert.Null(Assert.Single(protectedRival.Cities!).Details);
        sim.State.Turn++;
        var expired = session.DescribeRivalIntelligence(1)!;
        Assert.Null(expired.RivalProtection); Assert.NotNull(expired.Research);
        Assert.NotNull(Assert.Single(expired.Cities!).Details);
    }

    [Fact]
    public void Pending_events_are_listed_and_chosen()
    {
        var session = Session("Events");
        var sim = session.Sim;
        AddCity(sim, 0, 14);
        sim.TeamOf(0).Techs.Add("research:agriculture");
        var service = sim.Content.Services.Get<IEventService>().Single();
        for (int i = 0; i < 3000 && service.Pending(sim, 0).Count == 0; i++) sim.Content.TurnSystems.Get("events:turn").Run(sim);
        var shown = Assert.Single(session.DescribePendingEvents().Take(1));
        Assert.NotEmpty(shown.Options);
        var option = shown.Options.First(o => o.Blocked == null);
        Assert.True(session.ChooseEventOption(shown.Trigger, option.Id));
        Assert.DoesNotContain(session.DescribePendingEvents(), e => e.EventId == shown.EventId);
        Assert.Contains(session.Notifications, n => n.Kind == "event");
        Assert.False(session.ChooseEventOption(shown.Trigger, option.Id) && session.DescribePendingEvents().Any(e => e.EventId == shown.EventId));
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
        var corp = session.Sim.Content.Get<S.CorporationDef>().All.First();
        Assert.Equal("Corporations", session.Pedia.Find(corp.Id)!.Category);
    }
}
