using Ariel.Content;
using Ariel.Game;
using Ariel.Modding;
using Xunit;
using S = Ariel.Content.Schema;

namespace Ariel.Mods.Tests;

/// <summary>Every rule that asks which buildings a city runs gets the city rules' answer: a building granted by
/// the empire counts, a building that needs power does not count in an unpowered city.</summary>
public sealed class RunningBuildingsTests
{
    private const string Probe = "running_probe:";

    private static readonly Lazy<ContentDb> Content = new(() =>
    {
        var mods = new LoadedMods();
        return ModLoader.Build(mods.Sources.Append(new ModSource
        {
            Manifest = new() { Id = "running_probe", Version = "1", Dependencies = ["cities"], References = ["cities", "foundation", "military"] },
            Root = mods.Root, Instance = new BuildingsProbe(),
        }));
    });

    private sealed class BuildingsProbe : IMod
    {
        public void Register(IModContext context)
        {
            // The shipped constants allow no trade routes, so nothing would count slots.
            var rules = context.Content<S.RuleConstantsDef>().All.Single();
            context.Content<S.RuleConstantsDef>().Override(new() { Id = rules.Id, Values = rules.Values, Raw = rules.Raw.SetItem("MAX_TRADE_ROUTES", "8") });
            var buildings = context.Content<S.BuildingDef>();
            // The grant source gives every city of its owner the granted building, which no city builds itself.
            buildings.Register(new() { Id = Probe + "grant", DefaultBuilding = Probe + "grant", FreeBuilding = Probe + "granted" });
            buildings.Register(new() { Id = Probe + "granted", DefaultBuilding = Probe + "granted", TradeRoutes = 1,
                AnarchyModifier = -50, GreatPersonUnit = "military:warrior", GreatPeopleRateChange = 3 });
            buildings.Register(new() { Id = Probe + "unpowered", DefaultBuilding = Probe + "unpowered", RequiresPower = true,
                TradeRoutes = 1, AnarchyModifier = -25, GreatPersonUnit = "military:warrior", GreatPeopleRateChange = 5 });
        }
    }

    private static (Simulation Sim, City City) Scenario(params string[] buildings)
    {
        var sim = GameFactory.Scenario(Content.Value);
        var city = new City { Id = sim.State.NextCityId++, Owner = 0, Position = sim.State.Grid.HexOf(new TileId(40)), Population = 3 };
        city.Buildings.AddRange(buildings);
        sim.State.AddCity(city);
        return (sim, city);
    }

    private static T Service<T>(Simulation sim) where T : class => sim.Content.Services.Get<T>().Single();

    [Fact]
    public void Trade_route_slots_count_granted_buildings_and_skip_unpowered_ones()
    {
        var (sim, city) = Scenario(Probe + "grant", Probe + "unpowered");

        var lines = Service<ITradeRouteService>(sim).Calculate(sim, city.Id).Ledger.Lines.Where(l => l.Key == "trade:slots");

        Assert.Equal(1, lines.Where(l => l.Source == Probe + "granted").Sum(l => l.Amount));
        Assert.DoesNotContain(lines, l => l.Source == Probe + "unpowered");
    }

    [Fact]
    public void Anarchy_length_counts_granted_buildings_and_skips_unpowered_ones()
    {
        var (sim, _) = Scenario(Probe + "grant", Probe + "unpowered");

        long percent = ModifierSink.Collect(new(sim, ModifierScope.Player, 0)).Ledger.Total("government:anarchy_percent");

        Assert.Equal(-50, percent);
    }

    [Fact]
    public void Great_people_rates_count_granted_buildings_and_skip_unpowered_ones()
    {
        var (sim, city) = Scenario(Probe + "grant", Probe + "unpowered");

        var lines = Service<IGreatPeopleService>(sim).Calculate(sim, city.Id).Ledger.Lines.Where(l => l.Key == "great_people:base_rate");

        Assert.Equal(3, lines.Where(l => l.Source == Probe + "granted").Sum(l => l.Amount));
        Assert.DoesNotContain(lines, l => l.Source == Probe + "unpowered");
    }
}
