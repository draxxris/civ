using Ariel.Content;
using Ariel.Game;
using Ariel.Modding;
using Ariel.Mods.Diplomacy;
using Xunit;
using static Ariel.Mods.Tests.AiBindings;
using TechDef = Ariel.Content.Schema.TechDef;

namespace Ariel.Mods.Tests;

/// <summary>Embargoes, peace treaties, demands, requests and counter-offers: the deal rules and the computer player's
/// answers to them.</summary>
public sealed class NegotiationTests
{
    private static readonly Lazy<ContentDb> Content = new(() =>
    {
        var mods = new LoadedMods();
        var diplomacy = ModLoader.FromDirectory(Path.Combine(AppContext.BaseDirectory, "optional-mods", "Diplomacy"));
        return ModLoader.Build(mods.Sources.Append(diplomacy));
    });

    [Fact]
    public void An_embargo_ends_trade_between_the_giver_and_its_target_and_is_remembered()
    {
        var sim = Scenario();
        Apply(sim, new ProposeDealCommand { Player = 1, Target = 2, Items = [new(1, TradeItemKind.Gold, Amount: 10), new(2, TradeItemKind.Gold, Amount: 5)] });
        int earlier = Diplomacy(sim).Deals(sim).Single().Id;

        Apply(sim, new ProposeDealCommand { Player = 0, Target = 1, Items = [new(1, TradeItemKind.Embargo, Against: 2)] });
        Apply(sim, new ExecuteDealCommand { Player = 1, Deal = Latest(sim).Id });

        int team1 = sim.TeamOf(1).Id, team2 = sim.TeamOf(2).Id;
        Assert.True(Diplomacy(sim).HasEmbargo(sim, team1, team2));
        Assert.False(sim.Rules().Relations.MayTrade(sim, team1, team2));
        Assert.True(sim.Rules().Relations.MayTrade(sim, sim.TeamOf(0).Id, team2));
        Assert.Equal(DiplomaticDealStatus.Cancelled, Diplomacy(sim).Deals(sim).Single(d => d.Id == earlier).Status);
        Assert.Contains("embargo", Reject(sim, new ProposeDealCommand { Player = 2, Target = 1, Items = [new(2, TradeItemKind.Gold, Amount: 5)] }));
        Assert.Equal(1, Memory(sim, 2, 1, DiplomacyMemories.StoppedTrading));
        Assert.Equal(1, Memory(sim, 2, 0, DiplomacyMemories.HiredTradeEmbargo));
        Assert.Equal(1, Memory(sim, 0, 1, DiplomacyMemories.AcceptedStopTrading));
    }

    [Fact]
    public void An_embargo_names_a_third_team_the_giver_has_met()
    {
        var sim = Scenario();
        Assert.Contains("third team", Reject(sim, new ProposeDealCommand { Player = 0, Target = 1, Items = [new(1, TradeItemKind.Embargo, Against: 0)] }));
        Assert.Contains("only an embargo", Reject(sim, new ProposeDealCommand { Player = 0, Target = 1, Items = [new(1, TradeItemKind.Gold, Amount: 5, Against: 2)] }));
    }

    [Fact]
    public void A_deal_between_enemies_needs_a_peace_treaty_and_the_treaty_ends_the_war()
    {
        var sim = Scenario();
        Apply(sim, new DeclareWarCommand { Player = 0, Target = 1 });
        Assert.Contains("at war", Reject(sim, new ProposeDealCommand { Player = 0, Target = 1, Items = [new(1, TradeItemKind.Gold, Amount: 20)] }));
        List<DiplomaticTradeItem> peace = [new(0, TradeItemKind.PeaceTreaty), new(1, TradeItemKind.Gold, Amount: 20)];
        Assert.Contains("too recent", Reject(sim, new ProposeDealCommand { Player = 0, Target = 1, Items = peace }));

        sim.State.Turn += RelationRules.MinimumWarTurns;
        Assert.Contains("after peace", Reject(sim, new ProposeDealCommand { Player = 0, Target = 1,
            Items = [new(0, TradeItemKind.PeaceTreaty), new(0, TradeItemKind.OpenBorders)] }));
        Apply(sim, new ProposeDealCommand { Player = 0, Target = 1, Items = peace });
        Apply(sim, new ExecuteDealCommand { Player = 1, Deal = Latest(sim).Id });
        Assert.DoesNotContain(sim.TeamOf(1).Id, sim.TeamOf(0).Wars);
        Assert.Equal(120, sim.State.PlayerById(0).Treasury);
        Assert.Contains("not at war", Reject(sim, new ProposeDealCommand { Player = 0, Target = 1, Items = [new(0, TradeItemKind.PeaceTreaty)] }));
    }

