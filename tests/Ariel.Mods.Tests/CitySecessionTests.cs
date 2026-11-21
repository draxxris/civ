using Ariel.Game;
using Xunit;
using S = Ariel.Content.Schema;

namespace Ariel.Mods.Tests;

/// <summary>A city can leave its owner and found a nation: the base operation, and the rule that a city a faction
/// holds long enough breaks away.</summary>
public sealed class CitySecessionTests(LoadedMods mods) : IClassFixture<LoadedMods>
{
    private const string Warrior = "military:warrior";

    private Simulation Game(int players = 3)
    {
        var sim = GameFactory.CreateAi(mods.Content, players: players);
        return sim;
    }

    /// <summary>A city for the player on open land near its start, far enough from the cities already placed.</summary>
    private static City Found(Simulation sim, int player, string name, int population = 5)
    {
        var grid = sim.State.Grid;
        var start = grid.HexOf(sim.State.PlayerById(player).StartingPlot!.Value);
        var tile = grid.Tiles.Where(t =>
            {
                var plot = sim.State.Plots[t.Value];
                var hex = grid.HexOf(t);
                return !sim.Content.Get<S.TerrainDef>(plot.Terrain).IsWater && plot.Kind != PlotKind.Peak
                    && sim.State.Cities.All(c => grid.Distance(c.Position, hex) >= 4);
            })
            .OrderBy(t => grid.Distance(grid.HexOf(t), start)).ThenBy(t => t.Value).First();
        var city = new City { Id = sim.State.NextCityId++, Owner = player, Name = name, Position = grid.HexOf(tile), Population = population };
        sim.State.AddCity(city);
        foreach (var near in grid.Range(tile, 1))
        {
            var plot = sim.State.Plots[near.Value];
            plot.Owner = player; plot.Culture[player] = 50;
        }
        return city;
    }

    /// <summary>The capital the game gave the player at setup.</summary>
    private static City Capital(Simulation sim, int player) =>
        sim.State.Cities.Single(c => c.Owner == player && c.Buildings.Any(id => sim.Content.Get<S.BuildingDef>(id).Capital));

    private static void RunBreakaway(Simulation sim) => sim.Content.TurnSystems.Get("cities:faction_breakaway").Run(sim);

    private static (Simulation Sim, int Barbarian, City City) BarbarianHeld(Simulation sim)
    {
        var city = Found(sim, 1, "Outpost");
        int barbarian = GameFactory.EnsureBarbarian(sim);
        sim.Rules().CityCapture.Capture(sim, city.Id, barbarian);
        sim.Rules().Units.Create(sim, barbarian, Warrior, city.Position);
        return (sim, barbarian, sim.State.CityById(city.Id));
    }

    [Fact]
    public void A_city_a_faction_holds_long_enough_breaks_away_as_a_new_nation()
    {
        var (sim, barbarian, city) = BarbarianHeld(Game());
        int held = GameFactory.BarbarianFaction(sim.Content)!.BreakawayTurns;
        Assert.Equal(20, held);
        int players = sim.State.Players.Count;
        int garrison = sim.State.UnitsAt(city.Position).Single(u => u.Owner == barbarian).Id;

        sim.State.Turn += GamePace.Scale(sim, held) - 1;
        RunBreakaway(sim);
        Assert.Equal(barbarian, sim.State.CityById(city.Id).Owner);
        Assert.Equal(players, sim.State.Players.Count);

        sim.State.Turn++;
        RunBreakaway(sim);

        var seceded = sim.State.CityById(city.Id);
        int nation = seceded.Owner;
        Assert.Equal(players, nation);
        Assert.True(PlayerRules.IsNation(sim, nation));
        Assert.Equal("ai:standard", sim.State.PlayerById(nation).Ai);
        Assert.DoesNotContain(sim.State.Players.Where(p => p.Id != nation), p => p.Civilization == sim.State.PlayerById(nation).Civilization);
        Assert.Equal(city.Population, seceded.Population);
        Assert.Contains(seceded.Buildings, id => sim.Content.Get<S.BuildingDef>(id).Capital);
        Assert.Equal(nation, sim.State.PlotAt(seceded.Position).Owner);
        // The faction row says how many free defenders it starts with, besides the barbarians that went over.
        int free = GameFactory.BarbarianFaction(sim.Content)!.BreakawayGarrison;
        Assert.Equal(2, free);
        var owned = sim.State.UnitsAt(seceded.Position).Where(u => u.Owner == nation).ToArray();
        Assert.Equal(1 + free, owned.Length);
        // The barbarians in the city went over to it; the barbarians stay at war with it and nobody else is.
        Assert.Equal(nation, sim.State.UnitById(garrison).Owner);
        Assert.Equal([sim.TeamOf(barbarian).Id], sim.TeamOf(nation).Wars);
        Assert.Subset(sim.TeamOf(nation).Techs.ToHashSet(), sim.TeamOf(barbarian).Techs.ToHashSet());
        Assert.Contains(sim.Events.Published.OfType<CitySeceded>(), e => e.City == city.Id && e.PreviousOwner == barbarian && e.Player == nation);
        Assert.DoesNotContain(sim.Events.Published.OfType<CityCaptured>(), e => e.Player == nation);

        // Held by a nation now, it does not break away again.
        sim.State.Turn += 100;
        RunBreakaway(sim);
        Assert.Equal(nation, sim.State.CityById(city.Id).Owner);
    }

