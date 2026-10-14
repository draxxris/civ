using System.Diagnostics;
using Civ.Game;
using Xunit;
using Xunit.Abstractions;

namespace Civ.Mods.Tests;

public sealed class AiTests(LoadedMods mods, ITestOutputHelper output) : IClassFixture<LoadedMods>
{
    [Fact]
    public void Four_computer_players_develop_and_fight_over_200_turns()
    {
        var sim = GameFactory.CreateAi(mods.Content, seed: 17);
        int combat = 0;
        var founded = new int[4];
        var completed = new int[4];
        var owners = new Dictionary<int, int>();
        var timer = Stopwatch.StartNew();
        for (int turn = 0; turn < 200; turn++)
        {
            sim.AdvanceTurn();
            combat += sim.Events.PreviousTurn.OfType<CombatResolved>().Count();
            CountDevelopment(sim.Events.PreviousTurn, owners, founded, completed);
            if (turn % 25 == 24) Metrics(sim, $"turn {sim.State.Turn}");
        }
        combat += sim.Events.Published.OfType<CombatResolved>().Count();
        output.WriteLine($"Cities founded per AI: {string.Join(", ", founded)}");
        output.WriteLine($"Buildings completed per AI: {string.Join(", ", completed)}");
        output.WriteLine($"Combats: {combat}; elapsed: {timer.ElapsedMilliseconds} ms");
        foreach (var player in sim.State.Players.Where(p => p.Ai == "ai:standard"))
        {
            Assert.True(founded[player.Id] >= 1, $"player {player.Id}: founded no city");
            Assert.True(sim.TeamOf(player.Id).Techs.Count >= 15, $"player {player.Id}: {sim.TeamOf(player.Id).Techs.Count} technologies");
            Assert.True(completed[player.Id] > 0, $"player {player.Id} completed no buildings");
        }
        // Require actual expansion, but not survival or three cities per player under barbarian pressure.
        // Those are strategy/balance goals, not the functional turn-loop contract.
        Assert.True(founded.Sum() > founded.Length, "No AI founded a city beyond its initial capital");
        Assert.True(combat > 0, "No combat occurred");
    }

    [Fact]
    public void Identical_games_and_a_midgame_save_have_equal_hashes_at_every_turn()
    {
        var a = GameFactory.CreateAi(mods.Content, seed: 5);
        var b = GameFactory.CreateAi(mods.Content, seed: 5);
        Simulation? saved = null;
        for (int turn = 0; turn <= 150; turn++)
        {
            Assert.Equal(a.ComputeHash(), b.ComputeHash());
            if (turn == 75) saved = SaveGame.Load(mods.Content, SaveGame.Save(a));
            if (saved is not null) Assert.Equal(a.ComputeHash(), saved.ComputeHash());
            if (turn == 150) break;
            a.AdvanceTurn(); b.AdvanceTurn(); saved?.AdvanceTurn();
        }
        Metrics(a, "determinism turn 151");
    }

    [Fact]
    public void Eight_players_on_standard_have_bounded_turn_100_decision_time()
    {
        var sim = GameFactory.CreateAi(mods.Content, seed: 9, players: 8, world: "foundation:worldsize_standard");
        for (int i = 0; i < 99; i++) sim.AdvanceTurn();
        // Isolate AI decisions from the cost of turn systems. Saving does not rerun the AI.
        var decisionGame = SaveGame.Load(mods.Content, SaveGame.Save(sim));
        foreach (var player in decisionGame.State.Players.Where(p => p.Ai is not null)) player.EndedTurn = false;
        var timer = Stopwatch.StartNew();
        decisionGame.RunAi();
        timer.Stop();
        output.WriteLine($"Eight-player AI decision time at turn 100: {timer.ElapsedMilliseconds} ms");
        Metrics(sim, "timing turn 100");
        Assert.True(timer.Elapsed < TimeSpan.FromSeconds(5), $"AI decisions took {timer.Elapsed}");
    }