    [Fact]
    public void Treaties_in_a_deal_bind_both_sides()
    {
        var sim = Scenario();
        foreach (var team in sim.State.Teams) team.Techs.UnionWith(sim.Content.Get<TechDef>().All.Where(t => t.OpenBordersTrading).Select(t => t.Id));
        Apply(sim, new ProposeDealCommand { Player = 1, Target = 0, Items = [new(1, TradeItemKind.OpenBorders)] });
        Apply(sim, new ExecuteDealCommand { Player = 0, Deal = Latest(sim).Id });
        Assert.True(Diplomacy(sim).HasOpenBorders(sim, sim.TeamOf(0).Id, sim.TeamOf(1).Id));
    }

    [Fact]
    public void A_refused_demand_and_a_refused_request_are_remembered()
    {
        var sim = Scenario();
        Assert.Contains("only for what the other side gives", Reject(sim, new ProposeDealCommand { Player = 0, Target = 1,
            Items = [new(0, TradeItemKind.Gold, Amount: 10)], Intent = DealIntent.Demand }));

        Apply(sim, new ProposeDealCommand { Player = 0, Target = 1, Items = [new(1, TradeItemKind.Gold, Amount: 30)], Intent = DealIntent.Demand });
        Apply(sim, new CancelDealCommand { Player = 1, Deal = Latest(sim).Id });
        Assert.Equal(1, Memory(sim, 0, 1, DiplomacyMemories.RejectedDemand));
        Assert.Equal(1, Memory(sim, 1, 0, DiplomacyMemories.MadeDemand));

        Apply(sim, new ProposeDealCommand { Player = 2, Target = 1, Items = [new(1, TradeItemKind.Gold, Amount: 30)], Intent = DealIntent.Request });
        Apply(sim, new CancelDealCommand { Player = 1, Deal = Latest(sim).Id });
        Assert.Equal(1, Memory(sim, 2, 1, DiplomacyMemories.RefusedHelp));

        // Withdrawing one's own demand is not a refusal.
        Apply(sim, new ProposeDealCommand { Player = 2, Target = 0, Items = [new(0, TradeItemKind.Gold, Amount: 30)], Intent = DealIntent.Demand });
        Apply(sim, new CancelDealCommand { Player = 2, Deal = Latest(sim).Id });
        Assert.Equal(0, Memory(sim, 2, 0, DiplomacyMemories.RejectedDemand));
    }

    [Fact]
    public void A_demand_given_in_to_is_tribute_not_help()
    {
        var sim = Scenario();
        Apply(sim, new ProposeDealCommand { Player = 0, Target = 1, Items = [new(1, TradeItemKind.Gold, Amount: 30)], Intent = DealIntent.Demand });
        Apply(sim, new ExecuteDealCommand { Player = 1, Deal = Latest(sim).Id });
        Assert.Equal(1, Memory(sim, 0, 1, DiplomacyMemories.AcceptDemand));
        Assert.Equal(1, Memory(sim, 1, 0, DiplomacyMemories.MadeDemand));
        Assert.Equal(0, Memory(sim, 0, 1, DiplomacyMemories.GiveHelp));
    }

