using System.Collections.Immutable;
using Ariel.Content;
using Ariel.Modding;
using Xunit;

namespace Ariel.Tests;

/// <summary>Attachment fields that amend their target (<see cref="AmendsTargetAttribute"/>) and optional content
/// providers (<see cref="ModManifest.OptionalReferences"/>).</summary>
public class AmendmentTests
{
    public sealed class CoinDef : ContentDef, ICommerceType
    {
        public string Key { get; init; } = "";
        public int Order { get; init; }
    }

    public sealed class ShopDef : ContentDef
    {
        public CommerceVector Commerce { get; init; }
        [ContentReference(typeof(CoinDef), ReferencePart.Keys)]
        public ImmutableDictionary<string, int> Slots { get; init; } = ImmutableDictionary<string, int>.Empty;
        public ImmutableArray<string> Tags { get; init; } = [];
        public int Size { get; init; }
    }

    public sealed class ShopExtraDef : ContentAttachment<ShopDef>
    {
        [AmendsTarget] public CommerceVector Commerce { get; init; }
        [AmendsTarget] public ImmutableDictionary<string, int> Slots { get; init; } = ImmutableDictionary<string, int>.Empty;
        [AmendsTarget] public ImmutableArray<string> Tags { get; init; } = [];
        /// <summary>Read through the attachment, not merged.</summary>
        public bool Secret { get; init; }
    }

    public sealed class ShopAgentsDef : ContentAttachment<ShopDef>
    {
        [AmendsTarget] public ImmutableArray<string> Tags { get; init; } = [];
    }

    public sealed class ShopSizeDef : ContentAttachment<ShopDef>
    {
        [AmendsTarget] public int Size { get; init; }
    }

    /// <summary>Adds requirement alternatives to its target, beside an alternative the target already has.</summary>
    public sealed class ShopEntryDef : ContentAttachment<ShopDef>
    {
        [AmendsTarget(nameof(Requirements))]
        public ImmutableDictionary<string, ImmutableArray<Requirement>> Alternatives { get; init; } =
            ImmutableDictionary<string, ImmutableArray<Requirement>>.Empty;
    }

    /// <summary>A leaf kind that passes for the ids listed.</summary>
    private sealed class Passes(params string[] ids) : IRequirementEvaluator
    {
        public string Kind => "pass";
        public string? Evaluate(Requirement.Leaf leaf, RequirementContext context) => ids.Contains(leaf.Id) ? null : "no";
    }

    private static Requirement.Leaf Pass(string id) => new("pass", id);

    /// <summary>A gated shop: one of a key or a pass, and one of a seal.</summary>
    private static ModSource GatedBase(params string[] passing) => Src("base", ctx =>
    {
        ctx.RegisterService<IRequirementEvaluator>("pass", new Passes(passing));
        ctx.Content<ShopDef>().Register(new ShopDef
        {
            Id = "base:shop",
            Requirements = new Requirement.All([new Requirement.Any([Pass("base:key"), Pass("base:pass")]), new Requirement.Any([Pass("base:seal")])]),
        });
    });

    private static ModSource Entries(string beside, params Requirement[] added) => Src("entries", ctx =>
        ctx.Content<ShopEntryDef>().Register(new ShopEntryDef
        {
            Id = "entries:shop", Target = "base:shop",
            Alternatives = ImmutableDictionary<string, ImmutableArray<Requirement>>.Empty.Add(beside, [.. added]),
        }), references: ["base"]);

    [Fact]
    public void RequirementAlternativesJoinTheAnyGroupThatHoldsTheirNeighbour()
    {
        var db = ModLoader.Build([GatedBase(), Entries("base:pass", Pass("entries:token"))]);
        var gate = Assert.IsType<Requirement.All>(db.Get<ShopDef>("base:shop").Requirements);
        Assert.Equal([Pass("base:key"), Pass("base:pass"), Pass("entries:token")], Assert.IsType<Requirement.Any>(gate.Items[0]).Items);
        Assert.Equal([Pass("base:seal")], Assert.IsType<Requirement.Any>(gate.Items[1]).Items);
        // The added alternative passes the group; the other group still applies.
        var passing = ModLoader.Build([GatedBase("entries:token", "base:seal"), Entries("base:pass", Pass("entries:token"))]);
        Assert.True(passing.Requirements.Evaluate(passing.Get<ShopDef>("base:shop").Requirements, new(null!, 0)).Success);
        var sealless = ModLoader.Build([GatedBase("entries:token"), Entries("base:pass", Pass("entries:token"))]);
        Assert.False(sealless.Requirements.Evaluate(sealless.Get<ShopDef>("base:shop").Requirements, new(null!, 0)).Success);
    }

