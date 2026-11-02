using Ariel.Content;
using Ariel.Game;
using Ariel.Modding;
using Xunit;
using S = Ariel.Content.Schema;

namespace Ariel.Mods.Tests;

public sealed class RevolutionRulesTests(P7ModFixture mods) : IClassFixture<P7ModFixture>
{
    private static IRevolutionService Service(Simulation sim) => sim.Content.Services.Get<IRevolutionService>().Single();
    private static void Update(Simulation sim) => sim.Content.TurnSystems.Get("revolution:instability").Run(sim);
    private static void Rebellions(Simulation sim) => sim.Content.TurnSystems.Get("revolution:rebellions").Run(sim);
    private static RevolutionCityState State(Simulation sim, City city, int index)
    {
        var state = new RevolutionCityState { InstabilityIndex = index };
        city.Extensions.Set(sim.Content, state);
        return state;
    }
    private static long Contribution(CityStabilityDescription result, string source) =>
        result.Ledger.Lines.Where(l => l.Source == source).Sum(l => l.Amount);
    // Normal pace is 85%; each line rounds once, to the nearest point away from zero.
    private static long Round(long amount) => (long)Math.Round(amount * 100.0 / 85, MidpointRounding.AwayFromZero);

    [Theory]
    [InlineData(0, CityStabilityStatus.Stable)]
    [InlineData(399, CityStabilityStatus.Stable)]
    [InlineData(400, CityStabilityStatus.Warning)]
    [InlineData(699, CityStabilityStatus.Warning)]
    [InlineData(700, CityStabilityStatus.Mutiny)]
    [InlineData(999, CityStabilityStatus.Mutiny)]
    [InlineData(1000, CityStabilityStatus.Rebellion)]
    [InlineData(1500, CityStabilityStatus.Rebellion)]
    public void Status_thresholds_are_exact_and_descriptions_do_not_mutate_state(int index, CityStabilityStatus expected)
    {
        var sim = P7ModFixture.Scenario(mods.Revolution);
        var city = P7ModFixture.City(sim);
        State(sim, city, index);
        var before = sim.ComputeHash();
        var description = Service(sim).DescribeCityStability(sim, city.Id);
        Assert.Equal(expected, description.Status);
        Assert.Equal(index, description.InstabilityIndex);
        Assert.Equal(before, sim.ComputeHash());
    }

    [Fact]
    public void Ledger_combines_wellbeing_hex_distance_garrison_civics_anarchy_and_golden_age()
    {
        var sim = P7ModFixture.Scenario(mods.Revolution);
        var capital = P7ModFixture.City(sim, 40); capital.Buildings.Add("cities:palace");
        var city = P7ModFixture.City(sim, 46, population: 30);
        var service = Service(sim);
        var bare = service.DescribeCityStability(sim, city.Id);
        // Normal pace is 85%; each term rounds once, so the lines add up to the total.
        Assert.Equal(Round(6 * 3), Contribution(bare, "revolution:distance"));
        Assert.Equal(Round(15), Contribution(bare, "revolution:garrison"));
        Assert.True(Contribution(bare, "revolution:wellbeing") > 0);
        P7ModFixture.Unit(sim, city);
        sim.State.PlayerById(0).Civics["government:civicoption_government"] = "government:despotism";
        sim.State.PlayerById(0).Extensions.Set(sim.Content, new GovernmentPlayerState { AnarchyTurns = 2 });
        sim.State.PlayerById(0).Extensions.Set(sim.Content, new GreatPeoplePlayerState { GoldenAgeTurns = 2 });
        var protectedCity = service.DescribeCityStability(sim, city.Id);
        Assert.Equal(Round(-12), Contribution(protectedCity, "revolution:garrison"));
        Assert.Equal(Round(8), Contribution(protectedCity, "government:despotism"));
        Assert.Equal(Round(40), Contribution(protectedCity, "revolution:anarchy"));
        Assert.Equal(Round(-30), Contribution(protectedCity, "revolution:golden_age"));
        Assert.Equal(protectedCity.Ledger.Lines.Sum(l => l.Amount), protectedCity.ChangePerTurn);
        var state = State(sim, city, 200);
        Update(sim);
        Assert.Equal(200 + protectedCity.ChangePerTurn, state.InstabilityIndex);
        var hash = sim.ComputeHash(); Update(sim); Assert.Equal(hash, sim.ComputeHash());
        city.Population = 1;
        sim.State.PlayerById(0).Extensions.Set(sim.Content, new GovernmentPlayerState());
        var calm = service.DescribeCityStability(sim, city.Id);
        Assert.True(Contribution(calm, "revolution:wellbeing") < 0);
        state.InstabilityIndex = 1; sim.State.Turn++; Update(sim);
        Assert.Equal(0, state.InstabilityIndex);
    }

