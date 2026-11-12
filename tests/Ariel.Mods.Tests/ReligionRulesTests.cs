using System.Collections.Concurrent;
using Ariel.Content;
using Ariel.Game;
using Ariel.Modding;
using Xunit;
using S = Ariel.Content.Schema;

namespace Ariel.Mods.Tests;

public sealed class ReligionRulesTests(LoadedMods mods) : IClassFixture<LoadedMods>
{
    private const string Buddhism = "religion:buddhism";

    /// <summary>One frozen content per optional-mod set; xunit builds a new test class instance for every test, so
    /// the inquisition theory's cases would otherwise rebuild the ruleset once each.</summary>
    private static readonly ConcurrentDictionary<bool, Lazy<ContentDb>> InquisitionContents = new();

    private static ContentDb InquisitionContent(bool revolution) => InquisitionContents.GetOrAdd(revolution, key =>
        new(() =>
        {
            var loaded = new LoadedMods();
            var optional = (key ? new[] { "Diplomacy", "Revolution" } : ["Diplomacy"]).Select(name =>
                ModLoader.FromDirectory(Path.Combine(AppContext.BaseDirectory, "optional-mods", name)));
            return ModLoader.Build(loaded.Sources.Concat(optional));
        })).Value;
    private static readonly Lazy<string> ScenarioSave = new(() =>
    {
        var sim = GameFactory.Scenario(new LoadedMods().Content);
        GameFactory.NeutralPolitics(sim);
        return SaveGame.Save(sim);
    });
    private Simulation Scenario() => SaveGame.Load(mods.Content, ScenarioSave.Value);
    private static T Service<T>(Simulation sim) where T : class => sim.Content.Services.Get<T>().Single();
    private static City City(Simulation sim, int tile, int owner = 0, int population = 1)
    {
        var city = new City { Id = sim.State.NextCityId++, Owner = owner, Position = sim.State.Grid.HexOf(new TileId(tile)), Population = population };
        sim.State.AddCity(city);
        return city;
    }
    /// <summary>Give the city's team every technology, then complete the religion's founding wonder there.</summary>
    private static void Found(Simulation sim, City city, string religion = Buddhism)
    {
        sim.TeamOf(city.Owner).Techs.UnionWith(sim.Content.Get<S.TechDef>().All.Select(t => t.Id));
        GameFactory.FoundReligion(sim, city, religion);
    }
    private static void RunCityPhase(Simulation sim, string phase, int player) =>
        ((IPlayerTurnSystem)sim.Content.TurnSystems.Get("cities:" + phase)).Run(sim, player);
    private static Unit Missionary(Simulation sim, City city, int owner = 0, string type = "military:buddhist_missionary")
    {
        var unit = new Unit { Id = sim.State.NextUnitId++, Owner = owner, Type = type, Position = city.Position, MovesLeft = 60 };
        sim.State.AddUnit(unit);
        return unit;
    }
    private static void Spread(Simulation sim, City city, string religion = Buddhism, string type = "military:buddhist_missionary")
    {
        var unit = Missionary(sim, city, city.Owner, type);
        Assert.True(sim.TryApply(new PerformMissionCommand { Player = city.Owner, Unit = unit.Id, Mission = ReligionMissions.Spread, Subject = religion }, out var error), error);
    }
    private static void Presence(Simulation sim, City city, params string[] religions) =>
        city.Extensions.Set(sim.Content, new ReligionCityState { Religions = new(religions, StringComparer.Ordinal) });

