using System.Reflection;
using System.Runtime.Loader;
using Ariel.Ai;
using Ariel.Client;
using Ariel.Composition;
using Ariel.Content;
using Ariel.Game;
using Ariel.Modding;
using Ariel.Mods.Corporations;
using Ariel.Mods.Espionage;
using Ariel.Mods.ProjectsVictory;
using Ariel.Mods.Revolution;
using Ariel.Mods.ScriptedWonders;
using Ariel.Mods.Trade;
using Ariel.Rules;
using Xunit;

namespace Ariel.Mods.Tests;

/// <summary>A mod publishes an API assembly; other mods, the client and tests share one copy of it.</summary>
public sealed class ModApiTests
{
    private const string GreetingApi = "Ariel.TestMods.ApiProvider.Api";
    private static readonly string TestMods = Path.Combine(AppContext.BaseDirectory, "test-mods");

    private static ContentDb Load(params string[] enabled)
    {
        var discovery = ModDiscovery.Discover(TestMods);
        Assert.Empty(discovery.Errors);
        return ModLoader.Build(ModDiscovery.LoadMods(discovery.Mods, enabled));
    }

    /// <summary>What the consumer mod found under its own compile-time view of the provider's interface.</summary>
    private static object? Probe(ContentDb content) => content.Services.Find<Func<ContentDb, object?>>()!(content);

    private static Assembly[] LoadedCopies(string name) =>
        AssemblyLoadContext.All.SelectMany(context => context.Assemblies).Where(a => a.GetName().Name == name).Distinct().ToArray();

    [Fact]
    public void A_consumer_mod_finds_the_service_another_mod_publishes_through_one_shared_copy_of_its_api()
    {
        // No host here references the test API: provider and consumer each carry a copy in their own folder.
        var content = Load("apiconsumer", "apiprovider");

        var greeting = Probe(content);
        Assert.NotNull(greeting);
        var api = Assert.Single(LoadedCopies(GreetingApi));
        Assert.Same(api, ModApis.Find(new AssemblyName(GreetingApi)));
        var contract = api.GetType("Ariel.TestMods.ApiProvider.IGreeting", throwOnError: true)!;
        Assert.True(contract.IsInstanceOfType(greeting));
        Assert.Equal("hello", contract.GetMethod("Hello")!.Invoke(greeting, null));
        // The one copy lives in the shared API context, in neither mod's own load context.
        Assert.Equal("Ariel mod APIs", AssemblyLoadContext.GetLoadContext(api)!.Name);
    }

    [Fact]
    public void A_consumer_mod_loads_without_the_provider_and_finds_no_service()
    {
        var content = Load("apiconsumer");

        Assert.Equal(new[] { "apiconsumer" }, content.LoadedMods);
        Assert.Null(Probe(content));
        Assert.Single(LoadedCopies(GreetingApi));
    }

    [Fact]
    public void A_provider_must_mark_the_api_its_manifest_names()
    {
        string dir = Path.Combine(TestMods, "ApiConsumer");
        var manifest = ModLoader.ReadManifest(dir);
        var unmarked = new ModManifest { Id = manifest.Id, Name = manifest.Name, Assembly = manifest.Assembly, Api = manifest.Assembly };
        Assert.Contains("ModApi(\"apiconsumer\")", Assert.Throws<InvalidDataException>(() => ModApis.Publish(dir, unmarked)).Message);
    }

    [Fact]
    public void The_host_compiled_against_a_mod_api_shares_its_own_copy_with_the_loaded_mod()
    {
        var content = GameContent.Build(Path.Combine(AppContext.BaseDirectory, GameContent.RulesFolder),
            ModLoader.FromDirectories(new[] { "Corporations" }
                .Select(name => Path.Combine(AppContext.BaseDirectory, GameContent.OptionalModsFolder, name))));

        Assert.Same(typeof(ICorporationService).Assembly, ModApis.Find(typeof(ICorporationService).Assembly.GetName()));
        Assert.Single(LoadedCopies(typeof(ICorporationService).Assembly.GetName().Name!));
        var corporations = content.Services.Find<ICorporationService>();
        Assert.NotNull(corporations);
        Assert.Contains(corporations, content.Rules().TrainingRules.Cast<object>());
        Assert.Same(corporations, content.Services.Find<ICorporationService>());
    }

    [Fact]
    public void A_mod_contributing_an_ai_planner_binds_the_hosts_copy_of_the_computer_player()
    {
        var mods = ModLoader.FromDirectories(new[] { "Corporations" }
            .Select(name => Path.Combine(AppContext.BaseDirectory, GameContent.OptionalModsFolder, name)));
        var corporations = mods.Single(s => s.Manifest.Id == "corporations");

        var context = AssemblyLoadContext.GetLoadContext(corporations.Instance.GetType().Assembly)!;
        Assert.Same(typeof(IAiPlanner).Assembly, context.LoadFromAssemblyName(typeof(IAiPlanner).Assembly.GetName()));
        var content = GameContent.Build(Path.Combine(AppContext.BaseDirectory, GameContent.RulesFolder), mods);
        Assert.Contains(content.Services.Get<IAiPlanner>(), p => p.Id == "corporations:planner");
    }

