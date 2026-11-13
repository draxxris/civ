using Ariel.Ai;
using Ariel.Composition;
using Ariel.Content;
using Ariel.Content.Schema;
using Ariel.Game;
using Ariel.Modding;
using Xunit;
using static Ariel.Mods.Tests.AiBindings;

using UnitDef = Ariel.Content.Schema.UnitDef;

namespace Ariel.Mods.Tests;

/// <summary>Mods adjust what the standard computer player values through <see cref="IAiValuer{TSubject}"/>. Each test
/// makes one decision twice, without and with a valuer, and shows the valuer changes it. The ResourceVariety test mod
/// is the "food variety" idea in miniature; the other valuers are registered by small probe mods built here.</summary>
public sealed class AiValuerTests
{
    private const string Pig = "world:pig", Cow = "world:cow", Warrior = "military:warrior";
    private static string Host => AppContext.BaseDirectory;
    private static readonly Lazy<ModSource[]> Shipped = new(() => GameContent.Staged(Host).ToArray());
    private static readonly Lazy<ModSource> Variety = new(() => ModLoader.FromDirectory(Path.Combine(Host, "test-mods", "ResourceVariety")));
    private static readonly Lazy<ContentDb> Plain = new(() => ModLoader.Build(Shipped.Value));
    private static readonly Lazy<ContentDb> Varied = new(() => ModLoader.Build(Shipped.Value.Append(Variety.Value)));

    private static ContentDb With(params ModSource[] probes) => ModLoader.Build(Shipped.Value.Concat(probes));

    /// <summary>A mod built here whose registration is <paramref name="register"/>.</summary>
    private static ModSource Probe(string id, Action<IModContext> register) => new()
    {
        Manifest = new() { Id = id, Version = "1.0.0", Dependencies = ["ai", "military"] }, Instance = new ProbeMod(register), Root = Host,
    };

    private static ModSource Valuer<TSubject>(Func<TSubject, int> adjust, string id = "valuer_probe:valuer", string mod = "valuer_probe")
        where TSubject : struct => Probe(mod, context => context.RegisterService<IAiValuer<TSubject>>("valuer", new FixedValuer<TSubject>(id, adjust)));

    private sealed class ProbeMod(Action<IModContext> register) : IMod
    {
        public void Register(IModContext context) => register(context);
    }

    private sealed class FixedValuer<TSubject>(string id, Func<TSubject, int> adjust) : IAiValuer<TSubject> where TSubject : struct
    {
        public string Id => id;
        public int Adjust(AiContext ctx, in TSubject subject) => adjust(subject);
    }

    private sealed class NoWishes(string id) : IProductionWishes
    {
        public string Id => id;
        public IEnumerable<ProductionWish> Wishes(AiContext ctx, City city) => [];
    }

    private sealed class Planner(string id) : IAiPlanner
    {
        public string Id => id;
        public int Order => AiPlannerOrder.Hurry + 100;
        public void Plan(AiContext ctx) { }
    }

    [Fact]
    public void Without_valuers_every_adjustment_is_zero()
    {
        var sim = Grassland(Plain.Value);
        var ctx = (AiContext)NewAi(sim, "AiContext", sim, 0);
        Assert.Empty(sim.Content.Services.Get<IAiValuer<ResourceSubject>>());
        Assert.DoesNotContain(sim.Content.Services.Get<IAiValuer<CivicSubject>>(), v => !v.Id.StartsWith("corporations:", StringComparison.Ordinal));
        var tech = sim.Content.Get<TechDef>().All.First();
        Assert.Equal(0, ctx.Adjustment(new TechSubject(tech)));
        Assert.Equal(0, ctx.Adjustment(new ResourceSubject(sim.Content.Get<BonusDef>(Pig), 0, null)));
    }

    [Fact]
    public void Valuers_of_one_subject_add_up_and_the_sum_is_bounded()
    {
        var content = With(
            Valuer<TechSubject>(_ => AiValuation.Limit, "first_probe:valuer", "first_probe"),
            Valuer<TechSubject>(_ => AiValuation.Limit, "second_probe:valuer", "second_probe"),
            Valuer<TechSubject>(t => t.Tech.Id.Length, "third_probe:valuer", "third_probe"));
        var sim = Grassland(content);
        var ctx = (AiContext)NewAi(sim, "AiContext", sim, 0);
        var tech = sim.Content.Get<TechDef>().All.First();
        Assert.Equal(AiValuation.Limit, ctx.Adjustment(new TechSubject(tech)));
        Assert.Equal(0, ctx.Adjustment(new CivicSubject(sim.Content.Get<CivicDef>().All.First(c => c.Id.StartsWith("government:", StringComparison.Ordinal)))));
    }