    private static void StateFaith(Simulation sim, int player, string? faith = Buddhism)
    {
        sim.State.PlayerById(player).Civics["government:civicoption_religion"] = "government:intolerant";
        sim.State.PlayerById(player).Extensions.Set(sim.Content, new ReligionPlayerState { LastStateReligion = faith });
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Foreign_spread_requires_contact_and_peace_and_scales_with_existing_faiths(bool multiple)
    {
        var foundation = mods.Sources.Single(s => s.Manifest.Id == "foundation");
        var configured = new ModSource { Manifest = foundation.Manifest, Root = foundation.Root, Instance = new SpreadOptions(foundation.Instance, multiple) };
        var diplomacy = ModLoader.FromDirectory(Path.Combine(AppContext.BaseDirectory, "optional-mods", "Diplomacy"));
        var sim = GameFactory.Scenario(ModLoader.Build(mods.Sources.Where(s => s != foundation).Append(configured).Append(diplomacy)));
        GameFactory.NeutralPolitics(sim);
        var holy = City(sim, 0); var foreign = City(sim, 1, 1);
        foreach (var plot in sim.State.Plots) { plot.Revealed.Add(0); plot.Revealed.Add(1); plot.Route = "world:route_route_road"; }
        Found(sim, holy);
        holy.Buildings.AddRange(["cities:buddhist_temple", "cities:buddhist_shrine"]);
        sim.State.Turn = 101;
        var religion = Service<IReligionService>(sim);
        Assert.True(Service<ICityConnectionService>(sim).Connected(sim, holy.Id, foreign.Id));
        Assert.Equal(0, religion.NaturalSpreadOdds(sim, foreign.Id, Buddhism).Threshold);
        Service<IDiplomacyService>(sim).EstablishContact(sim, 0, 1);
        Service<IDiplomacyService>(sim).SignAgreement(sim, 0, 1, DiplomaticAgreement.OpenBorders);
        Assert.Contains(Service<ITradeRouteService>(sim).Calculate(sim, foreign.Id).Routes, r => r.PartnerCity == holy.Id && r.Foreign);
        Assert.Equal(10, religion.NaturalSpreadOdds(sim, foreign.Id, Buddhism).Threshold);
        Presence(sim, foreign, "religion:hinduism");
        Assert.Equal(multiple ? 5 : 0, religion.NaturalSpreadOdds(sim, foreign.Id, Buddhism).Threshold);
        Presence(sim, foreign, "religion:hinduism", "religion:christianity");
        Assert.Equal(multiple ? 3 : 0, religion.NaturalSpreadOdds(sim, foreign.Id, Buddhism).Threshold);
        Presence(sim, foreign);
        sim.State.SetWar(0, 1, true);
        Assert.Equal(0, religion.NaturalSpreadOdds(sim, foreign.Id, Buddhism).Threshold);
        sim.State.SetWar(0, 1, false);
        Service<IDiplomacyService>(sim).SignAgreement(sim, 0, 1, DiplomaticAgreement.OpenBorders);
        if (multiple) Presence(sim, foreign, "religion:hinduism");
        var loaded = SaveGame.Load(sim.Content, SaveGame.Save(sim));
        for (int i = 0; i < 2200 && !religion.Present(sim, foreign.Id).Contains(Buddhism); i++)
        {
            sim.Content.TurnSystems.All.Single(s => s.Id == "religion:city_turns").Run(sim);
            loaded.Content.TurnSystems.All.Single(s => s.Id == "religion:city_turns").Run(loaded);
        }
        Assert.Contains(Buddhism, religion.Present(sim, foreign.Id));
        if (multiple) Assert.Contains("religion:hinduism", religion.Present(sim, foreign.Id));
        Assert.Equal(sim.ComputeHash(), loaded.ComputeHash());
    }

    /// <summary>The inquisitor's chance where it stands, or zero when it cannot hold an inquisition there.</summary>
    private static int InquisitionChance(Simulation sim, Unit inquisitor) =>
        sim.Rules().Missions.Terms(sim, inquisitor.Owner, inquisitor.Id, ReligionMissions.Inquisition, "")?.Chance ?? 0;

    [Fact]
    public void A_trade_route_carries_natural_spread_to_a_city_that_cannot_see_its_way_back()
    {
        var foundation = mods.Sources.Single(s => s.Manifest.Id == "foundation");
        var configured = new ModSource { Manifest = foundation.Manifest, Root = foundation.Root, Instance = new SpreadOptions(foundation.Instance, false) };
        var diplomacy = ModLoader.FromDirectory(Path.Combine(AppContext.BaseDirectory, "optional-mods", "Diplomacy"));
        var sim = GameFactory.Scenario(ModLoader.Build(mods.Sources.Where(s => s != foundation).Append(configured).Append(diplomacy)));
        GameFactory.NeutralPolitics(sim);
        var holy = City(sim, 0); var foreign = City(sim, 1, 1);
        // Only the holy city's team has seen the roads, so the network joins the cities from its side alone.
        foreach (var plot in sim.State.Plots) { plot.Revealed.Add(0); plot.Route = "world:route_route_road"; }
        Found(sim, holy);
        holy.Buildings.AddRange(["cities:buddhist_temple", "cities:buddhist_shrine"]);
        sim.State.Turn = 101;
        var religion = Service<IReligionService>(sim);
        var diplomacyService = Service<IDiplomacyService>(sim);
        diplomacyService.EstablishContact(sim, 0, 1);
        Assert.True(Service<ICityConnectionService>(sim).Connected(sim, holy.Id, foreign.Id));
        Assert.False(Service<ICityConnectionService>(sim).Connected(sim, foreign.Id, holy.Id));
        Assert.Equal(0, religion.NaturalSpreadOdds(sim, foreign.Id, Buddhism).Threshold);

        diplomacyService.SignAgreement(sim, 0, 1, DiplomaticAgreement.OpenBorders);
        Assert.Contains(Service<ITradeRouteService>(sim).Calculate(sim, holy.Id).Routes, r => r.PartnerCity == foreign.Id);
        Assert.True(religion.NaturalSpreadOdds(sim, foreign.Id, Buddhism).Threshold > 0);

        diplomacyService.CancelAgreement(sim, 0, 1, DiplomaticAgreement.OpenBorders);
        Assert.Equal(0, religion.NaturalSpreadOdds(sim, foreign.Id, Buddhism).Threshold);
    }

    private sealed class SpreadOptions(IMod foundation, bool multiple) : IMod
    {
        public void Register(IModContext context)
        {
            foundation.Register(context);
            // The default catalog does not declare this opt-in option. Exercise a configured ruleset.
            context.Content<S.GameOptionDef>().Register(new() { Id = "foundation:gameoption_multiple_religion_spread", Default = multiple });
            var rules = context.Content<S.RuleConstantsDef>().All.Single();
            context.Content<S.RuleConstantsDef>().Override(new() { Id = rules.Id, Values = rules.Values, Raw = rules.Raw.SetItem("INITIAL_TRADE_ROUTES", "1").SetItem("MAX_TRADE_ROUTES", "2") });
        }
    }

    [Fact]
    public void Minority_decay_removes_prerequisite_buildings_but_preserves_state_faith_and_holy_cities()
    {
        var sim = Scenario(); var city = City(sim, 39); var holy = City(sim, 44, 1);
        const string minority = "religion:hinduism";
        Presence(sim, city, Buddhism, minority); Presence(sim, holy, minority);
        StateFaith(sim, 0);
        var founded = new ReligionGameState();
        founded.Founded[minority] = new(holy.Id, 0); sim.State.Extensions.Set(sim.Content, founded);
        var religion = Service<IReligionService>(sim);
        int young = religion.DecayChancePermille(sim, city.Id, minority);
        sim.State.Turn = 101;
        int mature = religion.DecayChancePermille(sim, city.Id, minority);
        sim.State.Turn = 400;
        Assert.True(religion.DecayChancePermille(sim, city.Id, minority) > mature);
        Assert.True(mature > young);
        sim.State.Turn = 101;
        city.Buildings.Add("cities:hindu_temple");
        Assert.True(religion.DecayChancePermille(sim, city.Id, minority) < mature);
        city.Buildings.AddRange(["cities:hindu_monastery", "cities:buddhist_temple", "cities:granary",
            "cities:hindu_brihadeeswarar", "cities:kotoku"]);
        Assert.Equal(0, religion.DecayChancePermille(sim, city.Id, Buddhism));
        Assert.Equal(0, religion.DecayChancePermille(sim, holy.Id, minority));
        var loaded = SaveGame.Load(sim.Content, SaveGame.Save(sim));
        for (int i = 0; i < 2000 && religion.Present(sim, city.Id).Contains(minority); i++)
        {
            sim.Content.TurnSystems.All.Single(s => s.Id == "religion:city_turns").Run(sim);
            loaded.Content.TurnSystems.All.Single(s => s.Id == "religion:city_turns").Run(loaded);
        }
        Assert.DoesNotContain(minority, religion.Present(sim, city.Id));
        Assert.Contains(Buddhism, religion.Present(sim, city.Id));
        Assert.Contains(minority, religion.Present(sim, holy.Id));
        Assert.DoesNotContain("cities:hindu_temple", city.Buildings);
        Assert.DoesNotContain("cities:hindu_monastery", city.Buildings);
        // A religious wonder goes with its faith, as in AND; the other faith's wonder keeps paying.
        Assert.DoesNotContain("cities:hindu_brihadeeswarar", city.Buildings);
        Assert.Contains("cities:kotoku", city.Buildings);
        Assert.Contains("cities:buddhist_temple", city.Buildings); Assert.Contains("cities:granary", city.Buildings);
        Assert.Contains(sim.Events.Published.OfType<ReligionRemoved>(), e => e.City == city.Id && e.Religion == minority && e.Reason == "decay");
        Assert.Equal(sim.ComputeHash(), loaded.ComputeHash());
    }

    [Fact]
    public void Decay_requires_the_religion_decay_option()
    {
        var sim = Scenario(); var city = City(sim, 39); var holy = City(sim, 44, 1);
        const string minority = "religion:hinduism";
        Presence(sim, city, Buddhism, minority); Presence(sim, holy, minority);
        StateFaith(sim, 0);
        var founded = new ReligionGameState(); founded.Founded[minority] = new(holy.Id, 0);
        sim.State.Extensions.Set(sim.Content, founded);
        sim.Settings.GameOptions["foundation:gameoption_religion_decay"] = false;
        var religion = Service<IReligionService>(sim);
        Assert.Equal(0, religion.DecayChancePermille(sim, city.Id, minority));
        for (int i = 0; i < 500; i++) sim.Content.TurnSystems.All.Single(s => s.Id == "religion:city_turns").Run(sim);
        Assert.Contains(minority, religion.Present(sim, city.Id));
        sim.Settings.GameOptions["foundation:gameoption_religion_decay"] = true;
        Assert.True(religion.DecayChancePermille(sim, city.Id, minority) > 0);
    }

    [Theory]
    [InlineData(false, false, false)]
    [InlineData(false, true, false)]
    [InlineData(true, false, false)]
    [InlineData(true, true, false)]
    [InlineData(true, true, true)]
    public void Inquisitor_training_and_actions_require_state_faith_permission_and_no_prohibition(bool state, bool allow, bool disallow)
    {
        var sim = Scenario(); var city = City(sim, 39); Presence(sim, city, Buddhism, "religion:hinduism");
        sim.TeamOf(0).Techs.UnionWith(sim.Content.Get<S.TechDef>().All.Select(t => t.Id));
        StateFaith(sim, 0, state ? Buddhism : null);
        if (!allow) sim.State.PlayerById(0).Civics["government:civicoption_religion"] = "government:state_church";
        if (disallow) sim.State.PlayerById(0).Civics["government:civicoption_society"] = "government:liberal";
        bool enabled = state && allow && !disallow;
        Assert.Equal(enabled, Service<IReligionService>(sim).IsInquisitionEnabled(sim, 0));
        var result = Service<ICityProductionService>(sim).Validate(sim, city.Id, "cities:unit", "military:inquisitor");
        Assert.Equal(enabled, result.Success);
        var unit = Missionary(sim, city, type: "military:inquisitor");
        var command = new PerformMissionCommand { Player = 0, Unit = unit.Id, Mission = ReligionMissions.Inquisition };
        Assert.Equal(enabled, command.Validate(sim) is null);
        if (!enabled)
        {
            ulong hash = sim.ComputeHash(); Assert.False(sim.TryApply(command, out _)); Assert.Equal(hash, sim.ComputeHash());
        }
    }

    private (Simulation Sim, City City, City Refuge, Unit Inquisitor) InquisitionScenario(bool holy, bool relocate, bool revolution)
    {
        var sim = GameFactory.Scenario(InquisitionContent(revolution));
        foreach (var player in sim.State.Players) player.Civics.Clear(); // Keep personalities for diplomatic memory effects.
        var city = City(sim, 39); var refuge = City(sim, 44, 1);
        const string minority = "religion:hinduism";
        Presence(sim, city, Buddhism, minority); StateFaith(sim, 0); StateFaith(sim, 1, minority);
        if (relocate) Presence(sim, refuge, minority);
        var game = new ReligionGameState(); game.Founded[minority] = new(holy ? city.Id : refuge.Id, 0);
        sim.State.Extensions.Set(sim.Content, game);
        city.Buildings.AddRange(["cities:hindu_temple", "cities:hindu_monastery", "cities:buddhist_temple", "cities:granary"]);
        if (revolution) city.Extensions.Set(sim.Content, new RevolutionCityState { InstabilityIndex = 10000 });
        Service<IDiplomacyService>(sim).EstablishContact(sim, 0, 1); sim.State.PlotAt(city.Position).Revealed.Add(1);
        return (sim, city, refuge, Missionary(sim, city, type: "military:inquisitor"));
    }

    [Theory]
    [InlineData(false, false, false)]
    [InlineData(false, false, true)]
    [InlineData(true, false, true)]
    [InlineData(true, true, true)]
    public void Inquisition_purges_buildings_relocates_holy_city_and_records_compensation_and_memory(bool holy, bool relocate, bool revolution)
    {
        var (sim, city, refuge, inquisitor) = InquisitionScenario(holy, relocate, revolution);
        const string minority = "religion:hinduism";
        long treasury = sim.State.PlayerById(0).Treasury;
        var religion = Service<IReligionService>(sim);
        Assert.Equal(holy ? 40 : 90, InquisitionChance(sim, inquisitor));
        city.Extensions.Get<ReligionCityState>(sim.Content)!.Religions.Add("religion:christianity");
        Assert.Equal(holy ? 35 : 85, InquisitionChance(sim, inquisitor));
        city.Extensions.Get<ReligionCityState>(sim.Content)!.Religions.Remove("religion:christianity");
        while (true)
        {
            var (w, x, y, z) = sim.Rng.State;
            if (DeterministicRng.Restore(w, x, y, z).Next(100) < InquisitionChance(sim, inquisitor)) break;
            sim.Rng.NextUInt64();
        }
        var loaded = SaveGame.Load(sim.Content, SaveGame.Save(sim));
        var command = new PerformMissionCommand { Player = 0, Unit = inquisitor.Id, Mission = ReligionMissions.Inquisition };
        Assert.True(sim.TryApply(command, out var error), error); Assert.True(loaded.TryApply(command, out error), error);
        var attempt = Assert.Single(sim.Events.Published.OfType<InquisitionAttempt>());
        Assert.True(attempt.Success); Assert.Equal([minority], attempt.RemovedReligions); Assert.True(attempt.CompensationGold > 0);
        Assert.Equal([Buddhism], religion.Present(sim, city.Id));
        Assert.DoesNotContain("cities:hindu_temple", city.Buildings); Assert.DoesNotContain("cities:hindu_monastery", city.Buildings);
        Assert.Contains("cities:buddhist_temple", city.Buildings); Assert.Contains("cities:granary", city.Buildings);
        if (revolution) Assert.Equal(10000 - attempt.CompensationGold, city.Extensions.Get<RevolutionCityState>(sim.Content)!.InstabilityIndex);
        else Assert.Equal(treasury + attempt.CompensationGold, sim.State.PlayerById(0).Treasury);
        Assert.True(city.Extensions.Get<EconomyCityState>(sim.Content)!.HurryAngerTurns > 0);
        Assert.Contains(Service<IDiplomacyService>(sim).GetAttitude(sim, 1, 0).Ledger.Lines, l => l.Source.Contains(DiplomaticMemories.Inquisition) && l.Amount < 0);
        int? destination = holy && !relocate ? null : refuge.Id;
        Assert.Equal(destination ?? -1, sim.State.Extensions.Get<ReligionGameState>(sim.Content)!.Founded[minority].HolyCity);
        Assert.Equal(destination, religion.HolyCity(sim, minority));
        Assert.Null(sim.State.FindUnit(inquisitor.Id)); Assert.Equal(sim.ComputeHash(), loaded.ComputeHash());
    }

    [Fact]
    public void Inquisition_pays_the_treasury_when_the_revolution_city_state_is_missing()
    {
        var sim = GameFactory.Scenario(InquisitionContent(true));
        foreach (var player in sim.State.Players) player.Civics.Clear();
        var city = City(sim, 39);
        Presence(sim, city, Buddhism, "religion:hinduism"); StateFaith(sim, 0);
        city.Buildings.Add("cities:hindu_temple");
        var inquisitor = Missionary(sim, city, type: "military:inquisitor");
        var religion = Service<IReligionService>(sim);
        Assert.Null(city.Extensions.Get<RevolutionCityState>(sim.Content));
        while (true)
        {
            var (w, x, y, z) = sim.Rng.State;
            if (DeterministicRng.Restore(w, x, y, z).Next(100) < InquisitionChance(sim, inquisitor)) break;
            sim.Rng.NextUInt64();
        }
        long treasury = sim.State.PlayerById(0).Treasury;
        Assert.True(sim.TryApply(new PerformMissionCommand { Player = 0, Unit = inquisitor.Id, Mission = ReligionMissions.Inquisition }, out var error), error);
        var attempt = Assert.Single(sim.Events.Published.OfType<InquisitionAttempt>());
        Assert.True(attempt.Success); Assert.True(attempt.CompensationGold > 0);
        Assert.Equal(treasury + attempt.CompensationGold, sim.State.PlayerById(0).Treasury);
        Assert.Null(city.Extensions.Get<RevolutionCityState>(sim.Content));
    }

    [Fact]
    public void Failed_inquisition_consumes_the_inquisitor_and_adds_anger_without_purging_or_compensation()
    {
        var sim = Scenario(); var city = City(sim, 39); StateFaith(sim, 0);
        Presence(sim, city, Buddhism, "religion:hinduism"); city.Buildings.Add("cities:hindu_temple");
        var inquisitor = Missionary(sim, city, type: "military:inquisitor");
        var religion = Service<IReligionService>(sim);
        while (true)
        {
            var (w, x, y, z) = sim.Rng.State;
            if (DeterministicRng.Restore(w, x, y, z).Next(100) >= InquisitionChance(sim, inquisitor)) break;
            sim.Rng.NextUInt64();
        }
        long gold = sim.State.PlayerById(0).Treasury;
        var loaded = SaveGame.Load(sim.Content, SaveGame.Save(sim));
        var command = new PerformMissionCommand { Player = 0, Unit = inquisitor.Id, Mission = ReligionMissions.Inquisition };
        Assert.True(sim.TryApply(command, out var error), error); Assert.True(loaded.TryApply(command, out error), error);
        var attempt = Assert.Single(sim.Events.Published.OfType<InquisitionAttempt>());
        Assert.False(attempt.Success); Assert.Empty(attempt.RemovedReligions); Assert.Equal(0, attempt.CompensationGold);
        Assert.Equal(gold, sim.State.PlayerById(0).Treasury); Assert.Contains("religion:hinduism", religion.Present(sim, city.Id));
        Assert.Contains("cities:hindu_temple", city.Buildings); Assert.Null(sim.State.FindUnit(inquisitor.Id));
        Assert.True(city.Extensions.Get<EconomyCityState>(sim.Content)!.HurryAngerTurns > 0);
        Assert.Equal(sim.ComputeHash(), loaded.ComputeHash());
    }

    [Theory]
    [InlineData(Buddhism, 0)]
    [InlineData("religion:christianity", 1)]
    public void Completing_the_founding_wonder_founds_the_religion_in_that_city_once(string religionId, int missionaries)
    {
        var sim = Scenario();
        var capital = City(sim, 0); capital.Buildings.Add("cities:palace");
        var builder = City(sim, 5, population: 4);
        var rival = City(sim, 60, owner: 1, population: 4);
        string tech = sim.Content.Get<S.ReligionDef>(religionId).FoundingTech!;
        string wonder = GameFactory.FoundingWonder(sim, religionId);
        var production = Service<ICityProductionService>(sim);
        Assert.False(production.Validate(sim, builder.Id, "cities:building", wonder).Success);
        sim.TeamOf(0).Techs.UnionWith(sim.Content.Get<S.TechDef>().All.Select(t => t.Id));
        sim.TeamOf(1).Techs.UnionWith(sim.Content.Get<S.TechDef>().All.Select(t => t.Id));
        Assert.Contains(tech, sim.TeamOf(0).Techs);
        Assert.True(sim.TryApply(new SetCityProductionCommand { Player = 0, CityId = builder.Id, Kind = "cities:building", ContentId = wonder }, out var error), error);
        Assert.True(sim.TryApply(new SetCityProductionCommand { Player = 1, CityId = rival.Id, Kind = "cities:building", ContentId = wonder }, out error), error);
        builder.ProductionQueue[0] = builder.ProductionQueue[0] with { Progress = production.Calculate(sim, builder.Id, "cities:building", wonder).Cost };

        RunCityPhase(sim, "production", 0);
        RunCityPhase(sim, "completion", 0);

        var religion = Service<IReligionService>(sim);
        Assert.Equal(builder.Id, religion.HolyCity(sim, religionId));
        Assert.Contains(religionId, religion.Present(sim, builder.Id));
        Assert.Empty(religion.Present(sim, capital.Id));
        var founded = Assert.Single(sim.Events.Published.OfType<ReligionFounded>());
        Assert.Equal((religionId, builder.Id, 0), (founded.Religion, founded.City, founded.Player));
        string missionary = sim.Content.Get<S.ReligionDef>(religionId).Missionary!;
        int moves = sim.Content.Get<S.UnitDef>(missionary).Moves * sim.Content.Get<S.RuleConstantsDef>().All.Single().Values.MoveDenominator;
        Assert.Equal(missionaries, sim.State.Units.Count(u => u.Type == missionary));
        Assert.All(sim.State.Units.Where(u => u.Type == missionary), u =>
        {
            Assert.Equal(0, u.Owner); Assert.Equal(builder.Position, u.Position);
            // Awarded on completion, so it must already have its full moves rather than waiting for a refill.
            Assert.Equal(moves, u.MovesLeft);
        });

        // The rival's copy can no longer be built: its order drops at completion and nothing is founded twice.
        Assert.False(production.Validate(sim, rival.Id, "cities:building", wonder).Success);
        rival.ProductionQueue[0] = rival.ProductionQueue[0] with { Progress = production.Calculate(sim, rival.Id, "cities:building", wonder).Cost };
        RunCityPhase(sim, "production", 1);
        RunCityPhase(sim, "completion", 1);
        Assert.DoesNotContain(rival.ProductionQueue, o => o.ContentId == wonder);
        Assert.DoesNotContain(wonder, rival.Buildings);
        Assert.Single(sim.Events.Published.OfType<ReligionFounded>());
        Assert.Equal(builder.Id, religion.HolyCity(sim, religionId));
    }
    [Fact]
    public void Discovering_the_founding_tech_founds_nothing_and_the_builder_founds_even_if_others_discovered_first()
    {
        var sim = Scenario();
        string tech = sim.Content.Get<S.ReligionDef>(Buddhism).FoundingTech!;
        // Team 0 has no city and discovers the tech first; under the old rules that reserved the religion for it.
        sim.TeamOf(0).Techs.Add(tech);
        sim.Events.Publish(new TechDiscovered(0, tech));
        var builder = City(sim, 60, owner: 1);
        sim.TeamOf(1).Techs.Add(tech);
        sim.Events.Publish(new TechDiscovered(1, tech));
        var religion = Service<IReligionService>(sim);
        Assert.Null(religion.HolyCity(sim, Buddhism));

        GameFactory.FoundReligion(sim, builder, Buddhism);

        Assert.Equal(builder.Id, religion.HolyCity(sim, Buddhism));
        Assert.Equal(1, Assert.Single(sim.Events.Published.OfType<ReligionFounded>()).Player);
        Assert.Equal("Religion is already founded",
            religion.ValidateBuilding(sim, City(sim, 0), sim.Content.Get<S.BuildingDef>(GameFactory.FoundingWonder(sim, Buddhism))));
    }
    [Fact]
    public void Barbarians_cannot_build_a_founding_wonder()
    {
        var sim = Scenario();
        var city = City(sim, 60, owner: 1);
        GameFactory.MakeBarbarian(sim, 1);
        var wonder = sim.Content.Get<S.BuildingDef>(GameFactory.FoundingWonder(sim, Buddhism));
        Assert.Equal("Barbarians cannot found a religion", Service<IReligionService>(sim).ValidateBuilding(sim, city, wonder));
    }
    [Fact]
    public void Every_religion_has_one_founding_wonder_gated_by_its_founding_tech()
    {
        var content = mods.Content;
        foreach (var religion in content.Get<S.ReligionDef>().All)
        {
            var wonder = Assert.Single(content.Get<S.BuildingDef>().All, b => b.FoundsReligion == religion.Id);
            Assert.Equal(religion.Name, wonder.Name);
            Assert.True(CityBuildingRules.IsWonder(wonder));
            Assert.Equal(1, wonder.Limits.World);
            Assert.Contains(new Requirement.Leaf("tech", religion.FoundingTech!), ContentReferences.Leaves(wonder.Requirements!));
        }
    }
    [Fact]
    public void Natural_spread_uses_connected_influence_and_continues_identically_after_a_save()
    {
        var sim = Scenario(); var source = City(sim, 0); Found(sim, source);
        source.Buildings.AddRange(["cities:buddhist_temple", "cities:buddhist_shrine"]);
        var connected = City(sim, 1); var isolated = City(sim, 8);
        sim.State.Turn = 101; // Outside the new-religion window, before old-religion attenuation.
        var religion = Service<IReligionService>(sim);
        Assert.Equal(new ReligionSpreadOdds(10, 1100), religion.NaturalSpreadOdds(sim, connected.Id, Buddhism)); // 245/6 +1, then /4. Hex diameter is nine.
        Assert.Equal(0, religion.NaturalSpreadOdds(sim, isolated.Id, Buddhism).Threshold);
        var before = sim.ComputeHash();
        _ = religion.NaturalSpreadOdds(sim, connected.Id, Buddhism);
        Assert.Equal(before, sim.ComputeHash()); // Preview neither draws RNG nor writes extension state.
        var loaded = SaveGame.Load(mods.Content, SaveGame.Save(sim));
        var phase = sim.Content.TurnSystems.All.Single(s => s.Id == "religion:city_turns");
        // Keep age fixed while sampling enough low-probability attempts to avoid dependence on the map's RNG offset.
        for (int i = 0; i < 1100 && religion.Present(sim, connected.Id).Count == 0; i++)
        {
            phase.Run(sim); phase.Run(loaded);
            Assert.Equal(sim.ComputeHash(), loaded.ComputeHash());
        }
        Assert.Contains(Buddhism, religion.Present(sim, connected.Id));
        Assert.Empty(religion.Present(sim, isolated.Id));
    }
    [Fact]
    public void Missionary_commands_consume_the_unit_and_enforce_foreign_civic_and_ownership_gates()
    {
        var sim = Scenario(); var domestic = City(sim, 0); var foreign = City(sim, 40, 1);
        sim.State.PlayerById(1).Civics["government:civicoption_religion"] = "government:intolerant";
        var unit = Missionary(sim, foreign);
        var command = new PerformMissionCommand { Player = 0, Unit = unit.Id, Mission = ReligionMissions.Spread, Subject = Buddhism };
        var hash = sim.ComputeHash();
        Assert.False(sim.TryApply(command, out var error)); Assert.Contains("prohibit", error);
        Assert.Equal(hash, sim.ComputeHash());
        sim.State.MoveUnit(unit.Id, domestic.Position);
        Assert.False(sim.TryApply(new PerformMissionCommand { Player = 1, Unit = unit.Id, Mission = ReligionMissions.Spread, Subject = Buddhism }, out _));
        Assert.Equal(100, sim.Rules().Missions.Terms(sim, unit.Owner, unit.Id, ReligionMissions.Spread, Buddhism)!.Chance);
        Assert.True(sim.TryApply(command, out error), error);
        Assert.Null(sim.State.FindUnit(unit.Id));
        Assert.Contains(Buddhism, Service<IReligionService>(sim).Present(sim, domestic.Id));
        Assert.Single(sim.Events.Published.OfType<MissionResolved>(), e => e.Mission == ReligionMissions.Spread && e.Success);
    }
    [Fact]
    public void Conversion_updates_happiness_production_and_commerce_ledgers_and_saves_anarchy()
    {
        var sim = Scenario(); var holy = City(sim, 0); Found(sim, holy);
        var foreign = City(sim, 40, 1); Spread(sim, foreign);
        holy.Buildings.AddRange(["cities:buddhist_temple", "cities:buddhist_shrine", "cities:sistine_chapel"]);
        sim.State.PlayerById(0).Civics["government:civicoption_religion"] = "government:state_church";
        var religion = Service<IReligionService>(sim);
        Assert.True(sim.TryApply(new SetStateReligionCommand { Player = 0, Religion = Buddhism }, out var error), error);
        Assert.Equal(Buddhism, religion.StateReligion(sim, 0));
        Assert.True(GovernmentStatus.IsAnarchy(sim, 0));
        Assert.Equal(default, Service<ICityYieldService>(sim).Calculate(sim, holy.Id).Yields);
        var loaded = SaveGame.Load(mods.Content, SaveGame.Save(sim));
        Assert.Equal(sim.ComputeHash(), loaded.ComputeHash());
        sim.AdvanceTurn(); loaded.AdvanceTurn();
        Assert.Equal(sim.ComputeHash(), loaded.ComputeHash());
        Assert.False(GovernmentStatus.IsAnarchy(sim, 0));
        var effects = ModifierSink.Collect(new(sim, ModifierScope.City, 0, holy)).Ledger;
        Assert.Equal(2, effects.Lines.Single(l => l.Source == "cities:buddhist_temple" && l.Key == "cities:happiness").Amount);
        Assert.Equal(1, effects.Lines.Single(l => l.Source == Buddhism && l.Key == "cities:happiness").Amount);
        Assert.Equal(2, effects.Lines.Single(l => l.Source == "cities:buddhist_shrine:shrine" && l.Key == "commerce.flat.gold").Amount);
        Assert.Equal(4, effects.Lines.Single(l => l.Source == "cities:buddhist_shrine:shrine" && l.Key == "commerce.flat.culture").Amount);
        Assert.Equal(5, effects.Lines.Single(l => l.Source == "cities:buddhist_temple:state_religion" && l.Key == "commerce.flat.culture").Amount);
        Assert.Equal(15, effects.Total("cities:religion_building_production_percent"));
        Assert.Equal(15, Service<ICityProductionService>(sim).Calculate(sim, holy.Id, "cities:building", "cities:granary").ModifierPercent - 100);
        Assert.False(sim.TryApply(new SetStateReligionCommand { Player = 0 }, out error)); Assert.Contains("cooldown", error);
        // The rule owns the guard, so a direct service call cannot skip it either.
        Assert.Throws<InvalidOperationException>(() => religion.Convert(sim, 0, null));
    }
    [Fact]
    public void Queries_answer_a_stale_city_id_instead_of_throwing()
    {
        var sim = Scenario(); var religion = Service<IReligionService>(sim);
        Assert.Empty(religion.Present(sim, 9999));
        Assert.Equal(0, religion.DecayChancePermille(sim, 9999, Buddhism));
    }
    [Fact]
    public void Building_eligibility_distinguishes_presence_state_religion_and_holy_city()
    {
        var sim = Scenario(); var holy = City(sim, 0); Found(sim, holy);
        var city = City(sim, 5);
        var production = Service<ICityProductionService>(sim);
        Assert.False(production.Validate(sim, city.Id, "cities:building", "cities:buddhist_temple").Success);
        Spread(sim, city);
        Assert.True(production.Validate(sim, city.Id, "cities:building", "cities:buddhist_temple").Success);
        var prophet = Missionary(sim, city, type: "military:prophet");
        var shrine = new PerformMissionCommand { Player = 0, Unit = prophet.Id, Mission = ReligionMissions.Shrine, Subject = "cities:buddhist_shrine" };
        Assert.False(sim.TryApply(shrine, out var shrineError)); Assert.Contains("holy city", shrineError);
        sim.State.MoveUnit(prophet.Id, holy.Position);
        Assert.True(sim.TryApply(shrine, out shrineError), shrineError);
        Assert.Contains("cities:buddhist_shrine", holy.Buildings);
        Assert.Null(sim.State.FindUnit(prophet.Id));
        sim.State.PlayerById(0).Civics["government:civicoption_religion"] = "government:state_church";
        Spread(sim, city, "religion:hinduism", "military:hindu_missionary");
        Assert.True(sim.TryApply(new SetStateReligionCommand { Player = 0, Religion = "religion:hinduism" }, out var error), error);
        Assert.False(production.Validate(sim, city.Id, "cities:building", "cities:buddhist_monastery").Success);
        Assert.True(production.Validate(sim, city.Id, "cities:building", "cities:buddhist_temple").Success); // Temple exemption.
        sim.State.PlayerById(0).Civics["government:civicoption_religion"] = "government:free_church";
        Assert.True(production.Validate(sim, city.Id, "cities:building", "cities:buddhist_monastery").Success);
        sim.State.PlayerById(0).Civics.Clear();
        Assert.Null(Service<IReligionService>(sim).StateReligion(sim, 0));
        Assert.Equal("religion:hinduism", sim.State.PlayerById(0).Extensions.Get<ReligionPlayerState>(sim.Content)!.LastStateReligion);
    }
    [Fact]
    public void Old_religion_spread_odds_scale_down_smoothly_instead_of_truncating_to_zero()
    {
        var sim = Scenario(); var source = City(sim, 0); Found(sim, source);
        source.Buildings.AddRange(["cities:buddhist_temple", "cities:buddhist_shrine"]);
        var connected = City(sim, 1);
        var religion = Service<IReligionService>(sim);
        int founded = sim.State.Turn;
        // Base threshold is 41 for this pair. Range 1100 implies 110% growth, so the old-age line is 250 * 110 / 100 = 275.
        // Threshold is then 41 * 275 / age, and the reported odds divide that by four.
        sim.State.Turn = founded + 101;
        Assert.Equal(10, religion.NaturalSpreadOdds(sim, connected.Id, Buddhism).Threshold);
        sim.State.Turn = founded + 300;
        Assert.Equal(9, religion.NaturalSpreadOdds(sim, connected.Id, Buddhism).Threshold);
        sim.State.Turn = founded + 400;
        Assert.Equal(7, religion.NaturalSpreadOdds(sim, connected.Id, Buddhism).Threshold);
        sim.State.Turn = founded + 550;
        Assert.Equal(5, religion.NaturalSpreadOdds(sim, connected.Id, Buddhism).Threshold);
    }
    [Fact]
    public void State_religion_buildings_need_an_active_state_religion_even_when_they_name_no_religion()
    {
        var sim = Scenario(); Found(sim, City(sim, 0));
        var city = City(sim, 5); Spread(sim, city);
        var religion = Service<IReligionService>(sim);
        var palace = sim.Content.Get<S.BuildingDef>("cities:apostolic_palace");
        Assert.NotNull(religion.ValidateBuilding(sim, city, palace));
        Assert.Null(religion.ValidateBuilding(sim, city, sim.Content.Get<S.BuildingDef>("cities:buddhist_temple"))); // Temple exemption.
        sim.State.PlayerById(0).Civics["government:civicoption_religion"] = "government:state_church";
        Assert.True(sim.TryApply(new SetStateReligionCommand { Player = 0, Religion = Buddhism }, out var error), error);
        Assert.Null(religion.ValidateBuilding(sim, city, palace));
        Assert.Equal("Requires matching state religion: religion:islam",
            religion.ValidateBuilding(sim, city, sim.Content.Get<S.BuildingDef>("cities:alhambra")));
    }
}
