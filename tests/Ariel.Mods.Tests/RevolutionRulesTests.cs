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

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Rebels_and_barbarians_are_one_faction_whichever_appears_first(bool rebellionFirst)
    {
        var sim = P7ModFixture.Scenario(mods.Revolution);
        var city = P7ModFixture.City(sim);
        State(sim, city, 1000);
        var faction = sim.Rules().Barbarians;
        int players = sim.State.Players.Count;

        int? barbarian = rebellionFirst ? null : faction.Ensure(sim);
        Rebellions(sim);
        int rebel = Assert.Single(sim.Events.Published.OfType<CityRebellionTriggered>()).RebelPlayer;
        barbarian ??= faction.Ensure(sim);

        Assert.Equal(barbarian, rebel);
        Assert.Equal(players + 1, sim.State.Players.Count);
        Assert.Equal(rebel, PlayerRules.BarbarianPlayer(sim));
        Assert.Equal([rebel], sim.TeamOf(rebel).Members);
        Assert.All(sim.State.Teams.Where(t => t.Id != sim.TeamOf(rebel).Id), t => Assert.Contains(t.Id, sim.TeamOf(rebel).Wars));
        var loaded = SaveGame.Load(mods.Revolution, SaveGame.Save(sim));
        Assert.Equal(rebel, loaded.Rules().Barbarians.Ensure(loaded));
        Assert.Equal(players + 1, loaded.State.Players.Count);
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
        Assert.Equal(PlayerRules.BarbarianPlayer(sim), rebellion.RebelPlayer);
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
        Assert.Single(sim.State.Players, p => p.Name == "Barbarians");
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
        // An enclosed city releases pressure and starts cooldown without spawning units, preventing deadlock.
        var state = city.Extensions.Get<RevolutionCityState>(sim.Content)!;
        Assert.Equal(400, state.InstabilityIndex);
        var rebellion = Assert.Single(sim.Events.Published.OfType<CityRebellionTriggered>());
        Assert.Equal(-1, rebellion.RebelPlayer);
        Assert.Empty(rebellion.Units);
        Assert.Equal(600, rebellion.InstabilityReleased);
        Assert.True(state.IsRebellionOnCooldown(sim.State.Turn));
        // Cooldown prevents immediate re-triggering.
        state.InstabilityIndex = 1000;
        Rebellions(sim);
        Assert.Single(sim.Events.Published.OfType<CityRebellionTriggered>());
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
        sim.Rules().Relations.EstablishContact(sim, 0, 1);
        var team = new EspionageTeamState(); team.Points[1] = 1000;
        sim.State.TeamById(0).Extensions.Set(sim.Content, team);
        var before = city.Extensions.Get<RevolutionCityState>(sim.Content)?.InstabilityIndex ?? 0;
        Assert.True(sim.TryApply(EspionageMissions.Order(0, spy.Id, EspionageMissionKind.InciteRevolt), out var error), error);
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

    [Fact]
    public void RevolutionCityState_invariants_and_transitions_manage_instability_and_timers()
    {
        var state = new RevolutionCityState();
        Assert.Equal(0, state.InstabilityIndex);
        Assert.Equal(0, state.LastUpdatedTurn);
        Assert.Equal(0, state.RebellionCooldownUntilTurn);
        Assert.Equal(0, state.UnrestUntilTurn);
        Assert.False(state.IsRebellionOnCooldown(0));
        Assert.False(state.IsInUnrest(0));
        Assert.Equal(0, state.CooldownTurnsRemaining(0));
        Assert.Equal(0, state.UnrestTurnsRemaining(0));

        // Clamping non-negative values
        state.InstabilityIndex = -10;
        state.LastUpdatedTurn = -1;
        state.RebellionCooldownUntilTurn = -5;
        state.UnrestUntilTurn = -2;
        Assert.Equal(0, state.InstabilityIndex);
        Assert.Equal(0, state.LastUpdatedTurn);
        Assert.Equal(0, state.RebellionCooldownUntilTurn);
        Assert.Equal(0, state.UnrestUntilTurn);

        // RecordInstabilityChange
        state.RecordInstabilityChange(500, turn: 5);
        Assert.Equal(500, state.InstabilityIndex);
        Assert.Equal(5, state.LastUpdatedTurn);
        state.RecordInstabilityChange(-200, turn: 6);
        Assert.Equal(300, state.InstabilityIndex);
        Assert.Equal(6, state.LastUpdatedTurn);
        state.RecordInstabilityChange(-500, turn: 7);
        Assert.Equal(0, state.InstabilityIndex);
        Assert.Equal(7, state.LastUpdatedTurn);

        // Suppress
        state.InstabilityIndex = 400;
        state.Suppress(150);
        Assert.Equal(250, state.InstabilityIndex);
        state.Suppress(500);
        Assert.Equal(0, state.InstabilityIndex);

        // Incite
        state.Incite(currentTurn: 10, additionalInstability: 300, unrestTurns: 3);
        Assert.Equal(300, state.InstabilityIndex);
        Assert.Equal(14, state.UnrestUntilTurn); // 10 + 1 + 3
        Assert.True(state.IsInUnrest(10));
        Assert.True(state.IsInUnrest(13));
        Assert.False(state.IsInUnrest(14));
        Assert.Equal(4, state.UnrestTurnsRemaining(10));
        Assert.Equal(0, state.UnrestTurnsRemaining(14));

        // TriggerRebellion
        state.InstabilityIndex = 1200;
        state.TriggerRebellion(currentTurn: 20, threshold: 1000, release: 600, cooldownTurns: 8, unrestTurns: 3);
        Assert.Equal(400, state.InstabilityIndex); // 1000 - 600
        Assert.Equal(29, state.RebellionCooldownUntilTurn); // 20 + 1 + 8
        Assert.Equal(24, state.UnrestUntilTurn); // 20 + 1 + 3
        Assert.True(state.IsRebellionOnCooldown(20));
        Assert.True(state.IsRebellionOnCooldown(28));
        Assert.False(state.IsRebellionOnCooldown(29));
        Assert.Equal(9, state.CooldownTurnsRemaining(20));
        Assert.Equal(0, state.CooldownTurnsRemaining(29));
    }

    [Fact]
    public void Status_is_capped_at_mutiny_while_rebellion_cooldown_is_active()
    {
        var sim = P7ModFixture.Scenario(mods.Revolution);
        var city = P7ModFixture.City(sim);
        var state = State(sim, city, 1200);
        state.RebellionCooldownUntilTurn = sim.State.Turn + 5;

        // Above 1000 threshold, but cooldown is active -> capped at Mutiny
        var desc = Service(sim).DescribeCityStability(sim, city.Id);
        Assert.Equal(CityStabilityStatus.Mutiny, desc.Status);
        Assert.Equal(5, desc.CooldownTurns);

        // Advance past cooldown -> status reflects Rebellion
        sim.State.Turn += 5;
        desc = Service(sim).DescribeCityStability(sim, city.Id);
        Assert.Equal(CityStabilityStatus.Rebellion, desc.Status);
        Assert.Equal(0, desc.CooldownTurns);
    }

    [Fact]
    public void Government_timers_run_before_revolution_instability_at_player_end()
    {
        var sim = P7ModFixture.Scenario(mods.Combined);
        int govOrder = sim.Content.TurnSystems.Get("government:timers").Order;
        int revOrder = sim.Content.TurnSystems.Get("revolution:instability").Order;
        Assert.Equal(TurnPhase.PlayerEnd, govOrder);
        Assert.Equal(TurnPhase.PlayerEnd + 1, revOrder);
        Assert.True(govOrder < revOrder);
    }
}
