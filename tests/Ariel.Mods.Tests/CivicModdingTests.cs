using System.Text.Json;
using Ariel.Client;
using Ariel.Composition;
using Ariel.Content;
using Ariel.Game;
using Ariel.Modding;
using Ariel.Mods.Trade;
using Xunit;
using static Ariel.Mods.Tests.AiBindings;
using S = Ariel.Content.Schema;

namespace Ariel.Mods.Tests;

/// <summary>Civics are moddable: open effects carry any declared modifier key, and a mod attaches its own data to a
/// civic without a field on <see cref="S.CivicDef"/>. The CivicExtension test mod adds a civic to the base government
/// option and a whole new option, using a base key and a key of its own.</summary>
public sealed class CivicModdingTests(LoadedMods mods) : IClassFixture<LoadedMods>
{
    private const string Tribunes = "civic_extension:tribunes", Charter = "civic_extension:civicoption_charter";
    private const string Caesar = "peoples:leader_julius_caesar";
    private static string Rules => Path.Combine(AppContext.BaseDirectory, GameContent.RulesFolder);
    private static ModSource Extension => ModLoader.FromDirectory(Path.Combine(AppContext.BaseDirectory, "test-mods", "CivicExtension"));

    /// <summary>Every shipped mod and the extension.</summary>
    private static readonly Lazy<ContentDb> Everything = new(() =>
        ModLoader.Build(GameContent.Staged(AppContext.BaseDirectory).Append(Extension).Reverse()));

    private static T Service<T>(Simulation sim) where T : class => sim.Content.Services.Get<T>().Single();

    private static Simulation Create(ContentDb content, int players = 2)
    {
        var sim = new Simulation(content, new SimulationSettings
        {
            Seed = 5, WorldSize = "foundation:worldsize_standard", MapGenerator = "world:continents",
            GameSpeed = "foundation:gamespeed_normal", Handicap = "foundation:handicap_noble",
            StartingEra = "foundation:era_ancient", PlayerCount = players, Leaders = [.. Enumerable.Repeat(Caesar, players)],
        });
        return sim;
    }

    private static City AddCity(Simulation sim, int owner = 0)
    {
        var tile = sim.State.Players[owner].StartingPlot!.Value;
        var city = new City { Id = sim.State.NextCityId++, Owner = owner, Position = sim.State.Grid.HexOf(tile), Name = "T" };
        sim.State.AddCity(city);
        return city;
    }

    private static void FinishAnarchy(Simulation sim)
    {
        for (int turn = 0; turn < 10 && GovernmentStatus.IsAnarchy(sim, 0); turn++) sim.AdvanceTurn();
        Assert.False(GovernmentStatus.IsAnarchy(sim, 0));
    }

    // The levy log lives in the test mod's own load context, so read it as saved.
    private static long Levied(Simulation sim) =>
        JsonSerializer.SerializeToElement(sim.State.Players[0].Extensions, CanonicalJson.Options)
            .TryGetProperty("civic_extension:levy_log", out var log) ? log.GetProperty("Collected").GetInt64() : 0;

    [Fact]
    public void A_mod_civic_in_a_base_option_is_adopted_and_applies_a_base_and_a_mod_effect()
    {
        var sim = Create(Everything.Value);
        var city = AddCity(sim);
        bool FromTribunes() => Service<ICityWellbeingService>(sim).Calculate(sim, city.Id).Ledger.Lines
            .Contains(new LedgerLine(Tribunes, "cities:happiness_good", 2));
        Assert.False(FromTribunes());

        Assert.True(sim.TryApply(new SetCivicsCommand { Player = 0, Civics = [Tribunes] }, out var error), error);
        Assert.Equal(Tribunes, sim.State.Players[0].Civics["government:civicoption_government"]);
        FinishAnarchy(sim);

        // The base key reaches the base happiness rule; the mod's key reaches only the mod's levy rule.
        Assert.True(FromTribunes());
        Assert.Equal(3, ModifierSink.Collect(new(sim, ModifierScope.Player, 0)).Ledger.Total("civic_extension:levy"));
        long levied = Levied(sim);
        sim.AdvanceTurn();
        Assert.Equal(levied + 3, Levied(sim));
        Assert.Equal(0, ModifierSink.Collect(new(sim, ModifierScope.Player, 1)).Ledger.Total("civic_extension:levy"));
    }

    [Fact]
    public void A_mod_option_starts_on_its_default_and_switches_like_a_base_one()
    {
        var sim = Create(Everything.Value);
        var city = AddCity(sim);
        Assert.Equal("civic_extension:open_charter", sim.State.Players[0].Civics[Charter]);
        Assert.Equal(sim.Content.Get<S.CivicOptionDef>().All.Count(), sim.State.Players[0].Civics.Count);

        Assert.True(sim.TryApply(new SetCivicsCommand { Player = 0, Civics = ["civic_extension:closed_charter"] }, out var error), error);
        FinishAnarchy(sim);
        Assert.Contains(new LedgerLine("civic_extension:closed_charter", "cities:health_good", 1),
            Service<ICityWellbeingService>(sim).Calculate(sim, city.Id).Ledger.Lines);
    }