    [Fact]
    public void RequirementAlternativesBesideNoAlternativeFailTheLoad()
    {
        var error = Assert.Throws<InvalidDataException>(() => ModLoader.Build([GatedBase(), Entries("base:gate", Pass("entries:token"))]));
        Assert.Contains("entries:shop.Alternatives[base:gate]: no 'any' group of the target holds the alternative 'base:gate'", error.Message);
    }

    /// <summary>A leaf kind naming a shop, so its ids are content references.</summary>
    private sealed class ShopLeaves : IContentRequirementEvaluator
    {
        public string Kind => "shop";
        public Type DefinitionType => typeof(ShopDef);
        public string? Evaluate(Requirement.Leaf leaf, RequirementContext context) => null;
    }

    [Fact]
    public void RequirementAlternativesNamingAnUnknownDefinitionFailTheLoad()
    {
        var gated = Src("base", ctx =>
        {
            ctx.RegisterService<IRequirementEvaluator>("pass", new Passes());
            ctx.RegisterService<IRequirementEvaluator>("shop", new ShopLeaves());
            ctx.Content<ShopDef>().Register(new ShopDef { Id = "base:shop", Requirements = new Requirement.Any([Pass("base:key")]) });
        });
        var error = Assert.Throws<InvalidDataException>(() => ModLoader.Build([gated, Entries("base:key", new Requirement.Leaf("shop", "base:nowhere"))]));
        Assert.StartsWith("Content reference validation failed:\nentries:shop.Alternatives", error.Message);
        Assert.Contains("base:nowhere", error.Message);
    }

    private sealed class TestMod(Action<IModContext> body) : IMod
    {
        public void Register(IModContext ctx) => body(ctx);
    }

    private static ModSource Src(string id, Action<IModContext> body, string[]? references = null, string[]? optional = null) => new()
    {
        Manifest = new ModManifest { Id = id, References = references ?? [], OptionalReferences = optional ?? [] },
        Instance = new TestMod(body),
        Root = AppContext.BaseDirectory,
    };

    private static ModSource Base() => Src("base", ctx =>
    {
        ctx.Content<CoinDef>().Register(new CoinDef { Id = "base:gold", Key = "gold" });
        ctx.Content<ShopDef>().Register(new ShopDef
        {
            Id = "base:shop", Commerce = CommerceVector.Of(("gold", 2)), Slots = ImmutableDictionary<string, int>.Empty.Add("base:gold", 1),
            Tags = ["base"], Size = 3,
        });
    });

    private static ModSource Spies(params ShopExtraDef[] rows) => Src("spies", ctx =>
    {
        ctx.Content<CoinDef>().Register(new CoinDef { Id = "spies:intel", Key = "intel", Order = 1 });
        foreach (var row in rows) ctx.Content<ShopExtraDef>().Register(row);
    }, references: ["base"]);

    [Fact]
    public void AmendingFieldsAddToTheTargetWhichKeepsItsOwner()
    {
        var db = ModLoader.Build([Base(), Spies(new ShopExtraDef
        {
            Id = "spies:shop", Target = "base:shop", Commerce = CommerceVector.Of(("intel", 4)),
            Slots = ImmutableDictionary<string, int>.Empty.Add("spies:intel", 2), Tags = ["covert"], Secret = true,
        })]);
        var shop = db.Get<ShopDef>("base:shop");
        Assert.Equal(2, shop.Commerce["gold"]);
        Assert.Equal(4, shop.Commerce["intel"]);
        // The merged vector carries the database's layout, so it combines with state vectors slot by slot.
        Assert.Same(db.Commerce.Layout, shop.Commerce.Layout);
        Assert.Equal(new Dictionary<string, int> { ["base:gold"] = 1, ["spies:intel"] = 2 }, shop.Slots);
        Assert.Equal(["base", "covert"], shop.Tags.ToArray());
        Assert.Equal(3, shop.Size);
        Assert.Equal("base", db.Get<ShopDef>().OwnerOf("base:shop"));
        Assert.True(db.Attachment<ShopExtraDef>("base:shop")!.Secret);
    }

