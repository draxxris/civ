using Ariel.Content;
using Ariel.Game;
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
        // Normal research speed is 84%; each term truncates separately.
        Assert.Equal(6 * 3 * 100 / 84, Contribution(bare, "revolution:distance"));
        Assert.Equal(15 * 100 / 84, Contribution(bare, "revolution:garrison"));
        Assert.True(Contribution(bare, "revolution:wellbeing") > 0);
        P7ModFixture.Unit(sim, city);
        sim.State.PlayerById(0).Civics["government:civicoption_government"] = "government:despotism";
        sim.State.PlayerById(0).Extensions.Set(sim.Content, new GovernmentPlayerState { AnarchyTurns = 2 });
        sim.State.PlayerById(0).Extensions.Set(sim.Content, new GreatPeoplePlayerState { GoldenAgeTurns = 2 });
        var protectedCity = service.DescribeCityStability(sim, city.Id);
        Assert.Equal(-12 * 100 / 84, Contribution(protectedCity, "revolution:garrison"));
        Assert.Equal(8 * 100 / 84, Contribution(protectedCity, "government:despotism"));
        Assert.Equal(40 * 100 / 84, Contribution(protectedCity, "revolution:anarchy"));
        Assert.Equal(-30 * 100 / 84, Contribution(protectedCity, "revolution:golden_age"));
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
        Assert.Equal(10 * 100 / 84, Contribution(mild, "revolution:non_state_religion"));
        Assert.Equal(-10 * 100 / 84, Contribution(mild, "revolution:state_religion"));
        sim.State.PlayerById(0).Civics["government:civicoption_religion"] = "government:intolerant";
        var intolerant = Service(sim).DescribeCityStability(sim, city.Id);
        Assert.Equal(20 * 100 / 84, Contribution(intolerant, "revolution:non_state_religion"));
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
        Assert.Equal(neighbors.Skip(3).Take(2).Select(t => sim.State.Grid.HexOf(t)), rebels.Select(u => u.Position));
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
        Assert.Equal(400, city.Extensions.Get<RevolutionCityState>(sim.Content)!.InstabilityIndex);
        Assert.Empty(Assert.Single(sim.Events.Published.OfType<CityRebellionTriggered>()).Units);
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
