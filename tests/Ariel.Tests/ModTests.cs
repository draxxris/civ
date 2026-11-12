using Ariel.BaseGame;
using Ariel.Content;
using Ariel.Modding;
using Xunit;

namespace Ariel.Tests;

public class ModTests
{
    private sealed class TestMod(Action<IModContext> body) : IMod
    {
        public void Register(IModContext ctx) => body(ctx);
    }

    private static ModSource Src(string id, Action<IModContext> body, params string[] deps) => new()
    {
        Manifest = new ModManifest { Id = id, Dependencies = deps },
        Instance = new TestMod(body),
        Root = AppContext.BaseDirectory,
    };

    [Fact]
    public void ModsLoadInDependencyOrderAndCanOverrideBase()
    {
        var baseSrc = ModLoader.FromAssembly(new ModManifest { Id = "base" }, typeof(BaseGame.TestRulesetMod).Assembly, AppContext.BaseDirectory);
        var extra = Src("extra", ctx =>
        {
            ctx.Content<TerrainDef>().Register(new TerrainDef { Id = "extra:swamp", Name = "Swamp", Food = 1 });
            ctx.Content<TerrainDef>().Override(new TerrainDef { Id = "base:desert", Name = "Dune", Color = "#ffffff" });
        }, "base");
        var db = ModLoader.Build([extra, baseSrc]); // given out of order
        Assert.Equal(["base", "extra"], db.LoadedMods);
        Assert.Equal("Dune", db.Get<TerrainDef>().Get("base:desert").Name);
        Assert.Equal("extra", db.Get<TerrainDef>().OwnerOf("base:desert"));
    }

    [Fact]
    public void IndependentDependenciesRegisterInOrdinalOrderRegardlessOfDeclarationOrder()
    {
        var calls = new List<string>();
        var a = Src("a", _ => calls.Add("a"), "c", "b");
        var b = Src("b", _ => calls.Add("b"));
        var c = Src("c", _ => calls.Add("c"));
        ModLoader.Build([a, c, b]);
        Assert.Equal(["b", "c", "a"], calls);
    }

    [Fact]
    public void RegisteringOutsideYourNamespaceIsRejected()
    {
        var bad = Src("bad", ctx => ctx.Content<TerrainDef>().Register(new TerrainDef { Id = "base:oops" }));
        Assert.Throws<ArgumentException>(() => ModLoader.Build([bad]));
    }

    [Fact]
    public void MissingDependencyIsRejected() =>
        Assert.Throws<InvalidOperationException>(() => ModLoader.Build([Src("a", _ => { }, "nope")]));

    [Fact]
    public void BaseModLoadsFromItsFolder()
    {
        // The test bin directory contains the copied test-ruleset mod.json and data/.
        var src = ModLoader.FromDirectory(AppContext.BaseDirectory);
        var db = ModLoader.Build([src]);
        // The folder copy loads in its own load context, so its TechDef is a different type from this assembly's:
        // look the registry up by the loaded copy's own type.
        var techDef = src.Instance.GetType().Assembly.GetType(typeof(TechDef).FullName!, throwOnError: true)!;
        var techs = typeof(ContentDb).GetMethod(nameof(ContentDb.Get), Type.EmptyTypes)!.MakeGenericMethod(techDef).Invoke(db, null)!;
        Assert.True((bool)techs.GetType().GetMethod("Contains")!.Invoke(techs, ["base:monarchy"])!);
        Assert.Equal(TestGame.Content().ContentHash, db.ContentHash);
    }
}
