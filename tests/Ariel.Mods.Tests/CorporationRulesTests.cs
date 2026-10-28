using Ariel.Content;
using Ariel.Game;
using Ariel.Modding;
using Xunit;

namespace Ariel.Mods.Tests;

internal static class P6RulesScenario
{
    private static readonly Lazy<ContentDb> Content = new(() => BuildContent());
    private static readonly Lazy<ContentDb> HalfMaintenanceContent = new(() => BuildContent(new HalfMaintenanceMod()));
    private static ContentDb BuildContent(IMod? extra = null)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        string configuration = dir.Parent!.Name;
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "Ariel.slnx"))) dir = dir.Parent;
        var names = new[] { "Corporations", "Espionage", "Diplomacy" };
        var sources = names.Select(name => ModLoader.FromDirectory(Path.Combine(dir!.FullName, "src", "Mods", name, "bin", configuration, "net10.0"))).ToArray();
        var loaded = new LoadedMods();
        var all = loaded.Sources.Where(s => !sources.Any(n => n.Manifest.Id == s.Manifest.Id)).Concat(sources);
        if (extra is not null) all = all.Append(new ModSource { Manifest = new() { Id = "probe", Version = "1", References = ["cities"] }, Root = loaded.Root, Instance = extra });
        return ModLoader.Build(all);
    }
    private sealed class HalfMaintenanceMod : IMod
    {
        public void Register(IModContext context) => context.Content<Ariel.Content.Schema.BuildingDef>().Override(new()
        {
            Id = "cities:courthouse", Name = "Courthouse", DefaultBuilding = "cities:courthouse", MaintenanceModifier = -50,
        });
    }
    public static T Service<T>(Simulation sim) where T : class => sim.Content.Services.Get<T>().Single();
    public static Simulation Create(bool halfMaintenance = false)
    {
        var sim = GameFactory.Scenario(halfMaintenance ? HalfMaintenanceContent.Value : Content.Value);
        GameFactory.NeutralPolitics(sim);
        foreach (var plot in sim.State.Plots) plot.Owner = null;
        AddCity(sim, 0, 14); AddCity(sim, 0, 18); AddCity(sim, 1, 57);
        foreach (var player in sim.State.Players) player.Treasury = 10000;
        sim.TeamOf(0).Techs.Add("research:currency");
        Service<IDiplomacyService>(sim).EstablishContact(sim, 0, 1);
        return sim;
    }
    public static City AddCity(Simulation sim, int owner, int tile)
    {
        var city = new City { Id = sim.State.NextCityId++, Owner = owner, Position = sim.State.Grid.HexOf(new TileId(tile)) };
        sim.State.AddCity(city); sim.State.PlotAt(city.Position).Owner = owner; return city;
    }
    public static Unit AddUnit(Simulation sim, int owner, City city, string type)
    {
        var unit = new Unit { Id = sim.State.NextUnitId++, Owner = owner, Position = city.Position, Type = type, MovesLeft = 60 };
        sim.State.AddUnit(unit); return unit;
    }
    public static void Resource(Simulation sim, City city, string bonus)
    {
        sim.State.PlotAt(city.Position).Bonus = bonus;
        var def = sim.Content.Get<Ariel.Content.Schema.BonusDef>(bonus);
        if (def.RevealTech is { } reveal) sim.TeamOf(city.Owner).Techs.Add(reveal);
        if (def.TradeTech is { } trade) sim.TeamOf(city.Owner).Techs.Add(trade);
    }
    public static void Apply(Simulation sim, GameCommand command) => Assert.True(sim.TryApply(command, out var error), error);
    public static string Reject(Simulation sim, GameCommand command)
    {
        ulong hash = sim.ComputeHash();
        Assert.False(sim.TryApply(command, out var error));
        Assert.Equal(hash, sim.ComputeHash()); return error!;
    }
}