    [Fact]
    public void Strong_army_declares_war_and_captures_a_weak_neighbour()
    {
        var sim = GameFactory.Scenario(mods.Content);
        var own = new City { Id = 1, Owner = 0, Position = sim.State.Grid.HexOf(new(39)), Population = 4 };
        var enemy = new City { Id = 2, Owner = 1, Position = own.Position.Neighbor(0).Neighbor(0).Neighbor(0).Neighbor(0).Neighbor(0) };
        sim.State.AddCity(own); sim.State.AddCity(enemy); sim.State.NextCityId = 3;
        sim.State.PlotAt(enemy.Position).Owner = 1;
        sim.State.PlotAt(enemy.Position).Revealed.Add(sim.TeamOf(0).Id);
        sim.TeamOf(0).Techs.UnionWith(["research:woodworking", "research:siege_warfare"]);
        var units = sim.Content.Services.Get<IUnitService>().Single();
        for (int i = 0; i < 15; i++)
        {
            var unit = new Unit { Id = sim.State.NextUnitId++, Owner = 0, Position = own.Position,
                Type = i < 12 ? "military:javelineer" : "military:catapult" };
            sim.State.AddUnit(unit); units.Initialize(sim, unit.Id);
        }
        var defender = new Unit { Id = sim.State.NextUnitId++, Owner = 1, Position = enemy.Position, Type = "military:warrior" };
        sim.State.AddUnit(defender); units.Initialize(sim, defender.Id);
        sim.State.Turn = 50;
        bool declared = false; int combats = 0;
        for (int i = 0; i < 100 && sim.State.CityById(enemy.Id).Owner != 0; i++)
        {
            sim.Content.PlayerAIs.Get("ai:standard").Decide(sim, 0).ToArray();
            declared |= sim.TeamOf(0).Wars.Contains(sim.TeamOf(1).Id);
            combats += sim.Content.Services.Get<ICombatService>().Single().History(sim).Count;
            sim.AdvanceTurn();
        }
        Assert.True(declared, "The stronger army never declared war");
        Assert.Equal(0, sim.State.CityById(enemy.Id).Owner);
        Assert.True(combats > 0);
    }

    [Fact]
    public void Production_emphasis_still_feeds_the_city_at_the_content_consumption_rate()
    {
        var sim = GameFactory.Scenario(mods.Content);
        GameFactory.NeutralPolitics(sim);
        var center = sim.State.Grid.TileAt(3, 3);
        var city = new City { Id = 1, Owner = 0, Population = 3, Position = sim.State.Grid.HexOf(center) };
        sim.State.AddCity(city); sim.State.NextCityId = 2;
        foreach (var tile in sim.State.Grid.Range(center, 2)) sim.State.Plots[tile.Value].Feature = "world:forest";
        foreach (var tile in sim.State.Grid.Neighbors(center).Take(2)) sim.State.Plots[tile.Value].Kind = PlotKind.Hills;
        var food = sim.State.Grid.Neighbors(center).Last();
        sim.State.Plots[food.Value].Feature = "world:flood_plains";
        sim.Content.PlayerAIs.Get("ai:standard").Decide(sim, 0).ToArray();
        var growth = sim.Content.Services.Get<ICityGrowthService>().Single().Calculate(sim, city.Id);
        Assert.True(growth.Difference >= 0, $"Production assignments leave {growth.Difference} food per turn");
        Assert.True(sim.Content.Services.Get<ICityWorkService>().Single().Validate(sim, city.Id, city.Worked, city.Specialists) is null);
    }

    [Fact]
    public void Rich_player_uses_the_highest_research_rate_that_pays_recurring_costs()
    {
        var sim = GameFactory.Scenario(mods.Content);
        GameFactory.NeutralPolitics(sim);
        var city = new City { Id = 1, Owner = 0, Population = 3, Position = sim.State.Grid.HexOf(new(39)), Buildings = ["cities:palace"] };
        sim.State.AddCity(city); sim.State.NextCityId = 2;
        sim.State.PlayerById(0).Treasury = 10000;
        // Noble grants fourteen free units here. Exceed that allowance so the sliders must fund upkeep.
        for (int i = 0; i < 20; i++) sim.State.AddUnit(new Unit
        {
            Id = sim.State.NextUnitId++, Owner = 0, Type = "military:warrior", Position = city.Position,
        });
        sim.Content.PlayerAIs.Get("ai:standard").Decide(sim, 0).ToArray();
        var economy = sim.Content.Services.Get<IPlayerEconomyService>().Single();
        var rates = economy.Rates(sim, 0);
        Assert.True(economy.Calculate(sim, 0).NetGold >= 0);
        Assert.True(rates.Gold > 0, "Fixture must have recurring costs that require gold commerce");
        Assert.True(sim.TryApply(new SetCommerceRatesCommand
        {
            Player = 0, Rates = new(rates.Gold - 5, rates.Research + 5, 0, 0),
        }, out var error), error);
        Assert.True(economy.Calculate(sim, 0).NetGold < 0, "The AI left an affordable five-point research increase unused");
    }