    [Fact]
    public void The_computer_player_gives_in_only_to_a_much_stronger_demander()
    {
        var sim = Scenario();
        Apply(sim, new ProposeDealCommand { Player = 0, Target = 1, Items = [new(1, TradeItemKind.Gold, Amount: 30)], Intent = DealIntent.Demand });
        Plan(sim, 1);
        Assert.Equal(DiplomaticDealStatus.Cancelled, Latest(sim).Status);

        for (int i = 0; i < 6; i++) AddUnit(sim, 0, "military:swordsman", 4, 4);
        Apply(sim, new ProposeDealCommand { Player = 0, Target = 1, Items = [new(1, TradeItemKind.Gold, Amount: 30)], Intent = DealIntent.Demand });
        Plan(sim, 1);
        Assert.Equal(DiplomaticDealStatus.Completed, Latest(sim).Status);
        Assert.Equal(130, sim.State.PlayerById(0).Treasury);
    }

    [Fact]
    public void A_short_offer_is_answered_with_a_counter_offer_that_asks_for_more_gold()
    {
        var sim = Scenario();
        sim.State.PlayerById(0).Treasury = 2000;
        var tech = sim.Content.Get<TechDef>().All.Where(t => !t.Disable && !t.Repeat && t.Cost > 0 && !sim.TeamOf(0).Techs.Contains(t.Id))
            .OrderBy(t => t.Cost).ThenBy(t => t.Id, StringComparer.Ordinal).First();
        sim.TeamOf(1).Techs.Add(tech.Id);
        Apply(sim, new ProposeDealCommand { Player = 0, Target = 1, Items = [new(1, TradeItemKind.Technology, tech.Id), new(0, TradeItemKind.Gold, Amount: 10)] });
        int offer = Latest(sim).Id;

        Plan(sim, 1);
        var counter = Latest(sim);
        Assert.Equal(DiplomaticDealStatus.Cancelled, Diplomacy(sim).Deals(sim).Single(d => d.Id == offer).Status);
        Assert.Equal(DiplomaticDealStatus.Proposed, counter.Status);
        Assert.Equal(offer, counter.CounterTo);
        Assert.Equal((1, 0), (counter.Proposer, counter.Recipient));
        Assert.True(counter.Items.Single(i => i.Kind == TradeItemKind.Gold).Amount > 10);
        Assert.Contains(counter.Items, i => i.Kind == TradeItemKind.Technology && i.ContentId == tech.Id);

        // A counter-offer refused by its recipient is not countered again.
        Apply(sim, new CancelDealCommand { Player = 0, Deal = counter.Id });
        Assert.Equal(counter.Id, Latest(sim).Id);
    }

    [Fact]
    public void A_winning_computer_player_offers_peace_for_tribute_and_a_losing_one_pays_it()
    {
        var sim = Scenario();
        Apply(sim, new DeclareWarCommand { Player = 0, Target = 1 });
        sim.State.Turn += RelationRules.MinimumWarTurns;
        SetWarScore(sim, sim.TeamOf(0).Id, sim.TeamOf(1).Id, 300);
        for (int i = 0; i < 6; i++) AddUnit(sim, 0, "military:swordsman", 4, 4);
        // Reviews fall on turns where turn plus player is a multiple of the interval.
        while ((sim.State.Turn + 0) % 5 != 0) sim.State.Turn++;

        Plan(sim, 0);
        var offer = Latest(sim);
        Assert.Equal((0, 1), (offer.Proposer, offer.Recipient));
        Assert.Contains(offer.Items, i => i.Kind == TradeItemKind.PeaceTreaty);
        Assert.Contains(offer.Items, i => i.FromPlayer == 1 && i.Kind is TradeItemKind.Gold or TradeItemKind.Technology);

        Plan(sim, 1);
        Assert.Equal(DiplomaticDealStatus.Completed, Diplomacy(sim).Deals(sim).Single(d => d.Id == offer.Id).Status);
        Assert.DoesNotContain(sim.TeamOf(1).Id, sim.TeamOf(0).Wars);
    }