    [Fact]
    public void RowsFromTwoModsApplyInIdOrder()
    {
        // Each mod amends through its own attachment type: one type has one row per target.
        var other = Src("agents", ctx => ctx.Content<ShopAgentsDef>().Register(new ShopAgentsDef { Id = "agents:shop", Target = "base:shop", Tags = ["agents"] }),
            references: ["base"]);
        var db = ModLoader.Build([Base(), other, Spies(new ShopExtraDef { Id = "spies:shop", Target = "base:shop", Tags = ["spies"] })]);
        Assert.Equal(["base", "agents", "spies"], db.Get<ShopDef>("base:shop").Tags.ToArray());
    }

    [Fact]
    public void AMapKeyTheTargetAlreadyHasFailsTheLoad()
    {
        var error = Assert.Throws<InvalidDataException>(() => ModLoader.Build([Base(), Spies(new ShopExtraDef
        {
            Id = "spies:shop", Target = "base:shop", Slots = ImmutableDictionary<string, int>.Empty.Add("base:gold", 5),
        })]));
        Assert.Contains("spies:shop.Slots: 'base:gold' is already in the target", error.Message);
    }

    [Fact]
    public void AScalarFieldCannotAmend()
    {
        var sizes = Src("sizes", ctx => ctx.Content<ShopSizeDef>().Register(new ShopSizeDef { Id = "sizes:shop", Target = "base:shop", Size = 1 }),
            references: ["base"]);
        var error = Assert.Throws<InvalidDataException>(() => ModLoader.Build([Base(), sizes]));
        Assert.Contains("a field of type Int32 cannot amend its target", error.Message);
    }

    [Fact]
    public void AnAmendmentIsValidatedAgainstItsOwnMod()
    {
        // The amending mod names a commerce type and a slot key whose provider it does not declare.
        var coins = Src("coins", ctx => ctx.Content<CoinDef>().Register(new CoinDef { Id = "coins:silver", Key = "silver", Order = 2 }));
        var spies = Src("spies", ctx => ctx.Content<ShopExtraDef>().Register(new ShopExtraDef
        {
            Id = "spies:shop", Target = "base:shop", Commerce = CommerceVector.Of(("silver", 1)),
            Slots = ImmutableDictionary<string, int>.Empty.Add("coins:silver", 1),
        }), references: ["base"]);
        var error = Assert.Throws<InvalidDataException>(() => ModLoader.Build([Base(), coins, spies]));
        Assert.Contains("spies:shop.Commerce: mod 'spies' must declare commerce type provider 'coins'", error.Message);
        Assert.Contains("spies:shop.Slots: mod 'spies' must declare content provider 'coins'", error.Message);
    }

    [Fact]
    public void AnOptionalReferenceNeedNotBeLoadedAndCountsAsAReferenceWhenItIs()
    {
        bool? sawShops = null;
        var spies = Src("spies", ctx =>
        {
            sawShops = ctx.IsLoaded("base");
            ctx.Content<CoinDef>().Register(new CoinDef { Id = "spies:intel", Key = "intel" });
            if (ctx.IsLoaded("base"))
                ctx.Content<ShopExtraDef>().Register(new ShopExtraDef { Id = "spies:shop", Target = "base:shop", Commerce = CommerceVector.Of(("intel", 1)) });
        }, optional: ["base"]);

        var alone = ModLoader.Build([spies]);
        Assert.False(sawShops);
        Assert.False(alone.TryGet<ShopExtraDef>("spies:shop", out _));

        var both = ModLoader.Build([Base(), spies]);
        Assert.True(sawShops);
        Assert.Equal(1, both.Get<ShopDef>("base:shop").Commerce["intel"]);
    }
}
