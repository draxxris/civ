using Civ.Content;
using Civ.Modding;
using Xunit;

namespace Civ.Tests;

public class ModTests
{
    [Fact]
    public void BaseGameRegistersTerrainTechsBuildingsAndCulture()
    {
        var db = TestGame.Content();
        Assert.True(db.Terrains.Contains("base:grassland"));
        Assert.Equal("base", db.Terrains.OwnerOf("base:ocean"));
        Assert.Equal(7, db.Techs.Count);
        Assert.True(db.Buildings.Contains("base:library"));
        Assert.Equal("base:fledgling", db.CultureLevelFor(15)!.Id);
    }

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
            ctx.Terrains.Register(new TerrainDef { Id = "extra:swamp", Name = "Swamp", Food = 1 });
            ctx.Terrains.Override(new TerrainDef { Id = "base:desert", Name = "Dune", Color = "#ffffff" });
        }, "base");
        var db = ModLoader.Build([extra, baseSrc]); // given out of order
        Assert.Equal(["base", "extra"], db.LoadedMods);
        Assert.Equal("Dune", db.Terrains.Get("base:desert").Name);
        Assert.Equal("extra", db.Terrains.OwnerOf("base:desert"));
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
        var bad = Src("bad", ctx => ctx.Terrains.Register(new TerrainDef { Id = "base:oops" }));
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
        Assert.True(db.Techs.Contains("base:monarchy"));
        Assert.Equal(TestGame.Content().ContentHash, db.ContentHash);
    }
}