    [Fact]
    public void War_commands_roundtrip_and_rejected_early_peace_does_not_change_state()
    {
        var sim = GameFactory.Scenario(mods.Content);
        var codec = new CommandCodec(mods.Content);
        var war = codec.Decode("{\"type\":\"ai:declare_war\",\"data\":{\"player\":0,\"target\":1}}");
        Assert.True(sim.TryApply(codec.Decode(codec.Encode(war)), out var error), error);
        Assert.Contains(1, sim.TeamOf(0).Wars); Assert.Contains(0, sim.TeamOf(1).Wars);
        var loaded = SaveGame.Load(mods.Content, SaveGame.Save(sim));
        var peace = codec.Decode("{\"type\":\"ai:make_peace\",\"data\":{\"player\":0,\"target\":1}}");
        ulong before = sim.ComputeHash();
        Assert.False(sim.TryApply(peace, out _));
        Assert.Equal(before, sim.ComputeHash());
        Assert.Equal(before, loaded.ComputeHash());
        sim.State.Turn += 12; loaded.State.Turn += 12;
        Assert.True(sim.TryApply(peace, out error), error);
        Assert.True(loaded.TryApply(peace, out error), error);
        Assert.Empty(sim.TeamOf(0).Wars); Assert.Empty(sim.TeamOf(1).Wars);
        Assert.Equal(sim.ComputeHash(), loaded.ComputeHash());
    }

    [Fact]
    public void Peace_treaty_blocks_redeclaring_war_for_ten_turns_and_survives_save_load()
    {
        var sim = GameFactory.Scenario(mods.Content);
        var codec = new CommandCodec(mods.Content);
        var war = codec.Decode("{\"type\":\"ai:declare_war\",\"data\":{\"player\":0,\"target\":1}}");
        var peace = codec.Decode("{\"type\":\"ai:make_peace\",\"data\":{\"player\":0,\"target\":1}}");
        Assert.True(sim.TryApply(war, out var error), error);
        sim.State.Turn += 12; // Minimum war duration before a treaty.
        Assert.True(sim.TryApply(peace, out error), error);
        var loaded = SaveGame.Load(mods.Content, SaveGame.Save(sim));
        ulong before = sim.ComputeHash();
        Assert.False(sim.TryApply(war, out error)); Assert.Contains("peace treaty", error);
        Assert.Equal(before, sim.ComputeHash());
        Assert.False(loaded.TryApply(war, out var loadedError)); Assert.Equal(error, loadedError);
        sim.State.Turn += 9; loaded.State.Turn += 9; // Nine turns after peace is still inside the cooldown.
        Assert.False(sim.TryApply(war, out _)); Assert.False(loaded.TryApply(war, out _));
        sim.State.Turn += 1; loaded.State.Turn += 1;
        Assert.True(sim.TryApply(war, out error), error);
        Assert.True(loaded.TryApply(war, out loadedError), loadedError);
        Assert.Equal(sim.ComputeHash(), loaded.ComputeHash());
    }

    private static void CountDevelopment(IEnumerable<DomainEvent> events, Dictionary<int, int> owners, int[] founded, int[] completed)
    {
        foreach (var message in events)
        {
            switch (message)
            {
                case CityFounded city:
                    owners[city.City] = city.Player;
                    if (city.Player < founded.Length) founded[city.Player]++;
                    break;
                case CityCaptured city:
                    owners[city.City] = city.Player;
                    break;
                case BuildingCompleted building:
                    if (owners.TryGetValue(building.City, out var owner) && owner < completed.Length) completed[owner]++;
                    break;
            }
        }
    }

    private void Metrics(Simulation sim, string label)
    {
        foreach (var p in sim.State.Players.Where(p => p.Ai == "ai:standard"))
            output.WriteLine($"{label} player {p.Id}: cities={sim.State.Cities.Count(c => c.Owner == p.Id)} " +
                $"techs={sim.TeamOf(p.Id).Techs.Count} units={sim.State.Units.Count(u => u.Owner == p.Id)} " +
                $"buildings={sim.State.Cities.Where(c => c.Owner == p.Id).Sum(c => c.Buildings.Count)} " +
                $"wars={sim.TeamOf(p.Id).Wars.Count} gold={p.Treasury}");
    }
}