    [Fact]
    public void Feature_mods_answer_the_base_seams_with_the_services_they_publish()
    {
        string[] optional = ["Espionage", "Revolution", "ScriptedWonders", "ProjectsVictory", "Corporations", "Votes"];
        string[] main = ["GreatPeople", "Religion", "Trade", "Barbarians"];
        var content = GameContent.Build(Path.Combine(AppContext.BaseDirectory, GameContent.RulesFolder),
            ModLoader.FromDirectories(optional.Select(name => Path.Combine(AppContext.BaseDirectory, GameContent.OptionalModsFolder, name))
                .Concat(main.Select(name => Path.Combine(AppContext.BaseDirectory, GameContent.ModsFolder, name)))));
        var rules = content.Rules();

        Assert.NotNull(content.Services.Find<IEspionageService>());
        Assert.Equal("espionage:commerce_espionage", content.Commerce[EspionageCommerce.Key].Id);
        Assert.Same(content.Services.Find<IRevolutionService>(), rules.CityUnrest);
        Assert.Same(content.Services.Find<ITradeRouteService>(), rules.TradeLinks);
        Assert.Contains(rules.VictoryConditions, c => c.Id == ProjectVictories.Spaceship);
        Assert.Contains(rules.VictoryConditions, c => c.Id == "votes:council");
        Assert.Contains(content.Services.Find<Ariel.Mods.Votes.IVotesService>(), rules.CivicRestrictions.Cast<object>());
        Assert.Contains(content.Services.Find<Ariel.Mods.Votes.IVotesService>(), rules.TradeTerms.Cast<object>());
        Assert.NotNull(rules.CombatLeaders);
        Assert.Contains(content.Services.Find<IScriptedWonderService>(), rules.FreeBuildingGrants.Cast<object>());
        Assert.NotNull(GameFactory.BarbarianFaction(content));
        Assert.Contains(content.Services.Find<IScriptedWonderService>(), rules.MovementBarriers.Cast<object>());
        // Religion, Corporations and GreatPeople each weigh a city as a new capital.
        Assert.Equal(3, rules.CapitalWeights.Count);
        Assert.Contains(content.Services.Get<IProductionWishes>(), w => w.Id == "espionage:spies");
    }

    [Fact]
    public void Without_the_provider_the_base_seam_and_the_mod_service_are_absent()
    {
        var content = GameContent.Build(Path.Combine(AppContext.BaseDirectory, GameContent.RulesFolder), []);

        Assert.Null(content.Services.Find<ICorporationService>());
        Assert.Empty(content.Rules().TrainingRules);
        Assert.Throws<InvalidOperationException>(() => content.Services.Find<IModifierSource>());
    }

    [Fact]
    public void Without_the_Corporations_mod_the_base_game_names_no_corporation()
    {
        // Every corporation, its founding buildings, stores, building kinds, executives, leaf kind and game option are the
        // mod's: the base rules and the main mods load and run without a trace of them.
        var content = GameContent.Build(Path.Combine(AppContext.BaseDirectory, GameContent.RulesFolder),
            ModLoader.FromDirectories(Directory.GetDirectories(Path.Combine(AppContext.BaseDirectory, GameContent.ModsFolder))));
        Assert.DoesNotContain(content.Services.Get<IRequirementEvaluator>(), e => e.Kind == "corporation");
        Assert.DoesNotContain(content.Get<Ariel.Content.Schema.BuildingDef>().All, b => b.Founds?.Kind == "corporation");
        Assert.DoesNotContain(SetupPolicies.GameOptions(content), o => o.Id == CorporationMode.Option);
        var folders = new[] { GameContent.RulesFolder, GameContent.ModsFolder }.Select(f => Path.Combine(AppContext.BaseDirectory, f));
        foreach (var file in folders.SelectMany(f => Directory.GetFiles(f, "*.json", SearchOption.AllDirectories)))
        {
            string text = File.ReadAllText(file);
            Assert.False(text.Contains("\"corporations:", StringComparison.Ordinal), $"{file} names the Corporations mod's content");
            Assert.False(text.Contains("\"kind\": \"corporation\"", StringComparison.Ordinal), $"{file} has a corporation requirement");
        }
        Assert.Empty(Directory.GetFiles(Path.Combine(AppContext.BaseDirectory, GameContent.RulesFolder), "corporation*.json", SearchOption.AllDirectories));
    }
}
