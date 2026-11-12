using Ariel.Content;
using Ariel.Game;
using Ariel.Mods.Diplomacy;
using Ariel.Modding;
using Xunit;
using Xunit.Abstractions;
using static Ariel.Mods.Tests.AiBindings;
using TechDef = Ariel.Content.Schema.TechDef;
using Ariel.Mods.Corporations;

namespace Ariel.Mods.Tests;

/// <summary>Deals, espionage focus, corporations and attitude in the computer player.</summary>
public sealed class AiDiplomacyTests(ITestOutputHelper output)
{
    private static readonly Lazy<ContentDb> Content = new(() =>
    {
        var mods = new LoadedMods();
        var optional = new[] { "Diplomacy", "Espionage", "Corporations" }
            .Select(name => ModLoader.FromDirectory(Path.Combine(AppContext.BaseDirectory, "optional-mods", name)));
        return ModLoader.Build(mods.Sources.Concat(optional));
    });

    [Fact]
    public void A_gift_is_accepted_and_a_one_sided_demand_is_refused()
    {
        var sim = Scenario();
        var diplomacy = Diplomacy(sim);
        Assert.True(sim.TryApply(new ProposeDealCommand { Player = 1, Target = 0, Items = [new(1, TradeItemKind.Gold, Amount: 50)] }, out var error), error);
        Assert.True(sim.TryApply(new ProposeDealCommand { Player = 2, Target = 0, Items = [new(0, TradeItemKind.Gold, Amount: 80)] }, out error), error);

        var ctx = NewAi(sim, "AiContext", sim, 0);
        Call(NewDiplomacyPlanner(sim, ctx), "Plan");

        var deals = diplomacy.Deals(sim).OrderBy(d => d.Id).ToArray();
        Assert.Equal(DiplomaticDealStatus.Completed, deals[0].Status);
        Assert.Equal(DiplomaticDealStatus.Cancelled, deals[1].Status);
        Assert.Equal(150, sim.State.PlayerById(0).Treasury);
    }

    [Fact]
    public void Deal_value_counts_received_items_against_given_ones()
    {
        var sim = Scenario();
        var planner = NewDiplomacyPlanner(sim, NewAi(sim, "AiContext", sim, 0));
        long Value(params DiplomaticTradeItem[] items) => (long)Call(planner, "Value", items.ToList())!;
        Assert.Equal(40, Value(new DiplomaticTradeItem(1, TradeItemKind.Gold, Amount: 40)));
        Assert.Equal(-40, Value(new DiplomaticTradeItem(0, TradeItemKind.Gold, Amount: 40)));
        Assert.Equal(30, Value(new DiplomaticTradeItem(1, TradeItemKind.GoldPerTurn, Amount: 3)));
        Assert.Equal(0, Value(new DiplomaticTradeItem(1, TradeItemKind.Gold, Amount: 10), new DiplomaticTradeItem(0, TradeItemKind.Gold, Amount: 10)));
    }

    [Fact]
    public void Friendlier_leaders_accept_thinner_deals()
    {
        var sim = Scenario();
        var margin = DiplomacyPlannerType(sim).GetMethod("Margin")!;
        long Margin(Disposition tier) => (long)margin.Invoke(null, [tier])!;
        Assert.True(Margin(Disposition.Annoyed) > Margin(Disposition.Cautious));
        Assert.True(Margin(Disposition.Cautious) > Margin(Disposition.Pleased));
        Assert.True(Margin(Disposition.Pleased) > Margin(Disposition.Friendly));
        Assert.Equal(long.MaxValue, Margin(Disposition.Furious));
    }

    [Fact]
    public void Espionage_focuses_on_an_enemy_before_a_nearer_rival()
    {
        var sim = Scenario();
        var ctx = NewAi(sim, "AiContext", sim, 0);
        var espionage = NewAi(sim, "EspionagePlanner", ctx);
        // Player 1's city is nearer than player 2's.
        Assert.Equal(sim.TeamOf(1).Id, (int?)Call(espionage, "FocusTeam"));
        sim.State.SetWar(sim.TeamOf(0).Id, sim.TeamOf(2).Id, true);
        Call(ctx, "Refresh");
        Assert.Equal(sim.TeamOf(2).Id, (int?)Call(espionage, "FocusTeam"));
    }

    [Fact]
    public void Corporation_worth_subtracts_maintenance()
    {
        var sim = Scenario();
        // The Corporations mod contributes its planner to the AI; it lives beside the service it plays.
        var planner = sim.Service<ICorporationService>()!.GetType().Assembly.GetType("Ariel.Mods.Corporations.CorporationPlanner")!;
        var worth = planner.GetMethod("Worth")!;
        var rich = new CorporationDef { Id = "test:rich", CommerceProduced = new(4, 0, 0, 0) };
        var costly = new CorporationDef { Id = "test:costly", CommerceProduced = new(1, 0, 0, 0), MaintenanceHundredths = 400 };
        Assert.Equal(20L, (long)worth.Invoke(null, [rich])!);
        Assert.True((long)worth.Invoke(null, [costly])! < 0);
    }