    [Fact]
    public void Leader_thresholds_refuse_at_or_below_the_named_attitude()
    {
        var sim = Scenario();
        var atOrBelow = DiplomacyPlannerType(sim).GetMethod("AtOrBelow", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic)!;
        bool Refuses(Disposition tier, string threshold) => (bool)atOrBelow.Invoke(null, [tier, threshold])!;
        Assert.True(Refuses(Disposition.Annoyed, "cautious"));
        Assert.True(Refuses(Disposition.Cautious, "cautious"));
        Assert.False(Refuses(Disposition.Pleased, "cautious"));
        Assert.False(Refuses(Disposition.Furious, "none"));
        Assert.False(Refuses(Disposition.Furious, ""));
    }

    private static IDiplomacyService Diplomacy(Simulation sim) => sim.Service<IDiplomacyService>()!;
    private static DiplomaticDeal Latest(Simulation sim) => Diplomacy(sim).Deals(sim).OrderBy(d => d.Id).Last();
    private static int Memory(Simulation sim, int from, int toward, string kind) =>
        sim.Rules().Relations.Memories(sim, from, toward).GetValueOrDefault(kind);

    private static void Apply(Simulation sim, GameCommand command) => Assert.True(sim.TryApply(command, out var error), error);
    private static string Reject(Simulation sim, GameCommand command)
    {
        Assert.False(sim.TryApply(command, out var error));
        return error!;
    }

    private static Type DiplomacyPlannerType(Simulation sim) =>
        Diplomacy(sim).GetType().Assembly.GetType("Ariel.Mods.Diplomacy.DiplomacyPlanner", throwOnError: true)!;

    private static void Plan(Simulation sim, int player)
    {
        var ctx = NewAi(sim, "AiContext", sim, player);
        var planner = Activator.CreateInstance(DiplomacyPlannerType(sim),
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic,
            binder: null, [ctx], culture: null)!;
        Call(planner, "Plan");
    }

    private static void SetWarScore(Simulation sim, int team, int enemy, int score)
    {
        foreach (var (a, b, points) in new[] { (team, enemy, score), (enemy, team, -score) })
        {
            var type = AiType(sim, "AiTeamState");
            var state = Activator.CreateInstance(type)!;
            ((SortedDictionary<int, int>)type.GetProperty("WarScore")!.GetValue(state)!)[b] = points;
            var extensions = sim.State.TeamById(a).Extensions;
            extensions.GetType().GetMethod("Set")!.MakeGenericMethod(type).Invoke(extensions, [sim.Content, state]);
        }
    }

    /// <summary>Three civilizations without leaders (so no personality thresholds) that have met, with gold, technology
    /// and agreement trading unlocked and a city each.</summary>
    private static Simulation Scenario()
    {
        var sim = Simulation.CreateScenario(Content.Value, GameFactory.Settings(71, "peoples:leader_gjergj_kastrioti", "peoples:leader_julius_caesar", "peoples:leader_bismarck"), new(24, 10, true),
            Enumerable.Range(0, 240).Select(_ => new Plot { Terrain = "world:grass" }).ToArray(), game =>
            {
                foreach (var plot in game.State.Plots) foreach (var team in game.State.Teams) plot.Revealed.Add(team.Id);
            });
        GameFactory.NeutralPolitics(sim);
        foreach (var player in sim.State.Players) player.Treasury = 100;
        foreach (var team in sim.State.Teams)
            foreach (var tech in sim.Content.Get<TechDef>().All.Where(t => t.GoldTrading || t.TechTrading)) team.Techs.Add(tech.Id);
        AddCity(sim, 0, 4, 4);
        AddCity(sim, 1, 9, 4);
        AddCity(sim, 2, 13, 4);
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

    private static void AddUnit(Simulation sim, int owner, string type, int column, int row)
    {
        var unit = new Unit { Id = sim.State.NextUnitId++, Owner = owner, Type = type, Position = HexGrid.FromOffset(column, row) };
        sim.State.AddUnit(unit);
        sim.Rules().Units.Initialize(sim, unit.Id);
    }
}
