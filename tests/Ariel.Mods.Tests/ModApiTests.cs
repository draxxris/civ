using System.Reflection;
using System.Runtime.Loader;
using Ariel.Ai;
using Ariel.Client;
using Ariel.Composition;
using Ariel.Content;
using Ariel.Game;
using Ariel.Modding;
using Ariel.Mods.Corporations;
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
            ModLoader.FromDirectories(new[] { "Corporations", "Diplomacy" }
                .Select(name => Path.Combine(AppContext.BaseDirectory, GameContent.OptionalModsFolder, name))));

        Assert.Same(typeof(ICorporationService).Assembly, ModApis.Find(typeof(ICorporationService).Assembly.GetName()));
        Assert.Single(LoadedCopies(typeof(ICorporationService).Assembly.GetName().Name!));
        var corporations = content.Services.Find<ICorporationService>();
        Assert.NotNull(corporations);
        Assert.Same(corporations, content.Rules().CityCorporations);
        Assert.Same(corporations, content.Services.Find<ICorporationService>());
    }

    [Fact]
    public void A_mod_contributing_an_ai_planner_binds_the_hosts_copy_of_the_computer_player()
    {
        var mods = ModLoader.FromDirectories(new[] { "Corporations", "Diplomacy" }
            .Select(name => Path.Combine(AppContext.BaseDirectory, GameContent.OptionalModsFolder, name)));
        var corporations = mods.Single(s => s.Manifest.Id == "corporations");

        var context = AssemblyLoadContext.GetLoadContext(corporations.Instance.GetType().Assembly)!;
        Assert.Same(typeof(IAiPlanner).Assembly, context.LoadFromAssemblyName(typeof(IAiPlanner).Assembly.GetName()));
        var content = GameContent.Build(Path.Combine(AppContext.BaseDirectory, GameContent.RulesFolder), mods);
        Assert.Contains(content.Services.Get<IAiPlanner>(), p => p.Id == "corporations:planner");
    }

    [Fact]
    public void Without_the_provider_the_base_seam_and_the_mod_service_are_absent()
    {
        var content = GameContent.Build(Path.Combine(AppContext.BaseDirectory, GameContent.RulesFolder), []);

        Assert.Null(content.Services.Find<ICorporationService>());
        Assert.Null(content.Rules().CityCorporations);
        Assert.Throws<InvalidOperationException>(() => content.Services.Find<IModifierSource>());
    }
}
