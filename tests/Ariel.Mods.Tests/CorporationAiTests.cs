using Ariel.Ai;
using Ariel.Content.Schema;
using Ariel.Game;
using Ariel.Mods.Corporations;
using Ariel.Content;
using Ariel.Modding;
using Xunit;
using Xunit.Abstractions;
using static Ariel.Mods.Tests.AiBindings;

namespace Ariel.Mods.Tests;

/// <summary>The computer player trains executives in a corporation's headquarters and walks them to foreign cities,
/// as the religion planner does with missionaries.</summary>
public sealed class CorporationAiTests(ITestOutputHelper output)
{
    private const string Cereal = "corporations:cereal_mills", Executive = "military:executive_1";

    /// <summary>Player 0 holds the Cereal Mills headquarters in its second city and a branch-less first city; player 1,
    /// at peace and met, has a city with corn whose surroundings it owns. Every plot is revealed to both teams.</summary>
    private static (Simulation Sim, City Hq, City Own, City Foreign) Staged(bool openBorders = true)
    {
        var sim = P6RulesScenario.Create();
        var own = sim.State.Cities[0]; var hq = sim.State.Cities[1]; var foreign = sim.State.Cities[2];
        foreach (var plot in sim.State.Plots) foreach (var team in sim.State.Teams) plot.Revealed.Add(team.Id);
        foreach (var tile in sim.State.Grid.Range(sim.State.Grid.TileAt(foreign.Position), 2)) sim.State.Plots[tile.Value].Owner = 1;
        foreach (var city in sim.State.Cities) P6RulesScenario.Resource(sim, city, "world:corn");
        // Income must not be negative, as spreading requires: the first city is the capital, which cuts distance
        // maintenance, federal reserves' ten gold each pay the rest and new branches, and taxes take all commerce.
        own.Buildings.Add("cities:palace"); own.Buildings.Add("cities:federal_reserve"); hq.Buildings.Add("cities:federal_reserve");
        P6RulesScenario.Apply(sim, new SetCommerceRatesCommand { Player = 0, Rates = new(100, 0, 0, 0) });
        var def = sim.Content.Get<CorporationDef>(Cereal);
        sim.TeamOf(0).Techs.Add(def.FoundingTech!);
        GameFactory.FoundCorporation(sim, hq, Cereal);
        Assert.Equal(hq.Id, sim.Service<ICorporationService>()!.Headquarters(sim, Cereal));
        if (openBorders)
        {
            foreach (var tech in sim.Content.Get<TechDef>().All.Where(t => t.OpenBordersTrading)) sim.TeamOf(0).Techs.Add(tech.Id);
            P6RulesScenario.Apply(sim, new SignOpenBordersCommand { Player = 0, Target = 1 });
        }
        return (sim, hq, own, foreign);
    }

    private static long NetGold(Simulation sim) => sim.Rules().Economy.Calculate(sim, 0).NetGold;
    private static AiContext Ai(Simulation sim) => (AiContext)NewAi(sim, "AiContext", sim, 0);
    private static IProductionWishes Wishes(Simulation sim) =>
        sim.Content.Services.Get<IProductionWishes>().Single(w => w.Id == "corporations:executives");
    private static IAiPlanner Planner(Simulation sim) => sim.Content.Services.Get<IAiPlanner>().Single(p => p.Id == "corporations:planner");
    private static bool WishesExecutive(Simulation sim, City city) => Wishes(sim).Wishes(Ai(sim), city).Any(w => w.ContentId == Executive);

    private static void Spawn(Simulation sim, City city, string type)
    {
        var unit = new Unit { Id = sim.State.NextUnitId++, Owner = city.Owner, Type = type, Position = city.Position };
        sim.State.AddUnit(unit); sim.Rules().Units.Initialize(sim, unit.Id);
    }