    [Fact]
    public void Two_valuers_planners_or_wish_sources_with_one_id_fail_the_content_load()
    {
        var valuers = Assert.Throws<InvalidOperationException>(() => With(
            Valuer<TechSubject>(_ => 1, "same_probe:valuer", "first_probe"),
            Valuer<TechSubject>(_ => 2, "same_probe:valuer", "second_probe")));
        Assert.Contains("'same_probe:valuer'", valuers.Message);
        Assert.Contains("IAiValuer<TechSubject>", valuers.Message);

        // Valuers of different subjects are different seams; one id may serve both.
        With(Valuer<TechSubject>(_ => 1, "same_probe:valuer", "first_probe"),
            Valuer<CivicSubject>(_ => 2, "same_probe:valuer", "second_probe"));

        var planners = Assert.Throws<InvalidOperationException>(() => With(
            Probe("planner_probe", context => context.RegisterService<IAiPlanner>("planner", new Planner("ai:research")))));
        Assert.Contains("'ai:research'", planners.Message);

        var wishes = Assert.Throws<InvalidOperationException>(() => With(
            Probe("first_probe", context => context.RegisterService<IProductionWishes>("wishes", new NoWishes("same_probe:wishes"))),
            Probe("second_probe", context => context.RegisterService<IProductionWishes>("wishes", new NoWishes("same_probe:wishes")))));
        Assert.Contains("'same_probe:wishes'", wishes.Message);
    }

    [Fact]
    public void A_worker_connects_a_resource_the_player_lacks_before_a_second_copy_of_one_it_holds()
    {
        // The player holds one pig, pastured beside its city. A second pig lies next to the worker, a first cow one
        // plot further. Without the variety mod the nearer pig comes first; with it, the cow does.
        TileId Choice(ContentDb content)
        {
            var sim = Pastures(content, out var worker);
            var ctx = NewAi(sim, "AiContext", sim, 0);
            var missions = (SortedDictionary<int, UnitMission>)Call(NewAi(sim, "WorkerPlanner", ctx), "EvaluateMissions")!;
            return missions[worker].Target;
        }
        var probe = Pastures(Plain.Value, out _);
        Assert.Equal(1, ResourceAccess.Count(probe, 0, probe.State.Cities[0].Id, Pig));
        Assert.Equal(0, ResourceAccess.Count(probe, 0, probe.State.Cities[0].Id, Cow));
        Assert.Equal(Tile(probe, 3, 3), Choice(Plain.Value));
        Assert.Equal(Tile(probe, 4, 3), Choice(Varied.Value));
    }

    [Fact]
    public void A_city_site_values_a_resource_the_player_lacks_above_a_second_copy()
    {
        int PlotValue(ContentDb content, int column)
        {
            var sim = Pastures(content, out _);
            var ctx = NewAi(sim, "AiContext", sim, 0);
            var values = AiType(sim, "SitePlanner").GetMethod("BuildTileValues", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic)!;
            return ((int[])values.Invoke(null, [ctx])!)[Tile(sim, column, 3).Value];
        }
        // Site scoring weighs plots on half the resource scale: the cow gains half the variety points, while the
        // second pig loses as much, which leaves it worth nothing.
        Assert.Equal(ResourceVarietyPoints / 2, PlotValue(Varied.Value, 4) - PlotValue(Plain.Value, 4));
        Assert.True(PlotValue(Plain.Value, 3) > 0);
        Assert.Equal(0, PlotValue(Varied.Value, 3));
    }

    [Fact]
    public void The_same_resource_valuer_prices_resources_in_deals()
    {
        long Worth(ContentDb content, long held)
        {
            var sim = Grassland(content);
            var ctx = NewAi(sim, "AiContext", sim, 0);
            var type = sim.Service<Ariel.Mods.Diplomacy.IDiplomacyService>()!.GetType().Assembly
                .GetType("Ariel.Mods.Diplomacy.DiplomacyPlanner", throwOnError: true)!;
            var planner = Activator.CreateInstance(type, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic
                | System.Reflection.BindingFlags.Public, binder: null, [ctx], culture: null)!;
            return (long)Call(planner, "ResourceWorth", Pig, held)!;
        }
        var plain = ModLoader.Build(GameContent.Staged(Host, "Diplomacy"));
        var varied = ModLoader.Build(GameContent.Staged(Host, "Diplomacy").Append(Variety.Value));
        Assert.Equal(Worth(plain, 0) + ResourceVarietyPoints, Worth(varied, 0));
        Assert.Equal(Worth(plain, 1) - ResourceVarietyPoints, Worth(varied, 1));
    }

