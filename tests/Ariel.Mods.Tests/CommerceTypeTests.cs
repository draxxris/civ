using Ariel.Content;
using Ariel.Content.Schema;
using Ariel.Game;
using Ariel.Modding;
using Xunit;

namespace Ariel.Mods.Tests;

/// <summary>A commerce type a mod registers runs through the economy exactly like the foundation's: it gets a slider
/// rate, a share of city commerce, typed modifiers, deficit cuts at settlement and a place in saved state.</summary>
public sealed class CommerceTypeTests
{
    private const string Faith = "faith";
    private static readonly Lazy<ContentDb> Content = new(() => Build(Probe("faith_probe", new FaithMod())));

    private static ModSource Probe(string id, IMod mod, params string[] references) =>
        new() { Manifest = new() { Id = id, Version = "1", References = references }, Root = new LoadedMods().Root, Instance = mod };
    private static ContentDb Build(params ModSource[] extra) => ModLoader.Build(new LoadedMods().Sources.Concat(extra));

    private static Simulation Scenario()
    {
        var sim = GameFactory.Scenario(Content.Value);
        GameFactory.NeutralPolitics(sim);
        sim.Settings.GameOptions["foundation:gameoption_no_revolution"] = true;
        var city = P6RulesScenario.AddCity(sim, 0, 39);
        city.Population = 3;
        city.Extensions.Set(sim.Content, new CityRuleState { ManualWork = true });
        sim.State.PlayerById(0).Treasury = 1000;
        return sim;
    }

    private static CommerceVector Rates(Simulation sim, long gold, long research, long faith) => sim.Rules().Commerce.Zero
        .With(FoundationCommerce.GoldKey, gold).With(FoundationCommerce.ResearchKey, research).With(Faith, faith);

    [Fact]
    public void Registered_type_joins_the_commerce_set_in_order_with_typed_modifier_keys()
    {
        var commerce = Content.Value.Commerce;
        Assert.Equal(["gold", "research", "culture", "espionage", Faith], commerce.Select(t => t.Key));
        Assert.Equal("faith_probe:commerce_faith", commerce[Faith].Id);
        Assert.Equal(5, commerce.Zero.Count);
        Assert.True(commerce.IsTypedKey("commerce.flat.faith"));
        Assert.True(commerce.IsTypedKey("commerce.percent.faith"));
        Assert.False(commerce.IsTypedKey("commerce.flat.piety"));
        Assert.Equal(6, Ariel.Ai.PlannerPoints.Commerce(Content.Value, CommerceVector.Of((Faith, 3))));
    }

    [Fact]
    public void City_commerce_splits_the_slider_and_applies_direct_and_percent_faith()
    {
        var sim = Scenario();
        P6RulesScenario.Apply(sim, new SetCommerceRatesCommand { Player = 0, Rates = Rates(sim, 30, 50, 20) });
        var result = sim.Rules().Economy.CalculateCity(sim, sim.State.Cities[0].Id);
        var ledger = result.Ledger;
        long split = ledger.Total("economy:faith_split_hundredths");
        long raw = ledger.Total("economy:gold_split_hundredths") + ledger.Total("economy:research_split_hundredths") + split
            + ledger.Total("economy:culture_split_hundredths") + ledger.Total("economy:espionage_split_hundredths");
        Assert.True(raw > 0);
        Assert.Equal(raw * 20 / 100, split);
        Assert.Equal(3, ledger.Total("commerce.flat.faith"));
        Assert.Equal(50, ledger.Total("commerce.percent.faith"));
        Assert.Equal(300, ledger.Total("economy:faith_direct_hundredths"));
        Assert.Equal(150, ledger.Total("economy:faith_factor_percent"));
        Assert.Equal((split + 300) * 150 / 100, result.Hundredths[Faith]);
        Assert.Equal(result.Hundredths[Faith], ledger.Total("economy:faith_hundredths"));
        var player = sim.Rules().Economy.Calculate(sim, 0);
        Assert.Equal(result.Hundredths[Faith], player.CommerceHundredths[Faith]);
        Assert.Equal(5, player.CommerceHundredths.Count);
    }

