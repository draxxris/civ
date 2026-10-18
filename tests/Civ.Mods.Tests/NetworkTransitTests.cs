using System.Collections.Immutable;
using Civ.Client;
using Civ.Content;
using Civ.Game;
using Civ.Modding;
using Xunit;
using S = Civ.Content.Schema;

namespace Civ.Mods.Tests;

public sealed class NetworkTransitTests(LoadedMods mods) : IClassFixture<LoadedMods>
{
    private const string Grain = "network_probe:grain";
    private const string Road = "world:route_route_road";
    private readonly Dictionary<bool, ContentDb> contents = [];
    private ContentDb Content(bool diplomacy = false)
    {
        if (contents.TryGetValue(diplomacy, out var content)) return content;
        var sources = mods.Sources.AsEnumerable();
        if (diplomacy) sources = sources.Append(ModLoader.FromDirectory(Path.Combine(AppContext.BaseDirectory, "optional-mods", "Diplomacy")));
        var probe = new ModSource { Root = mods.Root, Manifest = new() { Id = "network_probe", Version = "1", Dependencies = ["foundation"], References = ["foundation", "world", "military"] }, Instance = new Probe(mods.Content.Get<S.RuleConstantsDef>().All.Single()) };
        return contents[diplomacy] = ModLoader.Build(sources.Append(probe));
    }
    private Simulation Scenario(bool diplomacy = false)
    {
        var sim = GameFactory.Scenario(Content(diplomacy));
        GameFactory.NeutralPolitics(sim);
        sim.State.Cities.Clear(); sim.State.RebuildIndexes();
        foreach (var plot in sim.State.Plots) { plot.Owner = null; plot.Revealed.Add(0); plot.AreaId = 1; }
        sim.TeamOf(0).Techs.UnionWith(["network_probe:reveal", "network_probe:trade"]);
        return sim;
    }
    private static T Service<T>(Simulation sim) where T : class => sim.Content.Services.Get<T>().Single();
    private static void Apply(Simulation sim, GameCommand command) => Assert.True(sim.TryApply(command, out var error), error);
    private static City City(Simulation sim, int tile, int owner = 0)
    {
        var city = new City { Id = sim.State.NextCityId++, Owner = owner, Position = sim.State.Grid.HexOf(new(tile)), Population = 3 };
        city.Extensions.Set(sim.Content, new CityRuleState { ManualWork = true, AvoidGrowth = true });
        sim.State.AddCity(city); sim.State.Plots[tile].Owner = owner;
        return city;
    }
    private static Unit Ship(Simulation sim, int tile, int owner = 1, string type = "network_probe:ship")
    {
        var unit = new Unit { Id = sim.State.NextUnitId++, Owner = owner, Position = sim.State.Grid.HexOf(new(tile)), Type = type };
        sim.State.AddUnit(unit); Service<IUnitService>(sim).Initialize(sim, unit.Id);
        return unit;
    }
    private static SetUnitActivityCommand Activity(Unit unit, UnitActivity activity = UnitActivity.Blockade) => new() { Player = unit.Owner, Unit = unit.Id, Activity = activity };
    private static void Refill(Simulation sim, Unit unit) => unit.MovesLeft = Service<IUnitService>(sim).MaximumMoves(sim, unit.Id);

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Peaceful_revealed_foreign_corridors_carry_resources_without_a_treaty(bool diplomacy)
    {
        var sim = Scenario(diplomacy); var home = City(sim, 40); var colony = City(sim, 44);
        for (int i = 40; i <= 44; i++) sim.State.Plots[i].Route = Road;
        for (int i = 41; i <= 43; i++) sim.State.Plots[i].Owner = 1;
        sim.State.Plots[44].Bonus = Grain;
        var network = Service<IResourceNetwork>(sim); var connections = Service<ICityConnectionService>(sim);
        if (diplomacy) Assert.False(Service<IDiplomacyService>(sim).HasOpenBorders(sim, 0, 1));
        void Check(bool connected)
        {
            Assert.Equal(connected, connections.Connected(sim, home.Id, colony.Id));
            Assert.Equal(connected ? 1 : 0, network.ConnectedCount(sim, 0, home.Id, Grain));
        }
        Check(true);
        sim.State.Plots[42].Revealed.Remove(0); Check(false);
        sim.State.Plots[42].Revealed.Add(0); Check(true);
        sim.State.SetWar(0, 1, true); Check(false);
        sim.State.SetWar(0, 1, false); Check(true);
        sim.State.Plots[42].Route = null; Check(false);
        sim.State.Plots[42].Route = Road; Check(true);
    }
    [Fact]
    public void Physical_foreign_city_connection_does_not_grant_international_city_trade()
    {
        var sim = Scenario(true); var home = City(sim, 40); var foreign = City(sim, 42, 1);
        for (int i = 40; i <= 42; i++) sim.State.Plots[i].Route = Road;
        var diplomacy = Service<IDiplomacyService>(sim); diplomacy.EstablishContact(sim, 0, 1);
        Assert.True(Service<ICityConnectionService>(sim).Connected(sim, home.Id, foreign.Id));
        Assert.Empty(Service<ITradeRouteService>(sim).Calculate(sim, home.Id).Routes);
        sim.TeamOf(0).Techs.Add("research:writing");
        Apply(sim, new SignOpenBordersCommand { Player = 0, Target = 1 });
        Assert.Equal(foreign.Id, Assert.Single(Service<ITradeRouteService>(sim).Calculate(sim, home.Id).Routes).PartnerCity);
    }
    [Fact]
    public void Coastal_blockade_stops_city_trade_even_when_the_land_network_stays_connected()
    {
        var sim = Scenario(); var home = City(sim, 40); var partner = City(sim, 44);
        for (int i = 40; i <= 44; i++) sim.State.Plots[i].Route = Road;
        sim.State.Plots[28].Terrain = "world:coast"; sim.State.Plots[28].AreaId = 2;
        var blocker = Ship(sim, 28);
        Apply(sim, Activity(blocker)); // A peaceful order has no effect until war begins.
        Assert.Single(Service<ITradeRouteService>(sim).Calculate(sim, home.Id).Routes);
        sim.State.SetWar(0, 1, true);
        var connections = Service<ICityConnectionService>(sim);
        Assert.True(connections.IsCityBlockaded(sim, home.Id));
        Assert.False(connections.IsCityBlockaded(sim, partner.Id));
        Assert.True(connections.Connected(sim, home.Id, partner.Id));
        Assert.Empty(Service<ITradeRouteService>(sim).Calculate(sim, home.Id).Routes);
        Assert.Empty(Service<ITradeRouteService>(sim).Calculate(sim, partner.Id).Routes);
        sim.State.SetWar(0, 1, false);
        Assert.Single(Service<ITradeRouteService>(sim).Calculate(sim, home.Id).Routes);
    }
    private (Simulation Sim, City Home, City Colony, Unit Blocker) SeaScenario()
    {
        var sim = Scenario(); var home = City(sim, 40); var colony = City(sim, 44);
        home.Buildings.Add("network_probe:vicinity");
        for (int i = 41; i <= 43; i++) { sim.State.Plots[i].Terrain = "world:coast"; sim.State.Plots[i].Owner = 0; sim.State.Plots[i].AreaId = 2; }
        sim.State.Plots[44].Bonus = Grain;
        sim.State.Plots[41].Bonus = Grain; sim.State.Plots[41].Improvement = "network_probe:farm";
        sim.State.Plots[42].Owner = null;
        sim.State.SetWar(0, 1, true);
        return (sim, home, colony, Ship(sim, 42));
    }
    [Fact]
    public void Blockade_command_severs_sea_network_work_and_both_trade_endpoints_and_survives_reload()
    {
        var (sim, home, colony, blocker) = SeaScenario();
        var assign = new SetCityAssignmentsCommand { Player = 0, CityId = home.Id, Tiles = [sim.State.Grid.HexOf(new(41))] };
        Apply(sim, assign);
        var connections = Service<ICityConnectionService>(sim);
        Assert.True(connections.Connected(sim, home.Id, colony.Id));
        Assert.Equal(2, Service<IResourceNetwork>(sim).ConnectedCount(sim, 0, home.Id, Grain));
        Assert.True(Vicinity(sim, home)); Assert.Equal(7, VicinityYield(sim, home));
        Assert.Single(Service<ITradeRouteService>(sim).Calculate(sim, home.Id).Routes);
        Assert.Single(Service<ITradeRouteService>(sim).Calculate(sim, colony.Id).Routes);
        var before = Service<ICityYieldService>(sim).Calculate(sim, home.Id).Yields;
        Apply(sim, Activity(blocker));
        Assert.Equal(0, blocker.MovesLeft); Assert.True(blocker.Extensions.Get<MilitaryUnitState>(sim.Content)!.Moved);
        ulong hash = sim.ComputeHash(); var rng = sim.Rng.State;
        Assert.True(connections.IsTileBlockaded(sim, new(41), 0)); Assert.True(connections.IsCityBlockaded(sim, home.Id));
        Assert.False(connections.Connected(sim, home.Id, colony.Id));
        Assert.Equal(0, Service<IResourceNetwork>(sim).ConnectedCount(sim, 0, home.Id, Grain));
        Assert.False(Vicinity(sim, home)); Assert.Equal(0, VicinityYield(sim, home));
        Assert.Contains("unavailable", assign.Validate(sim)!);
        Assert.Empty(Service<ITradeRouteService>(sim).Calculate(sim, home.Id).Routes);
        Assert.Empty(Service<ITradeRouteService>(sim).Calculate(sim, colony.Id).Routes);
        Assert.True(Service<ICityYieldService>(sim).Calculate(sim, home.Id).Yields.Food < before.Food);
        Assert.Equal(hash, sim.ComputeHash()); Assert.Equal(rng, sim.Rng.State);
        var loaded = SaveGame.Load(sim.Content, SaveGame.Save(sim));
        Assert.Equal(hash, loaded.ComputeHash());
        Assert.True(Service<ICityConnectionService>(loaded).IsCityBlockaded(loaded, home.Id));
        Service<ICityWorkService>(sim).Assign(sim, home.Id);
        Service<ICityWorkService>(loaded).Assign(loaded, home.Id);
        Assert.Empty(home.Worked);
        sim.AdvanceTurn(); loaded.AdvanceTurn(); Assert.Equal(sim.ComputeHash(), loaded.ComputeHash());
        Assert.True(connections.IsCityBlockaded(sim, home.Id));
        Apply(sim, Activity(blocker, UnitActivity.Awake));
        Assert.False(connections.IsCityBlockaded(sim, home.Id));
        Assert.True(connections.Connected(sim, home.Id, colony.Id));
    }
    [Theory]
    [InlineData("peace")]
    [InlineData("sleep")]
    [InlineData("move")]
    [InlineData("load")]
    [InlineData("death")]
    [InlineData("disband")]
    [InlineData("lake")]
    [InlineData("land")]
    [InlineData("team_territory")]
    public void Blockade_effects_follow_current_unit_and_war_lifecycle(string change)
    {
        var (sim, home, _, blocker) = SeaScenario();
        Apply(sim, Activity(blocker));
        var connections = Service<ICityConnectionService>(sim);
        Assert.True(connections.IsCityBlockaded(sim, home.Id));
        Refill(sim, blocker);
        switch (change)
        {
            case "peace": sim.State.SetWar(0, 1, false); break;
            case "sleep": Apply(sim, Activity(blocker, UnitActivity.Sleep)); break;
            case "move": Apply(sim, new MoveUnitPathCommand { Player = 1, Unit = blocker.Id, Path = [sim.State.Grid.HexOf(new(43))] }); break;
            case "load":
                var carrier = Ship(sim, 42, type: "network_probe:carrier");
                Apply(sim, new LoadUnitCommand { Player = 1, Unit = blocker.Id, Transport = carrier.Id }); break;
            case "death": blocker.Health = 0; break;
            case "disband": Apply(sim, new DisbandUnitCommand { Player = 1, Unit = blocker.Id }); break;
            case "lake": sim.State.Plots[42].IsLake = true; break;
            case "land": sim.State.Plots[42].Terrain = "world:grass"; break;
            case "team_territory": sim.State.Plots[42].Owner = 1; break;
        }
        Assert.False(connections.IsCityBlockaded(sim, home.Id));
        if (change == "peace") { sim.State.SetWar(0, 1, true); Assert.True(connections.IsCityBlockaded(sim, home.Id)); }
    }
    [Theory]
    [InlineData("land", "non-lake water")]
    [InlineData("lake", "non-lake water")]
    [InlineData("team", "team territory")]
    [InlineData("moves", "No moves")]
    [InlineData("cargo", "Cargo")]
    [InlineData("civilian", "cannot blockade")]
    [InlineData("soldier", "cannot blockade")]
    public void Invalid_blockade_orders_are_rejected_without_state_changes(string reason, string message)
    {
        var (sim, _, _, blocker) = SeaScenario();
        switch (reason)
        {
            case "land": sim.State.Plots[42].Terrain = "world:grass"; break;
            case "lake": sim.State.Plots[42].IsLake = true; break;
            case "team": sim.State.Plots[42].Owner = 1; break;
            case "moves": blocker.MovesLeft = 0; break;
            case "cargo": blocker.Extensions.Get<MilitaryUnitState>(sim.Content)!.TransportId = 99; break;
            case "civilian": sim.State.RemoveUnit(blocker.Id); blocker = Ship(sim, 42, type: "network_probe:carrier"); break;
            case "soldier": sim.State.RemoveUnit(blocker.Id); blocker = Ship(sim, 42, type: "military:warrior"); break;
        }
        ulong hash = sim.ComputeHash();
        Assert.False(sim.TryApply(Activity(blocker), out var error)); Assert.Contains(message, error!);
        Assert.Equal(hash, sim.ComputeHash());
    }
    [Fact]
    public void Blockade_range_uses_water_paths_and_does_not_reach_another_area_or_friendly_team()
    {
        var sim = Scenario();
        foreach (int tile in new[] { 40, 41, 42, 43, 44, 45 }) { sim.State.Plots[tile].Terrain = "world:coast"; sim.State.Plots[tile].AreaId = 2; }
        var blocker = Ship(sim, 40); sim.State.SetWar(0, 1, true); Apply(sim, Activity(blocker));
        var connections = Service<ICityConnectionService>(sim);
        Assert.True(connections.IsTileBlockaded(sim, new(43), 0));
        Assert.False(connections.IsTileBlockaded(sim, new(44), 0));
        Assert.False(connections.IsTileBlockaded(sim, new(41), 1));
        sim.State.Plots[43].AreaId = 3; Assert.False(connections.IsTileBlockaded(sim, new(43), 0));
        sim.State.Plots[43].AreaId = 2; sim.State.Plots[41].Terrain = "world:grass";
        Assert.False(connections.IsTileBlockaded(sim, new(42), 0));
    }
    [Fact]
    public void Client_blockade_action_executes_a_real_order_and_disappears_when_unavailable()
    {
        var sim = Scenario(); sim.State.Plots[42].Terrain = "world:coast";
        var unit = Ship(sim, 42, 0); var session = new GameSession(sim); session.SelectUnit(unit.Id);
        Assert.IsType<SetUnitActivityCommand>(Assert.Single(session.UnitActions(unit, session.DefOf(unit)), a => a.Id == "blockade").Command);
        session.RunAction("blockade");
        Assert.Equal(UnitActivity.Blockade, unit.Extensions.Get<MilitaryUnitState>(sim.Content)!.Activity);
        Assert.Equal(0, unit.MovesLeft);
        Assert.DoesNotContain(session.UnitActions(unit, session.DefOf(unit)), a => a.Id == "blockade");
    }
    private static bool Vicinity(Simulation sim, City city, int count = 1) => sim.Content.Requirements.Evaluate(new Requirement.Leaf("bonus", Grain, count, 2), new(sim, city.Owner, city)).Success;
    private static long VicinityYield(Simulation sim, City city) => Service<ICityYieldService>(sim).Calculate(sim, city.Id).Ledger.Lines
        .Where(l => l.Source == "network_probe:vicinity:vicinity:" + Grain && l.Key == "yield.flat.production").Sum(l => l.Amount);
    [Theory]
    [InlineData("valid", true)]
    [InlineData("valid_not_connector", true)]
    [InlineData("unimproved", false)]
    [InlineData("wrong_improvement", false)]
    [InlineData("disconnected", false)]
    [InlineData("foreign", false)]
    [InlineData("hidden", false)]
    [InlineData("obsolete", false)]
    [InlineData("trade_tech", false)]
    [InlineData("suppressed", false)]
    public void Vicinity_requirements_and_yields_share_valid_connected_available_bonus_rules(string change, bool expected)
    {
        var sim = Scenario(); var city = City(sim, 40); city.Buildings.Add("network_probe:vicinity");
        foreach (int tile in new[] { 37, 38, 39, 40, 41 }) { sim.State.Plots[tile].Owner = 0; sim.State.Plots[tile].Route = Road; }
        foreach (int tile in new[] { 37, 41 }) { sim.State.Plots[tile].Bonus = Grain; sim.State.Plots[tile].Improvement = "network_probe:farm"; }
        Assert.True(Vicinity(sim, city)); Assert.Equal(7, VicinityYield(sim, city));
        switch (change)
        {
            case "valid_not_connector": sim.State.Plots[41].Improvement = "network_probe:valid_only"; break;
            case "unimproved": sim.State.Plots[41].Improvement = null; break;
            case "wrong_improvement": sim.State.Plots[41].Improvement = "network_probe:connector"; break;
            case "disconnected": sim.State.Plots[41].Route = null; break;
            case "foreign": sim.State.Plots[41].Owner = 1; break;
            case "hidden": sim.TeamOf(0).Techs.Remove("network_probe:reveal"); break;
            case "obsolete": sim.TeamOf(0).Techs.Add("network_probe:obsolete"); break;
            case "trade_tech": sim.TeamOf(0).Techs.Remove("network_probe:trade"); break;
            case "suppressed": city.Buildings.Add("network_probe:suppress"); break;
        }
        // The remote copy keeps the ledger positive for invalid local-source cases, but is outside this city's radius.
        if (change is "unimproved" or "wrong_improvement" or "disconnected" or "foreign") Assert.True(Service<IResourceNetwork>(sim).ConnectedCount(sim, 0, city.Id, Grain) > 0);
        ulong hash = sim.ComputeHash();
        Assert.Equal(expected, Vicinity(sim, city)); Assert.Equal(expected ? 7 : 0, VicinityYield(sim, city));
        Assert.Equal(hash, sim.ComputeHash());
        var loaded = SaveGame.Load(sim.Content, SaveGame.Save(sim));
        Assert.Equal(expected, Vicinity(loaded, loaded.State.CityById(city.Id)));
        sim.State.Plots[41].Improvement = "network_probe:farm"; sim.State.Plots[41].Route = Road; sim.State.Plots[41].Owner = 0;
        sim.TeamOf(0).Techs.UnionWith(["network_probe:reveal", "network_probe:trade"]); sim.TeamOf(0).Techs.Remove("network_probe:obsolete"); city.Buildings.Remove("network_probe:suppress");
        Assert.True(Vicinity(sim, city)); Assert.Equal(7, VicinityYield(sim, city));
        Assert.False(Vicinity(sim, city, 2));
    }
    [Theory]
    [InlineData("center", 1)]
    [InlineData("free_zero", 0)]
    [InlineData("free_two", 2)]
    [InlineData("free_extra", 3)]
    [InlineData("free_world", -1)]
    [InlineData("free_world", -1, "foundation:worldsize_duel")]
    [InlineData("free_world", -1, "foundation:worldsize_gigantic")]
    public void City_centers_and_active_local_building_grants_use_actual_copy_counts(string source, int copies,
        string worldSize = "foundation:worldsize_standard")
    {
        var sim = Scenario();
        var save = System.Text.Json.Nodes.JsonNode.Parse(SaveGame.Save(sim))!;
        save["Settings"]!["WorldSize"] = worldSize;
        sim = SaveGame.Load(sim.Content, save.ToJsonString());
        var city = City(sim, 40); city.Buildings.Add("network_probe:vicinity");
        if (source == "center") sim.State.Plots[40].Bonus = Grain;
        else city.Buildings.Add("network_probe:" + source);
        int expected = copies == -1 ? sim.Content.Get<S.WorldSizeDef>(sim.Settings.WorldSize!).NumFreeBuildingBonuses : copies;
        Assert.Equal(expected > 0, Vicinity(sim, city)); Assert.Equal(expected > 0 ? 7 : 0, VicinityYield(sim, city));
        Assert.Equal(expected > 0 ? 2 : 0, Service<ICityWellbeingService>(sim).EnvironmentalUnhealth(sim, city.Id).Bonuses);
        if (expected > 0) Assert.True(Vicinity(sim, city, expected));
        Assert.False(Vicinity(sim, city, expected + 1));
        city.Buildings.Add("network_probe:suppress"); Assert.False(Vicinity(sim, city));
        city.Buildings.Remove("network_probe:suppress");
        if (source != "center")
        {
            sim.TeamOf(0).Techs.Add("network_probe:building_obsolete");
            Assert.False(Vicinity(sim, city)); Assert.Equal(0, VicinityYield(sim, city));
            sim.TeamOf(0).Techs.Remove("network_probe:building_obsolete");
        }
        sim.TeamOf(0).Techs.Add("network_probe:obsolete"); Assert.False(Vicinity(sim, city)); Assert.Equal(0, VicinityYield(sim, city));
    }
    private sealed class Probe(S.RuleConstantsDef original) : IMod
    {
        public void Register(IModContext context)
        {
            context.Content<S.RuleConstantsDef>().Override(new() { Id = original.Id, Values = original.Values, Raw = original.Raw.SetItem("INITIAL_TRADE_ROUTES", "1").SetItem("MAX_TRADE_ROUTES", "2").SetItem("SHIP_BLOCKADE_RANGE", "3") });
            foreach (string name in new[] { "reveal", "trade", "obsolete", "building_obsolete" })
                context.Content<S.TechDef>().Register(new() { Id = "network_probe:" + name,
                    TradeTerrains = name == "trade" ? new Dictionary<string, bool> { ["world:coast"] = true }.ToImmutableDictionary() : ImmutableDictionary<string, bool>.Empty });
            context.Content<S.BonusDef>().Register(new() { Id = Grain, RevealTech = "network_probe:reveal", TradeTech = "network_probe:trade", ObsoleteTech = "network_probe:obsolete", Health = -2 });
            context.Content<S.ImprovementDef>().Register(new() { Id = "network_probe:farm", BonusEffects = new Dictionary<string, S.ImprovementBonus> { [Grain] = new() { MakesValid = true, Connects = true } }.ToImmutableDictionary() });
            context.Content<S.ImprovementDef>().Register(new() { Id = "network_probe:valid_only", BonusEffects = new Dictionary<string, S.ImprovementBonus> { [Grain] = new() { MakesValid = true } }.ToImmutableDictionary() });
            context.Content<S.ImprovementDef>().Register(new() { Id = "network_probe:connector", BonusEffects = new Dictionary<string, S.ImprovementBonus> { [Grain] = new() { Connects = true } }.ToImmutableDictionary() });
            context.Content<S.UnitDef>().Register(new() { Id = "network_probe:ship", DefaultUnit = "network_probe:ship", Domain = "military:domain_sea", Pillage = true, Moves = 2 });
            context.Content<S.UnitDef>().Register(new() { Id = "network_probe:carrier", DefaultUnit = "network_probe:carrier", Domain = "military:domain_sea", Moves = 2, Cargo = 2, CargoDomain = "military:domain_sea" });
            context.Content<S.BuildingDef>().Register(new() { Id = "network_probe:vicinity", DefaultBuilding = "network_probe:vicinity", VicinityBonusYieldChanges = new Dictionary<string, YieldVector> { [Grain] = new(0, 7, 0) }.ToImmutableDictionary() });
            context.Content<S.BuildingDef>().Register(new() { Id = "network_probe:suppress", DefaultBuilding = "network_probe:suppress", NoBonus = Grain });
            foreach (var (name, count) in new[] { ("free_zero", 0), ("free_two", 2), ("free_world", -1) })
                context.Content<S.BuildingDef>().Register(new() { Id = "network_probe:" + name, DefaultBuilding = "network_probe:" + name, FreeBonus = Grain, FreeBonusCount = count, ObsoleteTech = "network_probe:building_obsolete" });
            context.Content<S.BuildingDef>().Register(new() { Id = "network_probe:free_extra", DefaultBuilding = "network_probe:free_extra", ObsoleteTech = "network_probe:building_obsolete", ExtraFreeBonuses = new Dictionary<string, int> { [Grain] = 3 }.ToImmutableDictionary() });
        }
    }
}