    [Fact]
    public void A_computer_player_holding_a_headquarters_queues_an_executive_there()
    {
        var (sim, hq, own, _) = Staged();
        // Only the headquarters trains executives, so only it is asked for one.
        Assert.True(WishesExecutive(sim, hq));
        Assert.False(WishesExecutive(sim, own));
        var wish = Wishes(sim).Wishes(Ai(sim), hq).Single(w => w.ContentId == Executive);
        // Rated as the city planner rates a building's yield: one more branch's headquarters income, over cost plus 60.
        Assert.Equal(PlannerPoints.Commerce(sim.Content.Get<CorporationDef>(Cereal).HeadquartersCommerce) * 1000L
            / (sim.Content.Get<UnitDef>(Executive).Cost + 60), wish.Score);

        // The city planner ranks it with its own options: what it chooses before is granted as though built, until the
        // headquarters queues the executive.
        var chosen = new List<string>();
        for (int round = 0; round < 40 && !chosen.Contains(Executive); round++)
        {
            foreach (var city in sim.State.Cities.Where(c => c.Owner == 0)) city.ProductionQueue.Clear();
            var ctx = Ai(sim);
            Call(NewAi(sim, "CityPlanner", ctx, NewAi(sim, "SitePlanner", ctx), NewAi(sim, "ArmyPlan", ctx)), "Plan");
            Assert.DoesNotContain(own.ProductionQueue, o => o.ContentId == Executive);
            var head = hq.ProductionQueue[0];
            chosen.Add(head.ContentId);
            if (head.ContentId == Executive) break;
            if (head.Kind == "cities:building") hq.Buildings.Add(head.ContentId);
            else if (head.Kind == "cities:unit") Spawn(sim, hq, head.ContentId);
            else break;
        }
        Assert.True(chosen.Contains(Executive), "the headquarters queues an executive: " + string.Join(", ", chosen));

        // One at a time: with that one queued, no city wishes for another, nor while it is alive.
        Assert.False(WishesExecutive(sim, hq));
        hq.ProductionQueue.Clear();
        Spawn(sim, hq, Executive);
        Assert.False(WishesExecutive(sim, hq));
    }

    [Fact]
    public void An_executive_walks_to_a_foreign_city_and_opens_a_branch_there()
    {
        var (sim, hq, own, foreign) = Staged();
        // The player's other city lacks the resource, so nothing is paid for at home and the executive goes abroad.
        sim.State.PlotAt(own.Position).Bonus = null;
        Spawn(sim, hq, Executive);
        var executive = sim.State.Units.Single(u => u.Type == Executive);
        int moves = executive.MovesLeft;
        long treasury = sim.State.PlayerById(0).Treasury;
        var corporations = sim.Service<ICorporationService>()!;
        var path = new List<Hex> { executive.Position };
        for (int turn = 0; turn < 12 && !corporations.Present(sim, foreign.Id).Contains(Cereal); turn++)
        {
            Planner(sim).Plan(Ai(sim));
            if (sim.State.FindUnit(executive.Id) is not { } alive) break;
            path.Add(alive.Position);
            // A new turn: the executive's moves come back.
            alive.MovesLeft = moves;
        }
        Assert.True(path.Count > 1 && path[1] != hq.Position, "the executive left the headquarters: " + string.Join(" ", path));
        Assert.Contains(Cereal, corporations.Present(sim, foreign.Id));
        Assert.Null(sim.State.FindUnit(executive.Id));
        Assert.Equal(treasury - sim.Content.Get<CorporationDef>(Cereal).SpreadCost, sim.State.PlayerById(0).Treasury);
    }

    [Fact]
    public void No_executive_is_trained_without_a_foreign_city_that_could_take_one_or_while_income_is_negative()
    {
        // Closed borders: the foreign city cannot be reached, and the player's own cities are paid for without one.
        var (closed, closedHq, _, _) = Staged(openBorders: false);
        Assert.False(WishesExecutive(closed, closedHq));

        // A foreign city without the corporation's resource cannot take a branch.
        var (bare, bareHq, _, bareForeign) = Staged();
        Assert.True(WishesExecutive(bare, bareHq));
        bare.State.PlotAt(bareForeign.Position).Bonus = null;
        Assert.False(WishesExecutive(bare, bareHq));

        // Gold: a treasury that would fall under twice the reserve, or an income below zero, stops spreading.
        var (poor, poorHq, _, _) = Staged();
        poor.State.PlayerById(0).Treasury = 2 * Ai(poor).GoldReserve;
        Assert.False(WishesExecutive(poor, poorHq));

        var (losing, losingHq, _, _) = Staged();
        for (int i = 0; i < 60 && NetGold(losing) >= 0; i++) Spawn(losing, losingHq, "military:warrior");
        Assert.True(NetGold(losing) < 0, "unit upkeep puts the player's income below zero");
        Assert.False(WishesExecutive(losing, losingHq));
    }

    private static readonly Lazy<ContentDb> WithCorporations = new(() => ModLoader.Build(new LoadedMods().Sources
        .Append(ModLoader.FromDirectory(Path.Combine(AppContext.BaseDirectory, "optional-mods", "Corporations")))));

    private const int FoundingTurn = 100;

