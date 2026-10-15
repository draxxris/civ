using Civ.Content;
using Civ.Game;
using Civ.Modding;
using Xunit;
using S = Civ.Content.Schema;

namespace Civ.Mods.Tests;

public sealed class DiplomacyRulesTests(LoadedMods mods) : IClassFixture<LoadedMods>
{
    private ContentDb Content()
    {
        var diplomacy = ModLoader.FromDirectory(Path.Combine(AppContext.BaseDirectory, "optional-mods", "Diplomacy"));
        var civic = mods.Content.Get<S.CivicDef>().All.First(c => c.RequiresStateReligion);
        var constants = mods.Content.Get<S.RuleConstantsDef>().All.Single();
        var probe = new ModSource { Root = mods.Root, Manifest = new() { Id = "diplomacy_probe", Version = "1", Dependencies = ["foundation"], References = ["foundation", "government"] }, Instance = new Probe(civic, constants) };
        return ModLoader.Build(mods.Sources.Append(diplomacy).Append(probe));
    }
    private Simulation Scenario()
    {
var sim = GameFactory.Create(Content(), 19, "peoples:leader_gjergj_kastrioti", "peoples:leader_julius_caesar", "peoples:leader_bismarck", "peoples:leader_gandhi");
        sim.State.Players = sim.State.Players.Select(p => new Player { Id = p.Id, TeamId = p.TeamId, Civilization = p.Civilization,
            Leader = p.Id == 0 ? "diplomacy_probe:leader_a" : "diplomacy_probe:leader_b" }).ToList();
        sim.State.Map = new(12, 8, true);
        sim.State.Plots = Enumerable.Range(0, 96).Select(_ => new Plot { Terrain = "world:grass", Route = "world:route_route_road" }).ToArray();
        sim.State.Units.Clear(); sim.State.Cities.Clear();
        foreach (var team in sim.State.Teams) team.Techs.Clear();
        foreach (var player in sim.State.Players) { player.Treasury = 100; player.StartingPlot = null; }
        sim.State.RebuildIndexes();
        return sim;
    }
    private static IDiplomacyService Service(Simulation sim) => sim.Content.Services.Get<IDiplomacyService>().Single();
    private static void Apply(Simulation sim, GameCommand command) => Assert.True(sim.TryApply(command, out var error), error);
    private static void Reject(Simulation sim, GameCommand command, string reason)
    {
        ulong before = sim.ComputeHash();
        Assert.False(sim.TryApply(command, out var error)); Assert.Contains(reason, error!); Assert.Equal(before, sim.ComputeHash());
    }
    private static void Meet(Simulation sim, int a = 0, int b = 1) => Service(sim).EstablishContact(sim, sim.TeamOf(a).Id, sim.TeamOf(b).Id);
    private static City City(Simulation sim, int owner, int tile)
    {
        var city = new City { Id = sim.State.NextCityId++, Owner = owner, Position = sim.State.Grid.HexOf(new(tile)), Population = 5 };
        sim.State.AddCity(city); sim.State.Plots[tile].Owner = owner;
        city.Extensions.Set(sim.Content, new CityRuleState { ManualWork = true, AvoidGrowth = true });
        return city;
    }
    private const string Grain = "diplomacy_probe:grain";
    /// <summary>Foreign, unrevealed columns 0 and 6 split the 12-wide map into two continents.</summary>
    private static void Wall(Simulation sim)
    {
        for (int i = 0; i < sim.State.Plots.Length; i++)
            if (i % 12 is 0 or 6) sim.State.Plots[i].Owner = 1;
    }
    /// <summary>Player 0 offers one copy of a resource to player 1, and player 1 accepts it.</summary>
    private static void Export(Simulation sim, string resource)
    {
        Apply(sim, new ProposeDealCommand { Player = 0, Target = 1, Items = [new(0, TradeItemKind.Resource, resource)] });
        Apply(sim, new ExecuteDealCommand { Player = 1, Deal = Service(sim).Deals(sim).Last().Id });
    }
    [Fact]
    public void Current_sight_and_adjacent_borders_discover_teams_but_exploration_and_barbarians_do_not()
    {
        var sim = Scenario(); var diplomacy = Service(sim);
        sim.State.Teams.Add(new Team { Id = 17, Members = [17] });
        sim.State.Players.Add(new Player { Id = 17, TeamId = 17 });
        sim.State.Teams.Add(new Team { Id = 18, Members = [18] });
        var barbarian = new Player { Id = 18, TeamId = 18 }; barbarian.Extensions.Set(sim.Content, new MilitaryPlayerState { Barbarian = true }); sim.State.Players.Add(barbarian);
        sim.State.RebuildIndexes();
        sim.State.Plots[40].Owner = 17; sim.State.Plots[40].Revealed.Add(0);
        diplomacy.DiscoverContacts(sim); Assert.False(diplomacy.HasMet(sim, 0, 17));
        sim.State.Plots[40].Visible.Add(0); diplomacy.DiscoverContacts(sim);
        Assert.True(diplomacy.HasMet(sim, 0, 17)); Assert.True(diplomacy.HasMet(sim, 17, 0));
        sim.State.Plots[41].Owner = 1; diplomacy.DiscoverContacts(sim); Assert.True(diplomacy.HasMet(sim, 1, 17));
        sim.State.AddUnit(new Unit { Id = sim.State.NextUnitId++, Owner = 18, Type = "military:warrior", Position = sim.State.Grid.HexOf(new(40)) });
        diplomacy.DiscoverContacts(sim); Assert.False(diplomacy.HasMet(sim, 17, 18));
        var restored = SaveGame.Load(sim.Content, SaveGame.Save(sim)); Assert.True(Service(restored).HasMet(restored, 0, 17)); Assert.Equal(sim.ComputeHash(), restored.ComputeHash());
    }
    [Fact]
    public void Personality_religion_shared_favorite_civic_borders_and_war_have_an_explainable_readonly_ledger()
    {
        var sim = Scenario(); Meet(sim);
        string civic = sim.Content.Get<S.LeaderDef>("diplomacy_probe:leader_a").FavoriteCivic!;
        foreach (int p in new[] { 0, 1 })
        {
            sim.State.PlayerById(p).Civics.Clear(); sim.State.PlayerById(p).Civics["religion"] = civic;
            sim.State.PlayerById(p).Extensions.Set(sim.Content, new ReligionPlayerState { LastStateReligion = "religion:buddhism" });
        }
        sim.State.Plots[40].Owner = 0; sim.State.Plots[41].Owner = 1;
        ulong hash = sim.ComputeHash(); var result = Service(sim).GetAttitude(sim, 0, 1);
        long Source(string id) => result.Ledger.Lines.Where(l => l.Source == "diplomacy:" + id).Sum(l => l.Amount);
        Assert.Equal(1, Source("base")); Assert.Equal(2, Source("peace_weight")); Assert.Equal(1, Source("warmonger_respect"));
        Assert.Equal(2, Source("same_religion")); Assert.Equal(2, Source("favorite_civic")); Assert.Equal(-2, Source("close_borders"));
        Assert.Equal(AttitudeTier.Pleased, result.Tier); Assert.Equal(hash, sim.ComputeHash());
        sim.State.PlayerById(1).Extensions.Get<ReligionPlayerState>(sim.Content)!.LastStateReligion = "religion:hinduism";
        Assert.Contains(Service(sim).GetAttitude(sim, 0, 1).Ledger.Lines, l => l.Source == "diplomacy:different_religion" && l.Amount == -3);
        Apply(sim, new DiplomaticDeclareWarCommand { Player = 1, Target = 0 });
        var war = Service(sim).GetAttitude(sim, 0, 1); Assert.Contains(war.Ledger.Lines, l => l.Source == "diplomacy:war_history" && l.Amount == -3);
        Assert.Contains(war.Ledger.Lines, l => l.Source == "diplomacy:war" && l.Amount == -3);
        sim.State.Turn += 12; Apply(sim, new DiplomaticMakePeaceCommand { Player = 0, Target = 1 });
        sim.State.Turn += 10; Assert.Contains(Service(sim).GetAttitude(sim, 0, 1).Ledger.Lines, l => l.Source == "diplomacy:years_of_peace" && l.Amount == 2);
    }
    [Fact]
    public void Open_borders_unlock_foreign_routes_and_unit_passage_but_no_foreign_trade_civics_still_block_routes()
    {
        var sim = Scenario(); var home = City(sim, 0, 40); var foreign = City(sim, 1, 41);
        sim.State.Plots.ToList().ForEach(p => { p.Revealed.Add(0); p.Revealed.Add(1); });
        var trade = sim.Content.Services.Get<ITradeRouteService>().Single();
        Assert.Empty(trade.Calculate(sim, home.Id).Routes);
        Meet(sim); Assert.Empty(trade.Calculate(sim, home.Id).Routes);
        var unit = new Unit { Id = sim.State.NextUnitId++, Owner = 0, Type = "military:warrior", Position = home.Position };
        sim.State.AddUnit(unit); sim.Content.Services.Get<IUnitService>().Single().Initialize(sim, unit.Id);
        var movement = sim.Content.Services.Get<IMovementService>().Single();
        Assert.Equal("Closed borders", movement.CanEnter(sim, unit.Id, new(40), new(41)));
        Apply(sim, new SignOpenBordersCommand { Player = 0, Target = 1 });
        Assert.Null(movement.CanEnter(sim, unit.Id, new(40), new(41))); Assert.Equal(foreign.Id, Assert.Single(trade.Calculate(sim, home.Id).Routes).PartnerCity);
        var mercantilism = sim.Content.Get<S.CivicDef>().All.First(c => c.NoForeignTrade);
        sim.State.PlayerById(1).Civics["economy"] = mercantilism.Id;
        Assert.Empty(trade.Calculate(sim, home.Id).Routes);
    }
    [Fact]
    public void Domestic_resources_transit_open_borders_partner_roads_only_while_open_borders_last()
    {
        var sim = Scenario(); Meet(sim);
        var home = City(sim, 0, 40); City(sim, 0, 43).Extensions.Set(sim.Content, new CityRuleState { ManualWork = true, AvoidGrowth = true });
        for (int i = 0; i < 96; i++) if (i is < 40 or > 43) sim.State.Plots[i].Route = null;
        sim.State.Plots[41].Owner = 1; sim.State.Plots[42].Owner = 1; sim.State.Plots[43].Bonus = Grain;
        sim.State.Plots.ToList().ForEach(p => { p.Revealed.Add(0); p.Revealed.Add(1); });
        var network = sim.Content.Services.Get<IResourceNetwork>().Single();
        Assert.Equal(0, network.ConnectedCount(sim, 0, home.Id, Grain));
        Apply(sim, new SignOpenBordersCommand { Player = 0, Target = 1 });
        Assert.Equal(1, network.ConnectedCount(sim, 0, home.Id, Grain));
        Apply(sim, new DiplomaticDeclareWarCommand { Player = 0, Target = 1 });
        Assert.Equal(0, network.ConnectedCount(sim, 0, home.Id, Grain));
    }
    [Fact]
    public void Open_borders_and_technology_trades_need_an_enabling_technology_once_any_team_has_techs()
    {
        var sim = Scenario(); Meet(sim);
        var content = sim.Content.Get<S.TechDef>().All;
        var other = content.First(t => !t.OpenBordersTrading && !t.TechTrading && !t.GoldTrading);
        var giver = content.First(t => !t.OpenBordersTrading && !t.TechTrading && t.Id != other.Id);
        sim.TeamOf(0).Techs.Add(other.Id); sim.TeamOf(0).Techs.Add(giver.Id);
        Reject(sim, new SignOpenBordersCommand { Player = 0, Target = 1 }, "open borders");
        Reject(sim, new ProposeDealCommand { Player = 0, Target = 1, Items = [new(0, TradeItemKind.Technology, giver.Id)] }, "technology trading");
        sim.TeamOf(1).Techs.Add(content.First(t => t.OpenBordersTrading).Id);
        Apply(sim, new SignOpenBordersCommand { Player = 0, Target = 1 });
        sim.TeamOf(0).Techs.Add(content.First(t => t.TechTrading).Id);
        Apply(sim, new ProposeDealCommand { Player = 0, Target = 1, Items = [new(0, TradeItemKind.Technology, giver.Id)] });
    }
    [Fact]
    public void Defensive_partners_join_against_aggressors_and_offensive_wars_cancel_the_aggressors_pacts()
    {
        var sim = Scenario(); Meet(sim, 0, 1); Meet(sim, 1, 2); Meet(sim, 0, 3);
        Apply(sim, new SignDefensivePactCommand { Player = 1, Target = 2 }); Apply(sim, new SignDefensivePactCommand { Player = 0, Target = 3 });
        Apply(sim, new DiplomaticDeclareWarCommand { Player = 0, Target = 1 });
        Assert.True(sim.TeamOf(2).Wars.Contains(0)); Assert.True(sim.TeamOf(0).Wars.Contains(2)); Assert.False(sim.TeamOf(3).Wars.Contains(1));
        Assert.False(Service(sim).HasDefensivePact(sim, 0, 3));
        Reject(sim, new DiplomaticMakePeaceCommand { Player = 0, Target = 1 }, "recent");
        sim.State.Turn += 12; Apply(sim, new DiplomaticMakePeaceCommand { Player = 0, Target = 1 });
        var aiWar = new CommandCodec(sim.Content).Decode("{\"type\":\"ai:declare_war\",\"data\":{\"player\":0,\"target\":1}}");
        Reject(sim, aiWar, "recent"); Reject(sim, new DiplomaticDeclareWarCommand { Player = 0, Target = 1 }, "recent");
        sim.State.Turn += 10; Apply(sim, aiWar);
    }
    [Fact]
    public void Deals_transfer_assets_reserve_resources_settle_once_and_unlock_cancellation_after_ten_turns()
    {
        var sim = Scenario(); Meet(sim); var home = City(sim, 0, 40); var foreign = City(sim, 1, 41);
        sim.State.Plots[40].Bonus = "diplomacy_probe:grain";
        sim.TeamOf(0).Techs.Add("research:woodworking"); sim.TeamOf(0).Techs.Add("research:currency"); sim.TeamOf(0).Techs.Add("research:alphabet");
        Apply(sim, new ProposeDealCommand { Player = 0, Target = 1, Items = [new(0, TradeItemKind.Resource, "diplomacy_probe:grain"), new(1, TradeItemKind.GoldPerTurn, Amount: 3), new(1, TradeItemKind.Gold, Amount: 7), new(0, TradeItemKind.Technology, "research:woodworking")] });
        int deal = Service(sim).Deals(sim).Last().Id;
        Reject(sim, new ExecuteDealCommand { Player = 0, Deal = deal }, "recipient");
        Apply(sim, new ExecuteDealCommand { Player = 1, Deal = deal }); Assert.Equal(107, sim.State.PlayerById(0).Treasury); Assert.Equal(93, sim.State.PlayerById(1).Treasury);
        Assert.True(sim.KnowsTech(1, "research:woodworking"));
        Assert.Equal(0, Service(sim).AvailableResourceCount(sim, 0, home.Id, "diplomacy_probe:grain")); Assert.Equal(1, Service(sim).AvailableResourceCount(sim, 1, foreign.Id, "diplomacy_probe:grain"));
        Reject(sim, new ProposeDealCommand { Player = 0, Target = 2, Items = [new(0, TradeItemKind.Resource, "diplomacy_probe:grain")] }, "met");
        Meet(sim, 0, 2); Reject(sim, new ProposeDealCommand { Player = 0, Target = 2, Items = [new(0, TradeItemKind.Resource, "diplomacy_probe:grain")] }, "unreserved");
        sim.Content.TurnSystems.Get("diplomacy:deals").Run(sim); sim.Content.TurnSystems.Get("diplomacy:deals").Run(sim);
        Assert.Equal(110, sim.State.PlayerById(0).Treasury); Assert.Equal(90, sim.State.PlayerById(1).Treasury);
        Reject(sim, new CancelDealCommand { Player = 0, Deal = deal }, "ten turns");
        sim.State.Turn += 10; Apply(sim, new CancelDealCommand { Player = 0, Deal = deal }); Assert.Equal(0, Service(sim).ResourceBalance(sim, 1, "diplomacy_probe:grain"));
    }
    [Fact]
    public void War_cancels_binding_deals_and_save_load_resumes_contracts_and_turns_with_equal_hashes()
    {
        var sim = Scenario(); Meet(sim);
        Apply(sim, new SignOpenBordersCommand { Player = 0, Target = 1 });
        Apply(sim, new ProposeDealCommand { Player = 0, Target = 1, Items = [new(0, TradeItemKind.GoldPerTurn, Amount: 2)] });
        int deal = Service(sim).Deals(sim).Last().Id; Apply(sim, new ExecuteDealCommand { Player = 1, Deal = deal });
        var loaded = SaveGame.Load(sim.Content, SaveGame.Save(sim)); Assert.Equal(sim.ComputeHash(), loaded.ComputeHash());
        for (int i = 0; i < 3; i++) { sim.AdvanceTurn(); loaded.AdvanceTurn(); Assert.Equal(sim.ComputeHash(), loaded.ComputeHash()); }
        foreach (var game in new[] { sim, loaded }) Apply(game, new DiplomaticDeclareWarCommand { Player = 0, Target = 1 });
        Assert.Equal(DiplomaticDealStatus.Cancelled, Service(sim).Deals(sim).Last().Status); Assert.False(Service(sim).HasOpenBorders(sim, 0, 1)); Assert.Equal(sim.ComputeHash(), loaded.ComputeHash());
    }
    [Fact]
    public void Gold_per_turn_settles_after_the_turn_income_arrives()
    {
        var sim = Scenario(); Meet(sim);
        Apply(sim, new ProposeDealCommand { Player = 0, Target = 1, Items = [new(0, TradeItemKind.GoldPerTurn, Amount: 3)] });
        Apply(sim, new ExecuteDealCommand { Player = 1, Deal = Service(sim).Deals(sim).Last().Id });
        sim.State.PlayerById(0).Treasury = 0;
        sim.AdvanceTurn();
        Assert.Equal(DiplomaticDealStatus.Active, Service(sim).Deals(sim).Last().Status);
        Assert.Equal(2, sim.State.PlayerById(0).Treasury);
    }
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    public void Incoming_deal_gold_covers_domestic_deficits_without_slider_changes_or_duplicate_credit(int recipient)
    {
        var sim = Scenario(); Meet(sim);
        int giver = 1 - recipient;
        var city = City(sim, recipient, 40);
        for (int i = 0; i < 40; i++)
            sim.State.AddUnit(new Unit { Id = sim.State.NextUnitId++, Owner = recipient, Type = "military:warrior", Position = city.Position });
        var economy = sim.Content.Services.Get<IPlayerEconomyService>().Single();
        var rates = new CommerceVector(0, 100, 0, 0);
        economy.SetRates(sim, recipient, rates);
        var domestic = economy.Calculate(sim, recipient);
        Assert.True(domestic.NetGold < 0);
        Apply(sim, new ProposeDealCommand { Player = giver, Target = recipient,
            Items = [new(giver, TradeItemKind.GoldPerTurn, Amount: 100), new(recipient, TradeItemKind.GoldPerTurn, Amount: 7)] });
        int deal = Service(sim).Deals(sim).Last().Id;
        Apply(sim, new ExecuteDealCommand { Player = recipient, Deal = deal });
        var combined = economy.Calculate(sim, recipient);
        Assert.Equal(domestic.Income + 100, combined.Income);
        Assert.Equal(domestic.Expenses + 7, combined.Expenses);
        Assert.Equal(domestic.NetGold + 93, combined.NetGold);
        Assert.Equal(100, combined.Ledger.Total("economy:deal_income"));
        Assert.Equal(7, combined.Ledger.Total("economy:deal_expenses"));
        Assert.Equal(combined.Expenses, combined.Ledger.Total("economy:expenses"));
        long giverNet = economy.Calculate(sim, giver).NetGold;
        sim.State.PlayerById(recipient).Treasury = 0;
        var loaded = SaveGame.Load(sim.Content, SaveGame.Save(sim));
        sim.AdvanceTurn(); loaded.AdvanceTurn();
        Assert.Equal(sim.ComputeHash(), loaded.ComputeHash());
        Assert.Equal(rates, economy.Rates(sim, recipient));
        Assert.Equal(combined.NetGold + 5, sim.State.PlayerById(recipient).Treasury); // The fixture also earns five gold after Gold.
        Assert.Equal(100 + giverNet + 5, sim.State.PlayerById(giver).Treasury);
        Assert.Equal(0, sim.State.PlayerById(recipient).Extensions.Get<EconomyPlayerState>(sim.Content)!.UnpaidGold);
        Assert.Equal(DiplomaticDealStatus.Active, Service(sim).Deals(sim).Last().Status);
        sim.Content.TurnSystems.Get("diplomacy:deals").Run(sim);
        Assert.Equal(combined.NetGold + 5, sim.State.PlayerById(recipient).Treasury);
        sim.State.SetWar(0, 1, true);
        Assert.Equal(0, economy.Calculate(sim, recipient).Ledger.Total("economy:deal_income"));
        Assert.Equal(0, economy.Calculate(sim, recipient).Ledger.Total("economy:deal_expenses"));
    }
    [Fact]
    public void Outgoing_deal_payments_shift_science_only_as_far_as_needed_before_settlement()
    {
        var sim = P6RulesScenario.Create();
        foreach (var city in sim.State.Cities) city.Extensions.Set(sim.Content, new CityRuleState { ManualWork = true });
        sim.State.Cities[0].Buildings.Add("cities:palace");
        var economy = sim.Content.Services.Get<IPlayerEconomyService>().Single();
        economy.SetRates(sim, 0, new(0, 100, 0, 0));
        var diplomacy = Service(sim);
        int deal = diplomacy.ProposeDeal(sim, 0, 1, [new(0, TradeItemKind.GoldPerTurn, Amount: 3)]);
        Assert.Null(diplomacy.ValidateExecution(sim, 1, deal)); diplomacy.ExecuteDeal(sim, deal);
        var player = sim.State.PlayerById(0); player.Treasury = 0;
        Assert.True(economy.Calculate(sim, 0).NetGold < 0);
        sim.AdvanceTurn();
        var rates = economy.Rates(sim, 0);
        Assert.True(rates.Gold > 0); Assert.Equal(100, rates.Gold + rates.Research);
        Assert.Equal(0, rates.Culture); Assert.Equal(0, rates.Espionage);
        var settled = player.Extensions.Get<EconomyPlayerState>(sim.Content)!;
        Assert.Equal(settled.SettledCommerceHundredths.Gold / 100 - economy.Calculate(sim, 0).Expenses, player.Treasury);
        Assert.Equal(0, settled.UnpaidGold); Assert.Equal(0, settled.StrikeTurns);
        Assert.Equal(DiplomaticDealStatus.Active, diplomacy.Deals(sim).Single().Status);
        Assert.True(economy.Calculate(sim, 0).NetGold >= 0);
        economy.SetRates(sim, 0, new(rates.Gold - 5, rates.Research + 5, 0, 0));
        Assert.True(economy.Calculate(sim, 0).NetGold < 0); // One fewer legal slider step cannot fund the payment.
    }
    [Fact]
    public void Imported_resource_satisfies_building_requirements_in_the_receiving_city()
    {
        var sim = Scenario(); Meet(sim);
        City(sim, 0, 40); var foreign = City(sim, 1, 41);
        sim.State.Plots[40].Bonus = Grain;
        var production = sim.Content.Services.Get<ICityProductionService>().Single();
        Assert.Contains(production.Validate(sim, foreign.Id, "cities:building", "diplomacy_probe:granary").Failures,
            f => f.Explanation == "Requires connected resource: " + Grain);
        Export(sim, Grain);
        Assert.True(production.Validate(sim, foreign.Id, "cities:building", "diplomacy_probe:granary").Success);
    }
    [Fact]
    public void Exports_draw_on_every_disconnected_copy_and_leave_the_capital_network_its_remaining_copy()
    {
        var sim = Scenario(); Meet(sim); Wall(sim);
        var capital = City(sim, 0, 40); var colony = City(sim, 0, 47);
        sim.State.Plots[40].Bonus = Grain; sim.State.Plots[47].Bonus = Grain;
        var diplomacy = Service(sim);
        Export(sim, Grain);
        Assert.Equal(1, diplomacy.AvailableResourceCount(sim, 0, capital.Id, Grain));
        Assert.Equal(0, diplomacy.AvailableResourceCount(sim, 0, colony.Id, Grain));
        Export(sim, Grain);
        Reject(sim, new ProposeDealCommand { Player = 0, Target = 1, Items = [new(0, TradeItemKind.Resource, Grain)] }, "unreserved");
    }
    [Fact]
    public void Foreign_passage_between_own_cities_needs_open_borders()
    {
        var sim = Scenario(); Meet(sim);
        var west = City(sim, 0, 40); var east = City(sim, 0, 42);
        sim.State.Plots[41].Owner = 1; sim.State.Plots[41].Revealed.Add(0);
        var connections = sim.Content.Services.Get<ICityConnectionService>().Single();
        Assert.False(connections.Connected(sim, west.Id, east.Id));
        Apply(sim, new SignOpenBordersCommand { Player = 0, Target = 1 });
        Assert.True(connections.Connected(sim, west.Id, east.Id));
    }
    [Fact]
    public void Declaring_war_consumes_the_victims_defensive_pacts()
    {
        var sim = Scenario(); Meet(sim, 0, 1); Meet(sim, 1, 2);
        Apply(sim, new SignDefensivePactCommand { Player = 1, Target = 2 });
        Apply(sim, new DiplomaticDeclareWarCommand { Player = 0, Target = 1 });
        Assert.Contains(0, sim.TeamOf(2).Wars);
        Assert.False(Service(sim).HasDefensivePact(sim, 1, 2));
    }
    /// <summary>Five gold per player, run after the Gold phase so settlement sees income earned during the turn.</summary>
    private sealed class IncomeProbe : IPlayerTurnSystem
    {
        public string Id => "diplomacy_probe:income";
        public int Order => TurnPhase.Gold + 1;
        public void Run(Simulation sim, int player) => sim.State.PlayerById(player).Treasury += 5;
    }
    private sealed class Probe(S.CivicDef civic, S.RuleConstantsDef constants) : IMod
    {
        public void Register(IModContext context)
        {
            context.Content<S.RuleConstantsDef>().Override(new() { Id = constants.Id, Values = constants.Values, Raw = constants.Raw.SetItem("MAX_TRADE_ROUTES", "1").SetItem("INITIAL_TRADE_ROUTES", "1") });
            context.Content<S.BonusDef>().Register(new() { Id = "diplomacy_probe:grain" });
            context.Content<S.BuildingDef>().Register(new() { Id = "diplomacy_probe:granary", DefaultBuilding = "diplomacy_probe:granary", BonusType = Grain });
            context.TurnSystems.Register(new IncomeProbe());
            context.Content<S.LeaderDef>().Register(new() { Id = "diplomacy_probe:leader_a", BaseAttitude = 1, BasePeaceWeight = 3, WarmongerRespect = 2, SameReligionAttitudeChange = 2, SameReligionAttitudeDivisor = 2, SameReligionAttitudeChangeLimit = 3, DifferentReligionAttitudeChange = -3, FavoriteCivic = civic.Id, FavoriteCivicAttitudeChange = 2, FavoriteCivicAttitudeDivisor = 2, FavoriteCivicAttitudeChangeLimit = 2, CloseBordersAttitudeChange = -2, AtPeaceAttitudeDivisor = 2, AtPeaceAttitudeChangeLimit = 2 });
            context.Content<S.LeaderDef>().Register(new() { Id = "diplomacy_probe:leader_b", BasePeaceWeight = 5, WarmongerRespect = 1 });
        }
    }
}