    [Fact]
    public void Religion_presence_and_intolerance_civics_have_separate_explainable_effects()
    {
        var sim = P7ModFixture.Scenario(mods.Revolution);
        var city = P7ModFixture.City(sim);
        sim.State.PlayerById(0).Civics["government:civicoption_religion"] = "government:state_church";
        sim.State.PlayerById(0).Extensions.Set(sim.Content, new ReligionPlayerState { LastStateReligion = "religion:buddhism" });
        city.Extensions.Set(sim.Content, new ReligionCityState { Religions = new(StringComparer.Ordinal) { "religion:buddhism", "religion:christianity" } });
        var mild = Service(sim).DescribeCityStability(sim, city.Id);
        Assert.Equal(Round(10), Contribution(mild, "revolution:non_state_religion"));
        Assert.Equal(Round(-10), Contribution(mild, "revolution:state_religion"));
        sim.State.PlayerById(0).Civics["government:civicoption_religion"] = "government:intolerant";
        var intolerant = Service(sim).DescribeCityStability(sim, city.Id);
        Assert.Equal(Round(20), Contribution(intolerant, "revolution:non_state_religion"));
        city.Extensions.Get<ReligionCityState>(sim.Content)!.Religions.Remove("religion:buddhism");
        Assert.Equal(0, Contribution(Service(sim).DescribeCityStability(sim, city.Id), "revolution:state_religion"));
        sim.State.PlayerById(0).Civics.Clear();
        Assert.Equal(0, Contribution(Service(sim).DescribeCityStability(sim, city.Id), "revolution:non_state_religion"));
    }

    [Fact]
    public void Rebellion_uses_legal_adjacent_tiles_releases_pressure_sets_cooldown_and_adds_temporary_unrest()
    {
        var sim = P7ModFixture.Scenario(mods.Revolution);
        var city = P7ModFixture.City(sim);
        var state = State(sim, city, 1000);
        var neighbors = sim.State.Grid.Neighbors(sim.State.Grid.TileAt(city.Position)).OrderBy(t => t.Value).ToArray();
        sim.State.Plots[neighbors[0].Value].Terrain = "world:coast";
        sim.State.Plots[neighbors[1].Value].Kind = PlotKind.Peak;
        var blocked = P7ModFixture.City(sim, neighbors[2].Value);
        P7ModFixture.Unit(sim, blocked);
        Rebellions(sim);
        var rebellion = Assert.Single(sim.Events.Published.OfType<CityRebellionTriggered>());
        Assert.Equal(city.Id, rebellion.City); Assert.Equal(0, rebellion.Player);
        Assert.Equal(600, rebellion.InstabilityReleased); Assert.Equal(400, state.InstabilityIndex);
        Assert.Equal(2, rebellion.Units.Count);
        Assert.True(sim.State.PlayerById(rebellion.RebelPlayer).Extensions.Get<MilitaryPlayerState>(sim.Content)!.Barbarian);
        Assert.Contains(sim.TeamOf(0).Id, sim.TeamOf(rebellion.RebelPlayer).Wars);
        var rebels = rebellion.Units.Select(sim.State.UnitById).ToArray();
        var legal = neighbors.Skip(3).Select(t => sim.State.Grid.HexOf(t)).ToArray();
        Assert.Equal(2, rebels.Select(u => u.Position).Distinct().Count());
        Assert.All(rebels, u => Assert.Contains(u.Position, legal));
        Assert.All(rebels, u => { Assert.Equal(rebellion.RebelPlayer, u.Owner); Assert.True(u.MovesLeft > 0); });
        var unrest = ModifierSink.Collect(new(sim, ModifierScope.City, 0, city)).Ledger;
        Assert.Equal(3, unrest.Lines.Single(l => l.Source == "revolution:unrest").Amount);
        Assert.Equal(9, Service(sim).DescribeCityStability(sim, city.Id).CooldownTurns);
        state.InstabilityIndex = 1000; Rebellions(sim);
        Assert.Single(sim.Events.Published.OfType<CityRebellionTriggered>());
        sim.State.Turn = state.UnrestUntilTurn;
        Assert.DoesNotContain(ModifierSink.Collect(new(sim, ModifierScope.City, 0, city)).Ledger.Lines, l => l.Source == "revolution:unrest");
        sim.State.Turn = state.RebellionCooldownUntilTurn; Rebellions(sim);
        Assert.Equal(2, sim.Events.Published.OfType<CityRebellionTriggered>().Count());
        Assert.Single(sim.State.Players, p => p.Extensions.TryGet<MilitaryPlayerState>(sim.Content)?.Barbarian == true);
    }