    [Fact]
    public void Rates_name_only_loaded_types_and_respect_each_type_maximum()
    {
        var sim = Scenario();
        var economy = sim.Rules().Economy;
        Assert.Equal(sim.Rules().Commerce.InitialRates, economy.Rates(sim, 0));
        Assert.Equal(0, economy.Rates(sim, 0)[Faith]);
        Assert.Contains("maximum: faith", P6RulesScenario.Reject(sim, new SetCommerceRatesCommand { Player = 0, Rates = Rates(sim, 0, 40, 60) }));
        Assert.Contains("Unknown commerce type: piety",
            P6RulesScenario.Reject(sim, new SetCommerceRatesCommand { Player = 0, Rates = Rates(sim, 30, 50, 20).With("piety", 0) }));
        // A command may leave a type out; the state still names every loaded type.
        P6RulesScenario.Apply(sim, new SetCommerceRatesCommand { Player = 0, Rates = CommerceVector.Of(("gold", 50), (Faith, 50)) });
        var rates = economy.Rates(sim, 0);
        Assert.Equal(5, rates.Count);
        Assert.Equal(50, rates[Faith]);
        Assert.Equal(0, rates.Research);
        var loaded = SaveGame.Load(sim.Content, SaveGame.Save(sim));
        Assert.Contains("\"Faith\":50", SaveGame.Save(loaded));
        Assert.Equal(rates, loaded.Rules().Economy.Rates(loaded, 0));
        Assert.Equal(sim.ComputeHash(), loaded.ComputeHash());
    }

    [Fact]
    public void Settlement_cuts_a_registered_type_into_gold_when_the_treasury_runs_short()
    {
        var sim = Scenario();
        var player = sim.State.PlayerById(0);
        P6RulesScenario.Apply(sim, new SetCommerceRatesCommand { Player = 0, Rates = Rates(sim, 0, 0, 50).With(FoundationCommerce.CultureKey, 50) });
        for (int i = 0; i < 100; i++)
            sim.State.AddUnit(new Unit { Id = sim.State.NextUnitId++, Owner = 0, Type = "military:warrior", Position = sim.State.Cities[0].Position });
        Assert.True(sim.Rules().Economy.Calculate(sim, 0).NetGold < 0);
        player.Treasury = 0;
        sim.AdvanceTurn();
        var state = player.Extensions.Get<EconomyPlayerState>(sim.Content)!;
        // Culture is cut before faith (its deficit cut comes first), and every cut goes to gold.
        Assert.Equal(0, state.Rates.Culture);
        Assert.True(state.Rates[Faith] < 50);
        Assert.Equal(100 - state.Rates[Faith], state.Rates.Gold);
        Assert.Equal(5, state.SettledCommerceHundredths.Count);
    }

    [Fact]
    public void Content_that_names_an_unloaded_or_undeclared_commerce_type_fails_loading()
    {
        var error = Assert.Throws<InvalidDataException>(() => Build(Probe("faith_probe", new FaithMod()),
            Probe("faith_user", new FaithUser(), "foundation")));
        Assert.Contains("'piety' is not a loaded commerce type", error.Message);
        Assert.Contains("mod 'faith_user' must declare commerce type provider 'faith_probe'", error.Message);
    }

    private sealed class FaithMod : IMod
    {
        public void Register(IModContext context)
        {
            context.Content<CommerceDef>().Register(new()
            {
                Id = "faith_probe:commerce_faith", Name = "Faith", Key = Faith, Order = 10, FloorAtZero = true, DeficitCut = 10, AiPoints = 2,
            });
            context.RegisterService<IModifierSource>("shrines", new Shrines());
        }
    }

    private sealed class Shrines : IModifierSource
    {
        public void Contribute(ModifierContext context, ModifierSink sink)
        {
            if (context.Scope == ModifierScope.Player) sink.Add("faith_probe:limit", "economy:max_rate:faith", -50);
            if (context.Scope != ModifierScope.City) return;
            sink.AddYield("faith_probe:market", new(0, 0, 5));
            sink.AddCommerce("faith_probe:shrine", CommerceVector.Of((Faith, 3)));
            sink.AddCommerce("faith_probe:shrine", CommerceVector.Of((Faith, 50)), true);
        }
    }

    private sealed class FaithUser : IMod
    {
        public void Register(IModContext context)
        {
            context.Content<BuildingDef>().Register(new()
            {
                Id = "faith_user:chapel", DefaultBuilding = "faith_user:chapel", CommerceChanges = CommerceVector.Of((Faith, 1)),
            });
            context.Content<BuildingDef>().Register(new()
            {
                Id = "faith_user:altar", DefaultBuilding = "faith_user:altar", CommerceChanges = CommerceVector.Of(("piety", 1)),
            });
        }
    }
}