    /// <summary>Four computer players on a small world from the ancient era. At <see cref="FoundingTurn"/>, long before
    /// they could research a corporation, Cereal Mills is staged for player 0 (see <see cref="FoundForThePlayer"/>);
    /// then they play on. Returns the turn hashes and how many executives were trained, and branches opened by
    /// executives and in all.</summary>
    private static (List<ulong> Hashes, int Executives, int ExecutiveSpreads, int Branches) StagedGame(int turns)
    {
        var sim = new Simulation(WithCorporations.Value, GameFactory.Settings(23, "peoples:leader_gjergj_kastrioti",
            "peoples:leader_julius_caesar", "peoples:leader_bismarck", "peoples:leader_gandhi") with
        {
            WorldSize = "foundation:worldsize_small", PlayerAis = Enumerable.Repeat<string?>("ai:standard", 4).ToArray(),
        });
        var hashes = new List<ulong>();
        var executives = new HashSet<int>();
        int executiveSpreads = 0, branches = 0;
        for (int turn = 0; turn < turns; turn++)
        {
            if (turn == FoundingTurn) FoundForThePlayer(sim);
            sim.AdvanceTurn();
            hashes.Add(sim.ComputeHash());
            branches += sim.Events.PreviousTurn.OfType<CorporationSpread>().Count();
            executiveSpreads += sim.Events.PreviousTurn.OfType<MissionResolved>().Count(m => m.Mission == CorporationMissions.Spread && m.Success);
            foreach (var unit in sim.State.Units.Where(u => sim.Content.Attachment<UnitCorporationsDef>(u.Type)?.CorporationSpreads.Count > 0))
                executives.Add(unit.Id);
        }
        return (hashes, executives.Count, executiveSpreads, branches);
    }

    /// <summary>Corn under every city, with the techniques to use it, and Cereal Mills founded in player 0's city with the
    /// most foreign cities in an executive's reach, as completing its headquarters would; the corporation's own
    /// technology is not needed to spread it. Open borders are signed wherever they may be.</summary>
    private static void FoundForThePlayer(Simulation sim)
    {
        var corn = sim.Content.Get<BonusDef>("world:corn");
        foreach (var team in sim.State.Teams) foreach (var tech in new[] { corn.RevealTech, corn.TradeTech }.OfType<string>()) team.Techs.Add(tech);
        foreach (var city in sim.State.Cities) sim.State.PlotAt(city.Position).Bonus = corn.Id;
        int Reach(City city) => sim.State.Cities.Count(c => c.Owner != city.Owner && PlayerRules.IsNation(sim, c.Owner)
            && sim.State.PlotAt(c.Position).AreaId == sim.State.PlotAt(city.Position).AreaId
            && sim.State.Grid.Distance(c.Position, city.Position) <= 10);
        var hq = sim.State.Cities.Where(c => c.Owner == 0).OrderByDescending(Reach).ThenByDescending(c => c.Population).ThenBy(c => c.Id).First();
        GameFactory.FoundCorporation(sim, hq, Cereal);
        // The economy planner keeps the treasury near its reserve, under the spread cost plus twice the reserve that
        // spreading leaves, so the founder is also given the savings a corporation's owner would have put by.
        sim.State.PlayerById(0).Treasury += 2000;
        foreach (var other in sim.State.Players.Where(p => p.Id != 0 && PlayerRules.IsNation(sim, p.Id)))
            sim.TryApply(new SignOpenBordersCommand { Player = 0, Target = other.Id }, out _);
    }

    [Fact]
    [Trait("Category", "E2E")]
    public void A_game_with_a_staged_corporation_plays_on_and_repeats_turn_for_turn()
    {
        // What this shows is that the corporation planner and the executive wishes run in a whole game without failing
        // or adding nondeterminism. It cannot show executives at work: the economy planner runs a deficit once the
        // treasury is large and otherwise holds it near the reserve, and spreading waits for a non-negative income and
        // the spread cost over twice the reserve (CorporationPlanner.Affordable), so after a branch or two nothing more
        // is spread and no executive is asked for. CorporationAiTests' staged tests cover the executives themselves.
        const int Turns = FoundingTurn + 40;
        var game = StagedGame(Turns);
        output.WriteLine($"{Turns} turns, Cereal Mills staged at turn {FoundingTurn}: executives trained {game.Executives}, "
            + $"branches opened by executives {game.ExecutiveSpreads}, branches in all {game.Branches}");
        Assert.True(game.Branches > 0, "the founder pays for a branch in one of its own cities");
        Assert.Equal(game.Hashes, StagedGame(Turns).Hashes);
    }
}