    [Fact]
    public void Enclosed_city_releases_pressure_without_spawning_on_impassable_or_occupied_tiles()
    {
        var sim = P7ModFixture.Scenario(mods.Revolution);
        var city = P7ModFixture.City(sim);
        State(sim, city, 1000);
        foreach (var tile in sim.State.Grid.Neighbors(sim.State.Grid.TileAt(city.Position))) sim.State.Plots[tile.Value].Kind = PlotKind.Peak;
        Rebellions(sim);
        Assert.Empty(sim.State.Units);
        // No legal tile means no rebellion: pressure is not released and no cooldown starts.
        Assert.Equal(1000, city.Extensions.Get<RevolutionCityState>(sim.Content)!.InstabilityIndex);
        Assert.Empty(sim.Events.Published.OfType<CityRebellionTriggered>());
    }

    [Fact]
    public void Cooldown_protects_the_eight_turns_after_a_rebellion()
    {
        var sim = P7ModFixture.Scenario(mods.Revolution);
        var city = P7ModFixture.City(sim);
        var state = State(sim, city, 1000);
        int start = sim.State.Turn;
        Rebellions(sim);
        state.InstabilityIndex = 1000; sim.State.Turn = start + 8; Rebellions(sim);
        Assert.Single(sim.Events.Published.OfType<CityRebellionTriggered>());
        sim.State.Turn = start + 9; Rebellions(sim);
        Assert.Equal(2, sim.Events.Published.OfType<CityRebellionTriggered>().Count());
    }

    [Fact]
    public void Rebellion_drops_overflowing_pressure_back_below_the_threshold()
    {
        var sim = P7ModFixture.Scenario(mods.Revolution);
        var city = P7ModFixture.City(sim);
        var state = State(sim, city, 2500);
        Rebellions(sim);
        var rebellion = Assert.Single(sim.Events.Published.OfType<CityRebellionTriggered>());
        // Pressure above the threshold is discarded, so the city restarts at 1000 - 600.
        Assert.Equal(400, state.InstabilityIndex);
        Assert.Equal(2100, rebellion.InstabilityReleased);
    }

    [Fact]
    public void Rebels_never_spawn_inside_a_third_party_border()
    {
        var sim = P7ModFixture.Scenario(mods.Revolution);
        var city = P7ModFixture.City(sim);
        State(sim, city, 1000);
        var neighbors = sim.State.Grid.Neighbors(sim.State.Grid.TileAt(city.Position)).OrderBy(t => t.Value).ToArray();
        // Blocking the first three neighbors leaves neighbors[3] and [4] as the legal spots, as in the rebellion test.
        sim.State.Plots[neighbors[0].Value].Terrain = "world:coast";
        sim.State.Plots[neighbors[1].Value].Kind = PlotKind.Peak;
        P7ModFixture.Unit(sim, P7ModFixture.City(sim, neighbors[2].Value));
        sim.State.Plots[neighbors[3].Value].Owner = 1;
        Rebellions(sim);
        var rebellion = Assert.Single(sim.Events.Published.OfType<CityRebellionTriggered>());
        var positions = rebellion.Units.Select(id => sim.State.UnitById(id).Position).ToArray();
        Assert.DoesNotContain(sim.State.Grid.HexOf(neighbors[3]), positions);
        Assert.Contains(sim.State.Grid.HexOf(neighbors[4]), positions);
    }