    [Fact]
    public void The_description_lists_base_and_mod_effects_and_attached_data()
    {
        var content = Everything.Value;
        var tribunes = ContentDescription.Of(content, content.Get<S.CivicDef>(Tribunes));
        Assert.Equal(["+2 happiness in each city", "+3 gold levied each turn"], tribunes);

        // A base civic lists its open effects and what each feature mod attached to it.
        var mercantilism = ContentDescription.Of(content, content.Get<S.CivicDef>(
            content.Get<CivicTradeDef>().All.First(c => c.NoForeignTrade).Target));
        Assert.Contains("No foreign trade routes", mercantilism);

        var entry = new PediaCatalog(content).Find(Tribunes)!;
        Assert.Equal(tribunes, entry.Effects);
        var session = new GameSession(Create(content));
        var choice = session.Government().Groups.Single(g => g.Id == "government:civicoption_government").Choices.Single(c => c.Id == Tribunes);
        Assert.Equal(tribunes, choice.Effects);
        Assert.Contains(session.Government().Groups, g => g.Id == Charter && g.Selected == "civic_extension:open_charter");
    }

    [Fact]
    public void The_ai_civic_planner_weighs_mod_civics_without_failing()
    {
        var sim = Create(Everything.Value);
        AddCity(sim).Population = 4;
        sim.State.Turn = 40;
        sim.TeamOf(0).Techs.UnionWith(["research:code_of_laws", "research:monarchy"]);
        var ctx = NewAi(sim, "AiContext", sim, 0);
        var review = Call(NewAi(sim, "CivicPlanner", ctx), "Evaluate")!;
        Assert.NotNull(Property<object?>(review, "Goal"));

        var game = GameFactory.CreateAi(Everything.Value, seed: 5, players: 2);
        for (int turn = 0; turn < 30; turn++) game.AdvanceTurn();
        Assert.All(game.State.Players, p => Assert.Contains(Charter, p.Civics.Keys));
    }

    [Fact]
    public void The_extension_loads_on_the_base_game_alone()
    {
        var content = GameContent.Build(Rules, [Extension]);
        Assert.Null(content.Attachment<CivicTradeDef>(Tribunes));
        Assert.Empty(content.AttachmentsOf(Tribunes));
        var sim = Create(content);
        Assert.True(sim.TryApply(new SetCivicsCommand { Player = 0, Civics = [Tribunes] }, out var error), error);
    }

    [Fact]
    public void An_undeclared_or_mistargeted_modifier_key_fails_the_load()
    {
        var undeclared = Assert.Throws<InvalidDataException>(() => Probe(civic: Civic(new ModifierEffect
            { Scope = ModifierScope.City, Key = "probe:unknown", Amount = 1 })));
        Assert.Contains("'probe:unknown' is not declared", undeclared.Message);
        var untargeted = Assert.Throws<InvalidDataException>(() => Probe(civic: Civic(new ModifierEffect
            { Scope = ModifierScope.City, Key = "cities:combat_production_percent", Amount = 1 })));
        Assert.Contains("needs target", untargeted.Message);
        var typed = Assert.Throws<InvalidDataException>(() => Probe(civic: Civic(new ModifierEffect
            { Scope = ModifierScope.City, Key = "yield.percent.food", Target = "government:despotism", Amount = 1 })));
        Assert.Contains("takes no target", typed.Message);
    }

    [Fact]
    public void Attachments_are_validated_indexed_and_hashed()
    {
        var one = Probe(notes: [Note("probe:a", "government:despotism", 1)]);
        Assert.Equal(1, one.Attachment<ProbeNoteDef>("government:despotism")!.Weight);
        Assert.Contains(one.AttachmentsOf("government:despotism"), a => a.Id == "probe:a");
        var two = Probe(notes: [Note("probe:a", "government:despotism", 2)]);
        Assert.NotEqual(one.ContentHash, two.ContentHash);

        var missing = Assert.Throws<InvalidDataException>(() => Probe(notes: [Note("probe:a", "government:nothing", 1)]));
        Assert.Contains("'government:nothing' does not resolve", missing.Message);
        var wrongKind = Assert.Throws<InvalidDataException>(() => Probe(notes: [Note("probe:a", "research:monarchy", 1)]));
        Assert.Contains("'research:monarchy' does not resolve", wrongKind.Message);
        var duplicate = Assert.Throws<InvalidDataException>(() => Probe(notes:
            [Note("probe:a", "government:despotism", 1), Note("probe:b", "government:despotism", 2)]));
        Assert.Contains("both attach to 'government:despotism'", duplicate.Message);
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<CivicTradeDef[]>(
            """[{ "id": "trade:x", "target": "government:despotism", "routs": 1 }]""", ModLoader.Json));
    }

    private static S.CivicDef Civic(ModifierEffect effect) => new()
    {
        Id = "probe:civic", Name = "Probe", Option = "government:civicoption_government", Effects = [effect],
    };

    private static ProbeNoteDef Note(string id, string target, int weight) => new() { Id = id, Target = target, Weight = weight };

    /// <summary>The base game plus an in-memory mod holding the given rows.</summary>
    private static ContentDb Probe(S.CivicDef? civic = null, ProbeNoteDef[]? notes = null) =>
        GameContent.Build(Rules, [new ModSource
        {
            Root = AppContext.BaseDirectory,
            Manifest = new() { Id = "probe", Version = "1", Dependencies = ["government"], References = ["cities", "research"] },
            Instance = new ProbeMod(civic, notes ?? []),
        }]);

    private sealed class ProbeMod(S.CivicDef? civic, ProbeNoteDef[] notes) : IMod
    {
        public void Register(IModContext context)
        {
            if (civic is not null) context.Content<S.CivicDef>().Register(civic);
            foreach (var note in notes) context.Content<ProbeNoteDef>().Register(note);
        }
    }
}

/// <summary>A test mod's own data about a civic.</summary>
public sealed class ProbeNoteDef : ContentAttachment<S.CivicDef>
{
    public int Weight { get; init; }
}