    /// <summary>The ResourceVariety mod's points, as its source states them; the test mod's types load in their own context.</summary>
    private const int ResourceVarietyPoints = 200;

    [Fact]
    public void A_tech_valuer_changes_what_the_team_researches()
    {
        string Researching(ContentDb content, out string[] selectable)
        {
            var sim = Grassland(content);
            selectable = sim.Content.Get<TechDef>().All.Where(t => !t.Disable && sim.Rules().Research.ValidateSelection(sim, 0, t.Id).Success)
                .Select(t => t.Id).Order(StringComparer.Ordinal).ToArray();
            Call(NewAi(sim, "ResearchPlanner", NewAi(sim, "AiContext", sim, 0)), "Plan");
            return sim.TeamOf(0).Researching!;
        }
        string chosen = Researching(Plain.Value, out var selectable);
        string favored = selectable.Last(id => id != chosen);
        Assert.Equal(favored, Researching(With(Valuer<TechSubject>(t => t.Tech.Id == favored ? 10_000 : 0)), out _));
    }

    [Fact]
    public void A_civic_valuer_makes_the_ai_adopt_a_civic_for_an_effect_only_its_mod_reads()
    {
        // Tribunes (CivicExtension) carries the levy key, which only that mod's rule reads. The probe values a levied
        // gold a turn at 100 points, as the levy's owner would.
        const string tribunes = "civic_extension:tribunes";
        var extension = ModLoader.FromDirectory(Path.Combine(Host, "test-mods", "CivicExtension"));
        IReadOnlyList<string> Adopted(ContentDb content)
        {
            var sim = Simulation.CreateScenario(content, GameFactory.Settings(5, "peoples:leader_julius_caesar", "peoples:leader_julius_caesar"),
                new(12, 8, true), Enumerable.Range(0, 96).Select(_ => new Plot { Terrain = "world:grass" }).ToArray(), _ => { });
            AddCity(sim, 0, 4, 4).Population = 4;
            sim.State.Turn = 40;
            sim.TeamOf(0).Techs.UnionWith(["research:code_of_laws", "research:monarchy"]);
            var review = Call(NewAi(sim, "CivicPlanner", NewAi(sim, "AiContext", sim, 0)), "Evaluate")!;
            return Property<List<string>>(review, "Adopt");
        }
        Assert.DoesNotContain(tribunes, Adopted(ModLoader.Build(Shipped.Value.Append(extension))));
        var levy = Valuer<CivicSubject>(c => (int)c.Civic.Effects.Total(ModifierScope.Player, "civic_extension:levy") * 100);
        Assert.Contains(tribunes, Adopted(ModLoader.Build(Shipped.Value.Append(extension).Append(levy))));
    }

    [Fact]
    public void A_building_valuer_changes_what_a_city_builds()
    {
        (string Kind, string Id) Head(ContentDb content, out string[] buildable)
        {
            var sim = Grassland(content);
            sim.State.Turn = 60;
            var city = sim.State.Cities[0];
            city.Population = 1;
            sim.TeamOf(0).Techs.UnionWith(["research:ritualism", "research:stone_tools", "research:storytelling", "research:agriculture"]);
            for (int i = 0; i < 3; i++) AddUnit(sim, 0, Warrior, 4, 4);
            buildable = Buildable(sim, city).ToArray();
            CityPlan(sim);
            return (city.ProductionQueue[0].Kind, city.ProductionQueue[0].ContentId);
        }
        var chosen = Head(Plain.Value, out var buildable);
        string favored = buildable.FirstOrDefault(id => chosen != ("cities:building", id))
            ?? throw new Xunit.Sdk.XunitException($"chose {chosen}; buildable: {string.Join(", ", buildable)}");
        Assert.Equal(("cities:building", favored), Head(With(Valuer<BuildingSubject>(b => b.Building.Id == favored ? 1_000 : 0)), out _));
    }

    [Fact]
    public void A_unit_valuer_changes_which_unit_fills_a_role()
    {
        // An empty city needs a defender, and the warrior defends better than the probe's militia.
        const string militia = "militia_probe:militia";
        var militiaProbe = Probe("militia_probe", context => context.Content<UnitDef>().Register(new()
        {
            Id = militia, Name = "Militia", DefaultUnit = militia, Domain = "military:domain_land", Cost = 30, Strength = 1, Moves = 1,
        }));
        string Defender(ContentDb content)
        {
            var sim = Grassland(content);
            sim.State.Turn = 30;
            CityPlan(sim);
            var head = sim.State.Cities[0].ProductionQueue[0];
            Assert.Equal("cities:unit", head.Kind);
            return head.ContentId;
        }
        Assert.Equal(Warrior, Defender(With(militiaProbe)));
        Assert.Equal(militia, Defender(With(militiaProbe, Valuer<UnitSubject>(u => u.Unit.Id == militia ? 200 : 0))));
    }