    [Fact]
    public void Suppression_command_roundtrips_and_rejections_are_atomic_for_gold_and_garrison_actions()
    {
        var sim = P7ModFixture.Scenario(mods.Revolution);
        var city = P7ModFixture.City(sim);
        var state = State(sim, city, 1100);
        var gold = new SuppressRebellionCommand { Player = 0, CityId = city.Id };
        var codec = new CommandCodec(sim.Content);
        var decoded = codec.Decode(codec.Encode(gold));
        Assert.IsType<SuppressRebellionCommand>(decoded);
        var before = sim.ComputeHash();
        Assert.False(sim.TryApply(decoded, out _)); Assert.Equal(before, sim.ComputeHash());
        sim.State.PlayerById(0).Treasury = 150;
        Assert.True(sim.TryApply(decoded, out var error), error);
        Assert.Equal(50, sim.State.PlayerById(0).Treasury); Assert.Equal(700, state.InstabilityIndex);
        Rebellions(sim); Assert.Empty(sim.Events.Published.OfType<CityRebellionTriggered>());
        var civilian = P7ModFixture.Unit(sim, city, "military:settler");
        var invalid = new SuppressRebellionCommand { Player = 0, CityId = city.Id, Garrison = civilian.Id };
        before = sim.ComputeHash(); Assert.False(sim.TryApply(invalid, out _)); Assert.Equal(before, sim.ComputeHash());
        var garrison = P7ModFixture.Unit(sim, city);
        var action = new SuppressRebellionCommand { Player = 0, CityId = city.Id, Garrison = garrison.Id };
        Assert.True(sim.TryApply(action, out error), error);
        Assert.Equal(0, garrison.MovesLeft); Assert.Equal(300, state.InstabilityIndex);
        Assert.Equal(50, sim.State.PlayerById(0).Treasury);
        before = sim.ComputeHash(); Assert.False(sim.TryApply(action, out _)); Assert.Equal(before, sim.ComputeHash());
        before = sim.ComputeHash();
        Assert.False(sim.TryApply(new SuppressRebellionCommand { Player = 1, CityId = city.Id }, out _));
        Assert.Equal(before, sim.ComputeHash());
    }

    [Fact]
    public void Inciting_a_revolt_raises_instability_and_starts_unrest_once()
    {
        var sim = P7ModFixture.Scenario(mods.Revolution);
        var city = P7ModFixture.City(sim);
        var state = State(sim, city, 100);
        Assert.True(Service(sim).Incite(sim, city.Id));
        Assert.Equal(400, state.InstabilityIndex);
        Assert.Equal(sim.State.Turn + 1 + 3 * 85 / 100, state.UnrestUntilTurn);
        // A city already in disorder cannot be incited again, so the pressure cannot stack.
        city.Extensions.Set(sim.Content, new CityRuleState { Disorder = true });
        Assert.False(Service(sim).Incite(sim, city.Id));
        Assert.Equal(400, state.InstabilityIndex);
    }

    [Fact]
    public void Spy_revolt_feeds_the_revolution_instability_system()
    {
        var sim = P7ModFixture.Scenario(SpiesAndRevolution.Value);
        var city = P7ModFixture.City(sim, 57, owner: 1);
        P7ModFixture.City(sim, 40, owner: 0); // A return city for the spy; every mission needs one.
        var spy = P7ModFixture.Unit(sim, city, "military:spy", owner: 0);
        spy.MovesLeft = 60;
        sim.Content.Services.Get<IDiplomacyService>().Single().EstablishContact(sim, 0, 1);
        var team = new EspionageTeamState(); team.Points[1] = 1000;
        sim.State.TeamById(0).Extensions.Set(sim.Content, team);
        var before = city.Extensions.Get<RevolutionCityState>(sim.Content)?.InstabilityIndex ?? 0;
        Assert.True(sim.TryApply(new ExecuteEspionageMissionCommand
            { Player = 0, Unit = spy.Id, City = city.Id, Mission = EspionageMissionKind.InciteRevolt }, out var error), error);
        Assert.True(sim.Events.Published.OfType<EspionageMissionResult>().Single().Success);
        // The Revolution mod's incite raises the city's instability as well as causing disorder.
        Assert.True((city.Extensions.Get<RevolutionCityState>(sim.Content)?.InstabilityIndex ?? 0) > before);
        Assert.True(GovernmentStatus.IsDisorder(sim, city));
    }

    [Fact]
    public void Cities_at_risk_reports_saved_instability_above_the_warning_tier()
    {
        var sim = P7ModFixture.Scenario(mods.Revolution);
        P7ModFixture.City(sim);
        var warning = P7ModFixture.City(sim, 20); State(sim, warning, 400);
        var mutiny = P7ModFixture.City(sim, 26); State(sim, mutiny, 900);
        Assert.Equal(new[] { warning.Id, mutiny.Id }, Service(sim).CitiesAtRisk(sim, 0));
        Assert.Empty(Service(sim).CitiesAtRisk(sim, 1));
    }

