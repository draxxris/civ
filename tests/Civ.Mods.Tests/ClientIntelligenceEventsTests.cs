using Civ.Client;
using Civ.Content;
using Civ.Game;
using Civ.Modding;
using Xunit;
using S = Civ.Content.Schema;

namespace Civ.Mods.Tests;

/// <summary>Client.Core corporations, espionage and events against the optional P6 mods.</summary>
public sealed class ClientIntelligenceEventsTests(LoadedMods mods) : IClassFixture<LoadedMods>
{
    private static ModSource Optional(string name) => ModLoader.FromDirectory(Path.Combine(AppContext.BaseDirectory, "optional-mods", name));

    private GameSession Session(params string[] optional)
    {
        var sim = GameFactory.Scenario(ModLoader.Build(mods.Sources.Concat(optional.Select(Optional))));
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