    [Fact]
    public void A_defensive_pact_partner_is_never_a_war_target()
    {
        var sim = Scenario();
        foreach (var team in sim.State.Teams)
            foreach (var tech in sim.Content.Get<TechDef>().All.Where(t => t.DefensivePactTrading)) team.Techs.Add(tech.Id);
        Assert.True(sim.TryApply(new SignDefensivePactCommand { Player = 0, Target = 1 }, out var error), error);
        var ctx = NewAi(sim, "AiContext", sim, 0);
        var war = NewAi(sim, "WarPlanner", ctx, NewAi(sim, "ArmyPlan", ctx));
        Assert.Null(Call(war, "AttitudeNeed", 1));
        Assert.NotNull(Call(war, "AttitudeNeed", 2));
    }

    [Fact]
    [Trait("Category", "E2E")]
    public void Computer_players_run_150_turns_with_diplomacy_espionage_and_corporations_loaded()
    {
        var sim = new Simulation(Content.Value, GameFactory.Settings(23, "peoples:leader_gjergj_kastrioti",
            "peoples:leader_julius_caesar", "peoples:leader_bismarck", "peoples:leader_gandhi") with
        {
            WorldSize = "foundation:worldsize_small", PlayerAis = Enumerable.Repeat<string?>("ai:standard", 4).ToArray(),
        });
        int missions = 0, corporations = 0, blockades = 0;
        for (int turn = 0; turn < 150; turn++)
        {
            sim.AdvanceTurn();
            missions += sim.Events.PreviousTurn.OfType<EspionageMissionResult>().Count();
            corporations += sim.Events.PreviousTurn.OfType<CorporationFounded>().Count();
            blockades += sim.State.Units.Count(u => u.Extensions.Get<MilitaryUnitState>(sim.Content)?.Activity == UnitActivity.Blockade);
        }
        var deals = Diplomacy(sim).Deals(sim);
        output.WriteLine($"Deals: {deals.Count} ({deals.Count(d => d.Status is DiplomaticDealStatus.Active or DiplomaticDealStatus.Completed)} accepted), "
            + $"spy missions: {missions}, corporations founded: {corporations}, blockade unit-turns: {blockades}, "
            + $"open borders: {sim.State.Teams.Sum(a => sim.State.Teams.Count(b => a.Id < b.Id && Diplomacy(sim).HasOpenBorders(sim, a.Id, b.Id)))}");
        Assert.True(sim.State.Cities.Count(c => c.Owner is >= 0 and < 4) >= 4);
        // Offers to computer players are answered on the recipient's next turn, and each player proposes at most one
        // swap per turn, so no more than one unanswered offer per player can be waiting.
        Assert.True(Diplomacy(sim).Deals(sim).Count(d => d.Status == DiplomaticDealStatus.Proposed
            && sim.State.PlayerById(d.Recipient).Ai is not null) <= sim.State.Players.Count);
    }

    private static IDiplomacyService Diplomacy(Simulation sim) => sim.Service<IDiplomacyService>()!;

    /// <summary>The Diplomacy mod contributes its planner to the AI; it lives beside the service it plays.</summary>
    private static Type DiplomacyPlannerType(Simulation sim) =>
        Diplomacy(sim).GetType().Assembly.GetType("Ariel.Mods.Diplomacy.DiplomacyPlanner", throwOnError: true)!;

    private static object NewDiplomacyPlanner(Simulation sim, object ctx) => Activator.CreateInstance(DiplomacyPlannerType(sim),
        System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic,
        binder: null, [ctx], culture: null)!;

    /// <summary>Three civilizations that have met, with gold trading unlocked and a city each: player 1 at five tiles
    /// from player 0's city, player 2 at nine.</summary>
    private Simulation Scenario()
    {
        var sim = Simulation.CreateScenario(Content.Value, GameFactory.Settings(71, "peoples:leader_gjergj_kastrioti", "peoples:leader_julius_caesar", "peoples:leader_bismarck"), new(24, 10, true),
            Enumerable.Range(0, 240).Select(_ => new Plot { Terrain = "world:grass" }).ToArray(), game =>
            {
                foreach (var plot in game.State.Plots) foreach (var team in game.State.Teams) plot.Revealed.Add(team.Id);
            });
        GameFactory.NeutralPolitics(sim);
        foreach (var player in sim.State.Players) player.Treasury = 100;
        foreach (var team in sim.State.Teams)
            foreach (var tech in sim.Content.Get<TechDef>().All.Where(t => t.GoldTrading)) team.Techs.Add(tech.Id);
        AddCity(sim, 0, 4, 4);
        AddCity(sim, 1, 9, 4);
        AddCity(sim, 2, 13, 4);
        var diplomacy = Diplomacy(sim);
        foreach (var (a, b) in new[] { (0, 1), (0, 2), (1, 2) })
            sim.Rules().Relations.EstablishContact(sim, sim.TeamOf(a).Id, sim.TeamOf(b).Id);
        return sim;
    }

    private static void AddCity(Simulation sim, int owner, int column, int row)
    {
        var city = new City { Id = sim.State.NextCityId++, Owner = owner, Position = HexGrid.FromOffset(column, row), Population = 3 };
        sim.State.AddCity(city);
        sim.State.PlotAt(city.Position).Owner = owner;
    }
}
