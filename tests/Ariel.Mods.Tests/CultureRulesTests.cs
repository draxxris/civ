using System.Collections.Immutable;
using Ariel.Client;
using Ariel.Content;
using Ariel.Game;
using Ariel.Modding;
using Xunit;
using S = Ariel.Content.Schema;

namespace Ariel.Mods.Tests;

public sealed class CultureRulesTests
{
    private static readonly Lazy<ContentDb> Content = new(() =>
    {
        var loaded = new LoadedMods();
        var victory = ModLoader.FromDirectory(Path.Combine(AppContext.BaseDirectory, "optional-mods", "ProjectsVictory"));
        var probe = new ModSource { Root = loaded.Root, Manifest = new() { Id = "culture_probe", Version = "1",
            Dependencies = ["foundation"], References = ["foundation", "cities", "military", "government", "peoples", "research"] }, Instance = new Probe() };
        return ModLoader.Build(loaded.Sources.Append(victory).Append(probe));
    });
    private static T Service<T>(Simulation sim) where T : class => sim.Content.Services.Get<T>().Single();
    private static Simulation Scenario(string speed = "normal", bool sameTeam = false)
    {
        var sim = new Simulation(Content.Value, new SimulationSettings { Seed = 17, PlayerCount = 2,
            WorldSize = "foundation:worldsize_standard", GameSpeed = "foundation:gamespeed_" + speed,
            Handicap = "foundation:handicap_noble", StartingEra = "foundation:era_ancient", MapGenerator = "world:continents",
            Leaders = ["peoples:leader_julius_caesar", "peoples:leader_julius_caesar"], PlayerTeams = sameTeam ? [0, 0] : [0, 1] });
        sim.State.Map = new(12, 8, true);
        sim.State.Plots = Enumerable.Range(0, 96).Select(_ => new Plot { Terrain = "world:grass" }).ToArray();
        sim.State.Units.Clear(); sim.State.NextUnitId = 1;
        foreach (var team in sim.State.Teams) team.Techs.Clear();
        sim.State.Players = new List<Player> { new() { Id = 0, TeamId = 0, Civilization = "culture_probe:origin", Treasury = 10000 },
            new() { Id = 1, TeamId = sameTeam ? 0 : 1, Civilization = "culture_probe:other", Treasury = 10000 } };
        sim.State.RebuildIndexes();
        return sim;
    }
    private static City AddCity(Simulation sim, int owner, int tile, int population = 5)
    {
        var city = new City { Id = sim.State.NextCityId++, Owner = owner, Position = sim.State.Grid.HexOf(new TileId(tile)), Population = population };
        sim.State.AddCity(city); sim.State.PlotAt(city.Position).Owner = owner;
        city.Extensions.Set(sim.Content, new CityRuleState { ManualWork = true });
        return city;
    }
    private static Unit AddUnit(Simulation sim, int owner, Hex position, string type = "military:warrior")
    {
        var unit = new Unit { Id = sim.State.NextUnitId++, Owner = owner, Position = position, Type = type };
        sim.State.AddUnit(unit); return unit;
    }
    [Theory]
    [InlineData(true, false, false, 40, 2)]
    [InlineData(false, true, false, 40, 2)]
    [InlineData(false, false, true, 40, 2)]
    [InlineData(true, true, true, 1200, 4)]
    public void Founding_consumes_additive_trait_and_extension_culture_levels(bool trait, bool civic, bool civilization, long culture, int level)
    {
        var sim = Scenario();
        sim.State.Players[1] = new Player { Id = 1, TeamId = 1, Leader = trait ? "culture_probe:leader" : "",
            Civilization = civilization ? "culture_probe:boosted" : "culture_probe:other" };
        sim.State.RebuildIndexes();
        if (civic) sim.State.PlayerById(1).Civics["culture_probe:option"] = "culture_probe:boost";
        var settler = AddUnit(sim, 1, sim.State.Grid.HexOf(new TileId(39)), "military:settler");
        Assert.True(sim.TryApply(new FoundCityWithSettlerCommand { Player = 1, Settler = settler.Id }, out var error), error);
        var city = Assert.Single(sim.State.Cities);
        Assert.Equal(culture, city.Culture);
        Assert.Equal(culture * 100, city.Extensions.Get<CityRuleState>(sim.Content)!.CultureHundredths[1]);
        Assert.Equal(level, Service<ICityCultureService>(sim).LevelIndex(sim, city.Id));
        Assert.All(sim.State.Grid.Range(city.Position, level), h => Assert.Equal(1, sim.State.PlotAt(h).Owner));
        var loaded = SaveGame.Load(sim.Content, SaveGame.Save(sim));
        Assert.Equal(sim.ComputeHash(), loaded.ComputeHash());
        sim.AdvanceTurn(); loaded.AdvanceTurn();
        Assert.Equal(sim.ComputeHash(), loaded.ComputeHash());
    }
    [Fact]
    public void Owner_and_city_gated_culture_tier_controls_work_radius_requirements_defense_and_screen()
    {
        var sim = Scenario(); var city = AddCity(sim, 1, 39); city.Culture = 50;
        AddCity(sim, 0, 80);
        sim.State.PlayerById(0).Civics["culture_probe:option"] = "culture_probe:citizens";
        var attacker = AddUnit(sim, 0, city.Position.Neighbor(0));
        var defender = AddUnit(sim, 1, city.Position);
        sim.State.SetWar(0, 1, true);
        var production = Service<ICityProductionService>(sim);
        var session = new GameSession(sim);
        Assert.Equal("Fledgling", session.DescribeCity(city)!.CultureLevel);
        Assert.Equal(2, Service<ICityWorkService>(sim).Radius(sim, city.Id));
        Assert.False(production.Validate(sim, city.Id, "cities:building", "culture_probe:hall").Success);
        Assert.Equal(10, Service<ICombatService>(sim).Preview(sim, attacker.Id, defender.Id).DefenseStrength.Ledger.Lines.Single(l => l.Source == "cities:defense").Amount);
        // Player 0 already has the civic. Player 1 must adopt it, and this city must satisfy the population gate.
        sim.State.PlayerById(1).Civics["culture_probe:option"] = "culture_probe:citizens";
        city.Population = 4;
        Assert.Equal("Fledgling", session.DescribeCity(city)!.CultureLevel);
        city.Population = 5;
        var before = sim.ComputeHash();
        Assert.Equal("Chartered", session.DescribeCity(city)!.CultureLevel);
        Assert.Equal(3, Service<ICityWorkService>(sim).Radius(sim, city.Id));
        Assert.True(production.Validate(sim, city.Id, "cities:building", "culture_probe:hall").Success);
        Assert.Equal(77, Service<ICombatService>(sim).Preview(sim, attacker.Id, defender.Id).DefenseStrength.Ledger.Lines.Single(l => l.Source == "cities:defense").Amount);
        Assert.Equal(before, sim.ComputeHash());
    }
    [Fact]
    public void Legendary_tier_requirements_gate_the_cultural_victory_count()
    {
        var sim = Scenario(); AddCity(sim, 0, 80);
        var cities = new[] { AddCity(sim, 1, 14), AddCity(sim, 1, 39), AddCity(sim, 1, 57) };
        foreach (var city in cities) city.Culture = 240000;
        var culture = Service<ICityCultureService>(sim);
        var victory = Service<IProjectVictoryService>(sim);
        // The probe makes the legendary tier require bronze_working, so raw culture alone cannot reach it: the
        // victory rule counts eligible tiers, not stored culture. The rule's own counting is proven by
        // ProjectsVictoryRulesTests.Cultural_victory_needs_three_legendary_cities_in_one_team.
        Assert.NotEqual("foundation:culturelevel_legendary", culture.Level(sim, cities[0].Id).Id);
        Assert.False(victory.Evaluate(sim, true).IsGameOver);
        sim.TeamOf(1).Techs.Add("research:bronze_working");
        Assert.All(cities, c => Assert.Equal("foundation:culturelevel_legendary", culture.Level(sim, c.Id).Id));
        var status = victory.Evaluate(sim, true);
        Assert.Equal("projects_victory:cultural", status.VictoryId);
        Assert.Equal(1, status.WinningTeamId);
    }
    [Theory]
    [InlineData("normal", 50)] // 25 * GoldenAgePercent 200/100.
    [InlineData("quick", 37)]  // 25 * 150/100, truncated.
    public void Captured_city_assimilates_foreign_culture_on_speed_scaled_boundaries_and_replays(string speed, int boundary)
    {
        var sim = Scenario(speed); var city = AddCity(sim, 0, 39); city.Culture = 100;
        AddCity(sim, 0, 80); AddCity(sim, 1, 14);
        var plot = sim.State.PlotAt(city.Position); plot.Culture[0] = 100; plot.Culture[1] = 20;
        sim.State.SetWar(0, 1, true);
        Service<ICityCaptureService>(sim).Capture(sim, city.Id, 1);
        city = sim.State.CityById(city.Id);
        var state = city.Extensions.Get<CityRuleState>(sim.Content)!;
        Assert.Equal("culture_probe:origin", state.OriginalCivilization);
        Assert.True(Service<ICityProductionService>(sim).Validate(sim, city.Id, "cities:unit", "culture_probe:unique").Success);
        Assert.False(Service<ICityProductionService>(sim).Validate(sim, city.Id, "cities:unit", "culture_probe:base").Success);
        Assert.Equal("culture_probe:unique", Service<ICityConscriptService>(sim).Calculate(sim, city.Id)!.UnitId);
        state.Disorder = true; // No culture spread or accumulation can mask the decay boundary.
        Assert.Equal(16, Service<ICityCultureService>(sim).OwnerCulturePercent(sim, city.Id));
        sim.State.Turn = boundary - 1;
        sim.AdvanceTurn();
        Assert.Equal(100, plot.Culture[0]); Assert.Equal(10000, state.CultureHundredths[0]);
        var loaded = SaveGame.Load(sim.Content, SaveGame.Save(sim));
        Assert.Equal(sim.ComputeHash(), loaded.ComputeHash());
        sim.AdvanceTurn(); loaded.AdvanceTurn();
        Assert.Equal(50, plot.Culture[0]); Assert.Equal(20, plot.Culture[1]);
        Assert.Equal(5000, state.CultureHundredths[0]); Assert.Equal(0, state.CultureHundredths[1]);
        Assert.Equal(28, Service<ICityCultureService>(sim).OwnerCulturePercent(sim, city.Id));
        Assert.Equal(1, plot.Owner); // Capture pins city-plot ownership even while foreign culture leads.
        Assert.Equal(sim.ComputeHash(), loaded.ComputeHash());
    }
    [Fact]
    public void Plot_decay_exempts_barbarians_and_small_balances_and_releases_unclaimed_land()
    {
        var sim = Scenario();
        var barbarian = new Player { Id = 2, TeamId = 2 };
        barbarian.Extensions.Set(sim.Content, new MilitaryPlayerState { Barbarian = true });
        sim.State.Players.Add(barbarian); sim.State.Teams.Add(new Team { Id = 2, Members = [2] }); sim.State.RebuildIndexes();
        var plot = sim.State.Plots[0]; plot.Owner = 0;
        plot.Culture[0] = 45; plot.Culture[1] = 21; plot.Culture[2] = 99;
        sim.State.Turn = 50; sim.AdvanceTurn();
        Assert.Equal(22, plot.Culture[0]); Assert.Equal(21, plot.Culture[1]); Assert.Equal(99, plot.Culture[2]);
        Assert.Null(plot.Owner);
    }
    [Theory]
    [InlineData(false, false, 3, 3)] // 400 * 75/100 * 10/1000; (100 - 25) * (100/25) / 100.
    [InlineData(false, true, 4, 3)]  // At war: 75 * 150/100 = 112; 400 * 112/100 * 10/1000.
    [InlineData(true, false, 0, 3)]
    public void Nationality_anger_distinguishes_foreign_teams_war_and_civic_owner_nationality(bool sameTeam, bool war, long cultureAnger, long civicAnger)
    {
        var sim = Scenario(sameTeam: sameTeam); var city = AddCity(sim, 0, 39, 10); AddCity(sim, 1, 80);
        var plot = sim.State.PlotAt(city.Position); plot.Culture[0] = 25; plot.Culture[1] = 75;
        sim.State.PlayerById(0).Civics["culture_probe:option"] = "culture_probe:foreigners";
        if (war) sim.State.SetWar(0, 1, true);
        var before = sim.ComputeHash();
        var lines = Service<ICityWellbeingService>(sim).Calculate(sim, city.Id).Ledger.Lines;
        Assert.Equal(cultureAnger, lines.Where(l => l.Source == "cities:culture_anger").Sum(l => l.Amount));
        Assert.Equal(civicAnger, lines.Where(l => l.Source == "government:foreigner_anger").Sum(l => l.Amount));
        Assert.Equal(25, Service<ICityCultureService>(sim).OwnerCulturePercent(sim, city.Id));
        Assert.Equal(sameTeam ? 100 : 25, Service<ICityCultureService>(sim).TeamCulturePercent(sim, city.Id));
        Assert.Equal(before, sim.ComputeHash());
        plot.Culture.Clear();
        Assert.Equal(100, Service<ICityCultureService>(sim).OwnerCulturePercent(sim, city.Id));
        Assert.Equal(100, Service<ICityCultureService>(sim).TeamCulturePercent(sim, city.Id));
        Assert.DoesNotContain(Service<ICityWellbeingService>(sim).Calculate(sim, city.Id).Ledger.Lines,
            l => l.Source is "cities:culture_anger" or "government:foreigner_anger");
    }
    private sealed class Probe : IMod, IModifierSource
    {
        public void Register(IModContext context)
        {
            context.Content<S.TraitDef>().Register(new() { Id = "culture_probe:trait", ExtraCultureLevel = true });
            context.Content<S.LeaderDef>().Register(new() { Id = "culture_probe:leader", Traits = ImmutableDictionary<string, bool>.Empty.Add("culture_probe:trait", true) });
            context.Content<S.CivicOptionDef>().Register(new() { Id = "culture_probe:option" });
            foreach (var id in new[] { "boost", "citizens", "foreigners" })
                context.Content<S.CivicDef>().Register(new() { Id = "culture_probe:" + id, Option = "culture_probe:option", ForeignerUnhappyPercent = id == "foreigners" ? 25 : 0 });
            context.Content<S.UnitDef>().Register(new() { Id = "culture_probe:base", DefaultUnit = "culture_probe:base", Cost = 60, Conscription = 999 });
            context.Content<S.UnitDef>().Register(new() { Id = "culture_probe:unique", DefaultUnit = "culture_probe:base", Cost = 60, Conscription = 1000 });
            context.Content<S.CivilizationDef>().Register(new() { Id = "culture_probe:origin", UnitOverrides = ImmutableDictionary<string, string?>.Empty.Add("culture_probe:base", "culture_probe:unique") });
            foreach (var id in new[] { "other", "boosted" }) context.Content<S.CivilizationDef>().Register(new() { Id = "culture_probe:" + id });
            var fledgling = context.Content<S.CultureLevelDef>().Get("foundation:culturelevel_fledgling");
            context.Content<S.CultureLevelDef>().Register(new() { Id = "culture_probe:chartered", Name = "Chartered", CityRadius = 3, CityDefenseModifier = 77,
                SpeedThresholds = fledgling.SpeedThresholds.SetItem("foundation:gamespeed_normal", 50),
                Requirements = new Requirement.All([new Requirement.Leaf("civic", "culture_probe:citizens"), new Requirement.Leaf("condition", "foundation:population", 5)]) });
            context.Content<S.BuildingDef>().Register(new() { Id = "culture_probe:hall", DefaultBuilding = "culture_probe:hall", Cost = 1,
                Requirements = new Requirement.Leaf("culture-level", "culture_probe:chartered") });
            var legendary = context.Content<S.CultureLevelDef>().Get("foundation:culturelevel_legendary");
            context.Content<S.CultureLevelDef>().Override(new() { Id = legendary.Id, Name = legendary.Name, SpeedThresholds = legendary.SpeedThresholds,
                CityRadius = legendary.CityRadius, CityDefenseModifier = legendary.CityDefenseModifier,
                Requirements = new Requirement.Leaf("tech", "research:bronze_working") });
            context.RegisterService<IModifierSource>("founding_levels", this);
        }
        public void Contribute(ModifierContext context, ModifierSink sink)
        {
            if (context.Scope != ModifierScope.City) return;
            var player = context.Sim.State.PlayerById(context.Player);
            // Civic and civilization schemas have no extra-level fields; mods contribute through the public modifier contract.
            if (player.Civics.Values.Contains("culture_probe:boost")) sink.Add("culture_probe:boost", "cities:extra_culture_level", 1);
            if (player.Civilization == "culture_probe:boosted") sink.Add("culture_probe:boosted", "cities:extra_culture_level", 1);
        }
    }
}
