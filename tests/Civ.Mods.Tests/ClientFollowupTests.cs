using Civ.Client;
using Civ.Content;
using Civ.Game;
using Xunit;
using S = Civ.Content.Schema;

namespace Civ.Mods.Tests;

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
        Assert.True(pedia.Search("Buildings").Any(e => e.Cost > 0));
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
        s.Submit(new SetUnitActivityCommand { Player = 0, Unit = AddUnit(s.Sim, s.Sim.State.Cities[0], "military:buddhist_missionary").Id, Activity = UnitActivity.Sleep });
        Assert.Contains(s.Notifications, n => n.Kind == "tech");
        Assert.Contains(s.Notifications, n => n.Kind == "religion" && n.Text.Contains("was founded"));
    }
}