    [Fact]
    public void A_new_nations_free_defenders_are_the_best_its_technologies_allow()
    {
        var plain = Game();
        var city = Found(plain, 1, "Early");
        int early = plain.Rules().CityCapture.Secede(plain, city.Id, new(Garrison: 2));
        var first = plain.State.UnitsAt(city.Position).Where(u => u.Owner == early).Select(u => u.Type).Distinct().Single();
        Assert.NotEqual("military:musketman", first);

        // The same breakaway once its former owner knows gunpowder: the newcomer inherits the knowledge.
        var late = Game();
        late.TeamOf(1).Techs.Add("research:flintlock");
        var other = Found(late, 1, "Late");
        int nation = late.Rules().CityCapture.Secede(late, other.Id, new(Garrison: 2));
        var units = late.State.UnitsAt(other.Position).Where(u => u.Owner == nation).ToArray();
        Assert.Equal(2, units.Length);
        Assert.All(units, u => Assert.Equal("military:musketman", u.Type));
    }

    [Fact]
    public void A_secession_with_no_garrison_creates_no_units()
    {
        var sim = Game();
        var city = Found(sim, 1, "Bare");
        int nation = sim.Rules().CityCapture.Secede(sim, city.Id, new());
        Assert.DoesNotContain(sim.State.UnitsAt(city.Position), u => u.Owner == nation);
    }

    [Fact]
    public void The_new_nation_plays_on_in_sync_through_save_and_reload()
    {
        var (sim, _, city) = BarbarianHeld(Game());
        sim.State.Turn += 40;
        RunBreakaway(sim);
        int nation = sim.State.CityById(city.Id).Owner;
        Assert.True(PlayerRules.IsNation(sim, nation));

        var copy = SaveGame.Load(sim.Content, SaveGame.Save(sim));
        Assert.Equal(sim.ComputeHash(), copy.ComputeHash());
        for (int turn = 0; turn < 10; turn++)
        {
            sim.AdvanceTurn(); copy.AdvanceTurn();
            Assert.Equal(sim.ComputeHash(), copy.ComputeHash());
        }
        Assert.True(PlayerRules.IsAlive(sim, nation));
    }

    [Fact]
    public void A_nations_city_secedes_at_war_with_a_garrison_and_puts_its_owners_units_out()
    {
        var sim = Game();
        var city = Found(sim, 1, "Restless");
        int loyalist = sim.Rules().Units.Create(sim, 1, Warrior, city.Position).Id;
        int players = sim.State.Players.Count;

        Assert.Null(sim.Rules().CityCapture.ValidateSecession(sim, city.Id));
        int nation = sim.Rules().CityCapture.Secede(sim, city.Id, new(WarWithFormerOwner: true, Garrison: 2));

        Assert.Equal(players, nation);
        Assert.Equal(nation, sim.State.CityById(city.Id).Owner);
        Assert.Contains(sim.TeamOf(1).Id, sim.TeamOf(nation).Wars);
        Assert.Equal(sim.State.PlayerById(1).Ai, sim.State.PlayerById(nation).Ai);
        var defenders = sim.State.UnitsAt(city.Position).Where(u => u.Owner == nation).ToArray();
        Assert.Equal(2, defenders.Length);
        Assert.All(defenders, u => Assert.Equal(defenders[0].Type, u.Type));
        // The loyal unit was put out to a neighbouring plot, not destroyed and not left inside.
        var unit = sim.State.FindUnit(loyalist);
        Assert.NotNull(unit);
        Assert.Equal(1, unit!.Owner);
        Assert.Equal(1, sim.State.Grid.Distance(unit.Position, city.Position));
        // The former owner keeps its capital and its other land.
        Assert.Equal(1, Capital(sim, 1).Owner);
        Assert.True(sim.Rules().Relations.HasMet(sim, sim.TeamOf(1).Id, sim.TeamOf(nation).Id));
    }

    [Fact]
    public void A_capital_and_a_last_city_do_not_secede()
    {
        var sim = Game();
        var capital = Capital(sim, 1);
        var only = Capital(sim, 2);
        only.Buildings.RemoveAll(id => sim.Content.Get<S.BuildingDef>(id).Capital);

        Assert.Equal("A capital does not break away", sim.Rules().CityCapture.ValidateSecession(sim, capital.Id));
        Assert.Equal("A nation's last city does not break away", sim.Rules().CityCapture.ValidateSecession(sim, only.Id));
        Assert.Throws<InvalidOperationException>(() => sim.Rules().CityCapture.Secede(sim, only.Id, new()));
    }

    [Fact]
    public void A_human_players_city_secedes_to_a_computer_player()
    {
        var sim = Game();
        typeof(Player).GetProperty(nameof(Player.Ai))!.SetValue(sim.State.PlayerById(0), null);
        var city = Found(sim, 0, "Restless");

        int nation = sim.Rules().CityCapture.Secede(sim, city.Id, new());

        Assert.Null(sim.State.PlayerById(0).Ai);
        Assert.Equal("ai:standard", sim.State.PlayerById(nation).Ai);
        Assert.Empty(sim.TeamOf(nation).Wars);
    }

    [Fact]
    public void A_seceding_city_takes_the_land_only_it_held()
    {
        var sim = Game();
        var capital = Capital(sim, 1);
        var city = Found(sim, 1, "Restless");
        var grid = sim.State.Grid;

        int nation = sim.Rules().CityCapture.Secede(sim, city.Id, new());

        foreach (var tile in grid.Range(grid.TileAt(city.Position), 1))
        {
            var plot = sim.State.Plots[tile.Value];
            bool nearerCapital = grid.Distance(grid.HexOf(tile), capital.Position) <= grid.Distance(grid.HexOf(tile), city.Position);
            Assert.Equal(nearerCapital ? 1 : nation, plot.Owner);
            Assert.Equal(50, plot.Culture[plot.Owner!.Value]);
        }
        Assert.Equal(1, sim.State.PlotAt(capital.Position).Owner);
    }
}