    [Fact]
    public void Suppression_price_falls_with_the_instability_actually_removed()
    {
        var sim = P7ModFixture.Scenario(mods.Revolution);
        var city = P7ModFixture.City(sim);
        var state = State(sim, city, 10);
        sim.State.PlayerById(0).Treasury = 1000;
        // A full 400-point reduction costs 100 gold, so a 10-point reduction costs 100 * 10 / 400 = 2.
        Assert.Equal(2, Service(sim).DescribeCityStability(sim, city.Id).SuppressionGold);
        Assert.True(sim.TryApply(new SuppressRebellionCommand { Player = 0, CityId = city.Id }, out var error), error);
        Assert.Equal(998, sim.State.PlayerById(0).Treasury);
        Assert.Equal(0, state.InstabilityIndex);
        // Nothing left to suppress, and the price of nothing is nothing.
        Assert.Equal(0, Service(sim).DescribeCityStability(sim, city.Id).SuppressionGold);
        Assert.Contains("no instability", Service(sim).ValidateSuppression(sim, 0, city.Id, null));
    }

    [Fact]
    public void Mutiny_adds_unhappiness_while_warning_stays_a_display_alert()
    {
        var sim = P7ModFixture.Scenario(mods.Revolution);
        var city = P7ModFixture.City(sim);
        long Mutiny() => ModifierSink.Collect(new(sim, ModifierScope.City, 0, city)).Ledger
            .Lines.Where(l => l.Source == "revolution:mutiny").Sum(l => l.Amount);
        State(sim, city, 500);
        Assert.Equal(0, Mutiny());
        State(sim, city, 700);
        Assert.Equal(2, Mutiny());
        Assert.Equal(CityStabilityStatus.Mutiny, Service(sim).DescribeCityStability(sim, city.Id).Status);
    }

    /// <summary>Revolution and Espionage are both optional mods; the capture reset spans both.</summary>
    private static readonly Lazy<ContentDb> SpiesAndRevolution = new(() => ModLoader.Build(new LoadedMods().Sources
        .Concat(new[] { "Revolution", "Espionage", "Diplomacy" }
            .Select(name => ModLoader.FromDirectory(Path.Combine(AppContext.BaseDirectory, "optional-mods", name))))));

    [Fact]
    public void Capture_resets_the_losers_spy_and_revolution_city_state()
    {
        var sim = P7ModFixture.Scenario(SpiesAndRevolution.Value);
        var city = P7ModFixture.City(sim);
        city.Extensions.Set(sim.Content, new RevolutionCityState { InstabilityIndex = 900, RebellionCooldownUntilTurn = 5, UnrestUntilTurn = 5 });
        city.Extensions.Set(sim.Content, new EspionageCityState { RevoltUntilTurn = 9, UnhappyUntilTurn = 9 });
        sim.Events.Publish(new CityCaptured(city.Id, 0, 1));
        var revolution = city.Extensions.Get<RevolutionCityState>(sim.Content);
        var espionage = city.Extensions.Get<EspionageCityState>(sim.Content);
        Assert.Equal(0, revolution?.InstabilityIndex ?? 0);
        Assert.Equal(0, revolution?.RebellionCooldownUntilTurn ?? 0);
        Assert.Equal(0, revolution?.UnrestUntilTurn ?? 0);
        Assert.Equal(0, espionage?.RevoltUntilTurn ?? 0);
        Assert.Equal(0, espionage?.UnhappyUntilTurn ?? 0);
    }

    [Fact]
    public void Full_turn_rebellion_and_saved_cooldown_replay_have_identical_hashes()
    {
        var sim = P7ModFixture.Scenario(mods.Combined);
        var city = P7ModFixture.City(sim, population: 30);
        State(sim, city, 990);
        var peer = SaveGame.Load(mods.Combined, SaveGame.Save(sim));
        sim.AdvanceTurn(); peer.AdvanceTurn();
        Assert.Single(sim.Events.PreviousTurn.OfType<CityRebellionTriggered>());
        Assert.Equal(sim.ComputeHash(), peer.ComputeHash());
        peer = SaveGame.Load(mods.Combined, SaveGame.Save(sim));
        Assert.Equal(sim.ComputeHash(), peer.ComputeHash());
        for (int i = 0; i < 12; i++)
        {
            sim.AdvanceTurn(); peer.AdvanceTurn();
            Assert.Equal(sim.ComputeHash(), peer.ComputeHash());
        }
    }
}