public sealed class CorporationRulesTests
{
    private const string Cereal = "corporations:cereal_mills", Ethanol = "corporations:standard_ethanol";
    private static ICorporationService Corps(Simulation sim) => P6RulesScenario.Service<ICorporationService>(sim);
    private static void Found(Simulation sim, City city, string corporation = Cereal)
    {
        var def = sim.Content.Get<Ariel.Content.Schema.CorporationDef>(corporation);
        sim.TeamOf(city.Owner).Techs.Add(def.FoundingTech!);
        P6RulesScenario.Resource(sim, city, "world:corn");
        var gp = P6RulesScenario.AddUnit(sim, city.Owner, city, def.RequiredGreatPersonClass);
        P6RulesScenario.Apply(sim, new FoundCorporationCommand { Player = city.Owner, Unit = gp.Id, Corporation = corporation });
    }
    [Fact]
    public void Founding_requires_the_correct_unit_technology_and_connected_resource_and_is_unique()
    {
        var sim = P6RulesScenario.Create(); var city = sim.State.Cities[0];
        var wrong = P6RulesScenario.AddUnit(sim, 0, city, "military:scientist");
        Assert.Contains("Great Person class", P6RulesScenario.Reject(sim, new FoundCorporationCommand { Player = 0, Unit = wrong.Id, Corporation = Cereal }));
        var gp = P6RulesScenario.AddUnit(sim, 0, city, "military:merchant");
        var command = new FoundCorporationCommand { Player = 0, Unit = gp.Id, Corporation = Cereal };
        Assert.Contains("technology", P6RulesScenario.Reject(sim, command));
        sim.TeamOf(0).Techs.Add("research:refrigeration");
        Assert.Contains("resource", P6RulesScenario.Reject(sim, command));
        P6RulesScenario.Resource(sim, city, "world:corn");
        P6RulesScenario.Apply(sim, command);
        Assert.Null(sim.State.FindUnit(gp.Id)); Assert.Equal(city.Id, Corps(sim).Headquarters(sim, Cereal));
        Assert.Contains(Cereal, Corps(sim).Present(sim, city.Id));
        var rival = sim.State.Cities[2]; sim.TeamOf(1).Techs.Add("research:refrigeration"); P6RulesScenario.Resource(sim, rival, "world:corn");
        var rivalGp = P6RulesScenario.AddUnit(sim, 1, rival, "military:merchant");
        Assert.Contains("already founded", P6RulesScenario.Reject(sim, new FoundCorporationCommand { Player = 1, Unit = rivalGp.Id, Corporation = Cereal }));
        Assert.Single(sim.Events.Published.OfType<CorporationFounded>());
    }
    [Fact]
    public void Spreading_charges_gold_consumes_executives_and_counts_worldwide_branches()
    {
        var sim = P6RulesScenario.Create(); var hq = sim.State.Cities[0]; var domestic = sim.State.Cities[1]; var foreign = sim.State.Cities[2];
        Found(sim, hq); P6RulesScenario.Resource(sim, domestic, "world:corn"); P6RulesScenario.Resource(sim, foreign, "world:corn");
        long initial = sim.State.PlayerById(0).Treasury;
        var spread = new SpreadCorporationCommand { Player = 0, City = domestic.Id, Corporation = Cereal };
        sim.State.PlayerById(0).Treasury = 99;
        Assert.Contains("gold", P6RulesScenario.Reject(sim, spread)); sim.State.PlayerById(0).Treasury = initial;
        P6RulesScenario.Apply(sim, spread);
        Assert.Contains("executive", P6RulesScenario.Reject(sim, new SpreadCorporationCommand { Player = 0, City = foreign.Id, Corporation = Cereal }));
        var executive = P6RulesScenario.AddUnit(sim, 0, foreign, "military:executive_1");
        P6RulesScenario.Apply(sim, new SpreadCorporationCommand { Player = 0, City = foreign.Id, Unit = executive.Id, Corporation = Cereal });
        Assert.Null(sim.State.FindUnit(executive.Id)); Assert.Equal(initial - 200, sim.State.PlayerById(0).Treasury);
        var effects = ModifierSink.Collect(new(sim, ModifierScope.City, 0, hq));
        Assert.Equal(12, effects.Ledger.Lines.Where(l => l.Source == Cereal + ":headquarters" && l.Key == "commerce.flat.gold").Sum(l => l.Amount));
        Assert.Equal(1, P6RulesScenario.Service<ICityYieldService>(sim).Calculate(sim, domestic.Id).Ledger.Lines.Single(l => l.Source == Cereal && l.Key == "yield.flat.food").Amount);
    }
    [Fact]
    public void Competing_branches_are_displaced_but_headquarters_are_protected()
    {
        var sim = P6RulesScenario.Create(); var hq = sim.State.Cities[0]; var branch = sim.State.Cities[1]; var otherHq = sim.State.Cities[2];
        Found(sim, hq); Found(sim, otherHq, Ethanol);
        P6RulesScenario.Resource(sim, branch, "world:corn");
        P6RulesScenario.Apply(sim, new SpreadCorporationCommand { Player = 0, City = branch.Id, Corporation = Cereal });
        var unit = P6RulesScenario.AddUnit(sim, 1, branch, "military:executive_3");
        P6RulesScenario.Apply(sim, new SpreadCorporationCommand { Player = 1, City = branch.Id, Unit = unit.Id, Corporation = Ethanol });
        Assert.DoesNotContain(Cereal, Corps(sim).Present(sim, branch.Id)); Assert.Contains(Ethanol, Corps(sim).Present(sim, branch.Id));
        Assert.Equal(2, P6RulesScenario.Service<IPlayerEconomyService>(sim).CalculateCity(sim, branch.Id).Ledger.Lines.Single(l => l.Source == Ethanol && l.Key == "commerce.flat.research").Amount);
        var attacker = P6RulesScenario.AddUnit(sim, 1, hq, "military:executive_3");
        Assert.Contains("headquarters", P6RulesScenario.Reject(sim, new SpreadCorporationCommand { Player = 1, City = hq.Id, Unit = attacker.Id, Corporation = Ethanol }));
    }
    [Fact]
    public void Resource_copies_scale_output_and_maintenance_and_civics_suppress_without_erasing_presence()
    {
        var sim = P6RulesScenario.Create(); var hq = sim.State.Cities[0]; Found(sim, hq);
        var tile = sim.State.Grid.Neighbors(sim.State.Grid.TileAt(hq.Position)).First();
        var second = sim.State.Plots[tile.Value]; second.Owner = 0; second.Bonus = "world:corn"; second.Improvement = "world:improvement_improvement_farm"; second.Route = "world:road";
        Assert.Equal(2, Corps(sim).ConsumedBonusCount(sim, hq.Id, Cereal));
        Assert.Equal(2, ModifierSink.Collect(new(sim, ModifierScope.City, 0, hq)).Ledger.Lines.Single(l => l.Source == Cereal && l.Key == "yield.flat.food").Amount);
        Assert.Equal(750, Corps(sim).MaintenanceHundredths(sim, hq.Id)); // (400 headquarters + 200 resources) * 100/80.
        sim.State.PlayerById(0).Civics["test"] = "government:guilds";
        Assert.Equal(-105, ModifierSink.Collect(new(sim, ModifierScope.Player, 0)).Ledger.Total("cities:corporation_maintenance_percent"));
        Assert.Equal(0, Corps(sim).MaintenanceHundredths(sim, hq.Id));
        sim.State.PlayerById(0).Civics["test"] = "government:planned";
        Assert.False(Corps(sim).IsActive(sim, hq.Id, Cereal)); Assert.Equal(0, Corps(sim).MaintenanceHundredths(sim, hq.Id));
        Assert.Contains(Cereal, Corps(sim).Present(sim, hq.Id));
        Assert.DoesNotContain(ModifierSink.Collect(new(sim, ModifierScope.City, 0, hq)).Ledger.Lines, l => l.Source == Cereal);
        sim.State.PlayerById(0).Civics.Clear(); Assert.True(Corps(sim).IsActive(sim, hq.Id, Cereal));
        second.Bonus = null; sim.State.PlotAt(hq.Position).Bonus = null;
        Assert.Equal(500, Corps(sim).MaintenanceHundredths(sim, hq.Id)); // Headquarters commerce remains in the base without resources.
    }
    [Fact]
    public void Foreign_corporation_civics_block_spread_and_suspend_existing_foreign_branches()
    {
        var sim = P6RulesScenario.Create(); var hq = sim.State.Cities[0]; var foreign = sim.State.Cities[2]; Found(sim, hq);
        P6RulesScenario.Resource(sim, foreign, "world:corn");
        var executive = P6RulesScenario.AddUnit(sim, 0, foreign, "military:executive_1");
        var command = new SpreadCorporationCommand { Player = 0, City = foreign.Id, Unit = executive.Id, Corporation = Cereal };
        sim.State.PlayerById(1).Civics["test"] = "government:mercantilism";
        Assert.Contains("Civics", P6RulesScenario.Reject(sim, command));
        sim.State.PlayerById(1).Civics.Clear(); P6RulesScenario.Apply(sim, command);
        sim.State.PlayerById(1).Civics["test"] = "government:mercantilism";
        Assert.False(Corps(sim).IsActive(sim, foreign.Id, Cereal)); Assert.Equal(0, Corps(sim).MaintenanceHundredths(sim, foreign.Id));
        Assert.Contains(Cereal, Corps(sim).Present(sim, foreign.Id)); Assert.True(Corps(sim).IsActive(sim, hq.Id, Cereal));
    }
    [Fact]
    public void Captured_headquarters_moves_the_mercantilism_boundary_to_the_new_owner()
    {
        var sim = P6RulesScenario.Create(); var hq = sim.State.Cities[0]; var branch = sim.State.Cities[1];
        hq.Population = 3; // The headquarters must survive conquest instead of auto-razing.
        Found(sim, hq);
        P6RulesScenario.Resource(sim, branch, "world:corn");
        P6RulesScenario.Apply(sim, new SpreadCorporationCommand { Player = 0, City = branch.Id, Corporation = Cereal });
        sim.State.PlayerById(0).Civics["test"] = "government:mercantilism";
        Assert.True(Corps(sim).IsActive(sim, branch.Id, Cereal)); // The branch shares a team with the headquarters.
        sim.State.SetWar(0, 1, true);
        P6RulesScenario.Service<ICityCaptureService>(sim).Capture(sim, hq.Id, 1);
        Assert.False(Corps(sim).IsActive(sim, branch.Id, Cereal)); // The headquarters now belongs to team 1.
        Assert.Equal(0, Corps(sim).MaintenanceHundredths(sim, branch.Id));
        Assert.Contains(Cereal, Corps(sim).Present(sim, branch.Id));
        Assert.Equal(hq.Id, Corps(sim).Headquarters(sim, Cereal));
        Assert.True(Corps(sim).IsActive(sim, hq.Id, Cereal)); // Domestic to the new owner, which has no mercantilism.
    }
    [Theory]
    [InlineData(0)]
    [InlineData(10000)]
    public void Courthouse_reduces_corporate_costs_before_economy_adjusts_sliders_and_settles_once(long treasury)
    {
        var sim = P6RulesScenario.Create(halfMaintenance: true); var branch = sim.State.Cities[1]; var hq = sim.State.Cities[2];
        sim.State.RemoveCity(sim.State.Cities[0].Id);
        foreach (var city in sim.State.Cities) city.Extensions.Set(sim.Content, new CityRuleState { ManualWork = true });
        Found(sim, hq); P6RulesScenario.Resource(sim, branch, "world:corn");
        var tile = sim.State.Grid.Neighbors(sim.State.Grid.TileAt(branch.Position)).First();
        var extra = sim.State.Plots[tile.Value]; extra.Owner = 0; extra.Bonus = "world:corn";
        extra.Improvement = "world:improvement_improvement_farm"; extra.Route = "world:road";
        var executive = P6RulesScenario.AddUnit(sim, 1, branch, "military:executive_1");
        P6RulesScenario.Apply(sim, new SpreadCorporationCommand { Player = 1, City = branch.Id, Unit = executive.Id, Corporation = Cereal });
        var maintenance = P6RulesScenario.Service<ICityMaintenanceService>(sim);
        var economy = P6RulesScenario.Service<IPlayerEconomyService>(sim);
        branch.Buildings.Add("cities:palace"); // Enough commerce to cover the full corporate formula at zero treasury.
        long corporateCost = Corps(sim).MaintenanceHundredths(sim, branch.Id);
        Assert.Equal(750, corporateCost);
        var full = maintenance.Calculate(sim, branch.Id);
        Assert.Equal(corporateCost, full.CorporateHundredths);
        Assert.Equal(full.DistanceHundredths + full.CityCountHundredths + corporateCost, full.TotalHundredths);
        branch.Buildings.Add("cities:courthouse");
        var reduced = maintenance.Calculate(sim, branch.Id);
        Assert.Equal(full.TotalHundredths / 2, reduced.TotalHundredths);
        Assert.Equal(reduced.TotalHundredths, reduced.Ledger.Total("cities:maintenance_hundredths"));
        economy.SetRates(sim, 0, new(0, 100, 0, 0));
        var before = economy.Calculate(sim, 0);
        Assert.Equal(reduced.TotalHundredths, before.Ledger.Total("economy:maintenance_hundredths"));
        Assert.Equal(corporateCost, before.Ledger.Total("economy:corporate_maintenance_hundredths"));
        Assert.Equal(7, before.Ledger.Total("economy:corporate_maintenance"));
        Assert.Equal(3, before.Expenses);
        var owner = sim.State.PlayerById(0); owner.Treasury = treasury;
        sim.AdvanceTurn();
        var settled = owner.Extensions.Get<EconomyPlayerState>(sim.Content)!;
        Assert.Equal(treasury + settled.SettledCommerceHundredths.Gold / 100 - before.Expenses, owner.Treasury);
        Assert.Equal(0, settled.UnpaidGold);
        if (treasury == 0) Assert.True(economy.Rates(sim, 0).Gold > 0);
        else Assert.Equal(new CommerceVector(0, 100, 0, 0), economy.Rates(sim, 0));
    }
    [Fact]
    public void Maintenance_is_settled_once_with_economy_rounding_and_founding_records_survive_save_load()
    {
        var sim = P6RulesScenario.Create(); var hq = sim.State.Cities[0]; Found(sim, hq);
        sim.State.PlayerById(0).Civics["test"] = "government:corporatist";
        foreach (var city in sim.State.Cities) city.Extensions.Set(sim.Content, new CityRuleState { ManualWork = true });
        Assert.Equal(156, Corps(sim).MaintenanceHundredths(sim, hq.Id)); // (400 + 100) * 25/100 * 100/80.
        long net = P6RulesScenario.Service<IPlayerEconomyService>(sim).Calculate(sim, 0).NetGold;
        long gold = sim.State.PlayerById(0).Treasury;
        var loaded = SaveGame.Load(sim.Content, SaveGame.Save(sim)); Assert.Equal(sim.ComputeHash(), loaded.ComputeHash());
        sim.AdvanceTurn(); loaded.AdvanceTurn();
        Assert.Equal(gold + net, sim.State.PlayerById(0).Treasury); // Economy rounds the combined city costs, not each corporation.
        Assert.Null(sim.State.PlayerById(0).Extensions.Get<CorporationPlayerState>(sim.Content));
        Assert.Equal(sim.ComputeHash(), loaded.ComputeHash());
        loaded = SaveGame.Load(sim.Content, SaveGame.Save(sim));
        net = P6RulesScenario.Service<IPlayerEconomyService>(sim).Calculate(sim, 0).NetGold; gold = sim.State.PlayerById(0).Treasury;
        sim.AdvanceTurn(); loaded.AdvanceTurn(); Assert.Equal(gold + net, sim.State.PlayerById(0).Treasury);
        Assert.Equal(sim.ComputeHash(), loaded.ComputeHash());
        sim.State.RemoveCity(hq.Id); Assert.Null(Corps(sim).Headquarters(sim, Cereal));
        var rival = sim.State.Cities.Last(); var gp = P6RulesScenario.AddUnit(sim, rival.Owner, rival, "military:merchant");
        Assert.Contains("already founded", P6RulesScenario.Reject(sim, new FoundCorporationCommand { Player = rival.Owner, Unit = gp.Id, Corporation = Cereal }));
    }
}