    /// <summary>Buildings the city planner could consider in <paramref name="city"/>, by ordinal id: legal, not a
    /// wonder, and not a civilization's replacement or a religious building.</summary>
    private static IEnumerable<string> Buildable(Simulation sim, City city)
    {
        var civilization = sim.Content.Get<CivilizationDef>(sim.State.PlayerById(0).Civilization);
        return sim.Content.Get<BuildingDef>().All
            .Where(b => b.Cost > 0 && !CityBuildingRules.IsWonder(b) && !b.Capital && !b.RequiresReligion && !b.RequiresStateReligion
                && b.ReligionType is null && b.ObsoleteTech is null
                && civilization.BuildingOverrides.GetValueOrDefault(b.DefaultBuilding, b.DefaultBuilding) == b.Id
                && sim.Rules().CityProduction.Validate(sim, city.Id, "cities:building", b.Id).Success)
            .Select(b => b.Id).Order(StringComparer.Ordinal);
    }

    private static void CityPlan(Simulation sim)
    {
        var ctx = NewAi(sim, "AiContext", sim, 0);
        Call(NewAi(sim, "CityPlanner", ctx, NewAi(sim, "SitePlanner", ctx), NewAi(sim, "ArmyPlan", ctx)), "Plan");
    }

    /// <summary>Open grassland, every plot revealed and owned by player 0, with one city of three at (4, 4).</summary>
    private static Simulation Grassland(ContentDb content)
    {
        var sim = Simulation.CreateScenario(content, GameFactory.Settings(71), new(12, 8, true),
            Enumerable.Range(0, 96).Select(_ => new Plot { Terrain = "world:grass" }).ToArray(), game =>
            {
                GameFactory.NeutralPolitics(game);
                foreach (var plot in game.State.Plots) { plot.Owner = 0; plot.Revealed.Add(game.TeamOf(0).Id); }
            });
        AddCity(sim, 0, 4, 4);
        return sim;
    }

    /// <summary>A city at (2, 3) working nothing by hand, a pastured pig on a road at (1, 3), a bare pig at (3, 3), a cow at
    /// (4, 3), and a worker in the city; the team knows hunting and animal husbandry.</summary>
    private static Simulation Pastures(ContentDb content, out int worker)
    {
        var sim = Simulation.CreateScenario(content, GameFactory.Settings(71), new(12, 6, true),
            Enumerable.Range(0, 72).Select(_ => new Plot { Terrain = "world:grass" }).ToArray(), game =>
            {
                GameFactory.NeutralPolitics(game);
                game.TeamOf(0).Techs.UnionWith(["research:hunting", "research:animal_husbandry"]);
                foreach (var plot in game.State.Plots) { plot.Owner = 0; plot.Revealed.Add(game.TeamOf(0).Id); }
            });
        var city = AddCity(sim, 0, 2, 3);
        city.Extensions.Set(sim.Content, new CityRuleState { ManualWork = true });
        var held = sim.State.PlotAt(HexGrid.FromOffset(1, 3));
        held.Bonus = Pig;
        held.Improvement = "world:improvement_improvement_pasture";
        held.Route = "world:route_route_road";
        sim.State.PlotAt(HexGrid.FromOffset(3, 3)).Bonus = Pig;
        sim.State.PlotAt(HexGrid.FromOffset(4, 3)).Bonus = Cow;
        worker = AddUnit(sim, 0, "military:worker", 2, 3).Id;
        return sim;
    }

    private static TileId Tile(Simulation sim, int column, int row) => sim.State.Grid.TileAt(HexGrid.FromOffset(column, row));

    private static City AddCity(Simulation sim, int owner, int column, int row)
    {
        var city = new City { Id = sim.State.NextCityId++, Owner = owner, Position = HexGrid.FromOffset(column, row), Population = 3 };
        sim.State.AddCity(city);
        sim.State.PlotAt(city.Position).Owner = owner;
        return city;
    }

    private static Unit AddUnit(Simulation sim, int owner, string type, int column, int row)
    {
        var unit = new Unit { Id = sim.State.NextUnitId++, Owner = owner, Type = type, Position = HexGrid.FromOffset(column, row) };
        sim.State.AddUnit(unit);
        sim.Rules().Units.Initialize(sim, unit.Id);
        return unit;
    }
}
