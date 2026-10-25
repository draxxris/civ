using Civ.Content;
using Civ.Game;
using Civ.Modding;
using S = Civ.Content.Schema;
using Xunit;

namespace Civ.Mods.Tests;

public sealed class ResearchRulesTests
{
    private const string Agriculture = "research:agriculture";
    private static readonly Lazy<ContentDb> Content = new(() => BuildContent());
    private static readonly Lazy<ContentDb> CustomContent = new(() => BuildContent(new CustomRules()));
    private static readonly Lazy<ContentDb> MissingEraContent = new(() => BuildContent(new MissingEra()));
    private static T Service<T>(Simulation sim) where T : class => sim.Content.Services.Get<T>().Single();
    private static ContentDb BuildContent(IMod? custom = null)
    {
        var loaded = new LoadedMods();
        var optional = new[] { "Diplomacy", "Espionage", "ProjectsVictory" }.Select(name =>
            ModLoader.FromDirectory(Path.Combine(AppContext.BaseDirectory, "optional-mods", name)));
        var sources = loaded.Sources.Concat(optional);
        if (custom is not null) sources = sources.Append(new ModSource { Manifest = new() { Id = "probe", Version = "1",
            Dependencies = ["foundation", "research"], References = ["foundation", "research"] }, Root = loaded.Root, Instance = custom });
        return ModLoader.Build(sources);
    }
    private static Simulation Scenario(int players = 2, string speed = "normal", string era = "foundation:era_ancient", ContentDb? content = null)
    {
        var settings = GameFactory.Settings() with { PlayerCount = players,
            Leaders = Enumerable.Repeat("peoples:leader_julius_caesar", players).ToArray(),
            GameSpeed = speed.Contains(':') ? speed : "foundation:gamespeed_" + speed, StartingEra = era };
        var sim = Simulation.CreateScenario(content ?? Content.Value, settings, new(12, 8, true),
            Enumerable.Range(0, 96).Select(_ => new Plot { Terrain = "world:grass" }).ToArray());
        GameFactory.NeutralPolitics(sim);
        foreach (var team in sim.State.Teams) team.Techs.Clear();
        for (int p = 0; p < players; p++) sim.State.AddCity(new City { Id = sim.State.NextCityId++, Owner = p,
            Position = sim.State.Grid.HexOf(new TileId(10 + 12 * p)), Population = p == 0 ? 1000 : 1 });
        return sim;
    }

    // Owner: rules constants. A custom ruleset that omits RevolutionResearchCostPercent must get the neutral
    // default rather than the shipped 82%, and the shipped realistic-timescale option must stay on.
    [Fact]
    public void Research_rule_defaults_stay_neutral_and_realistic_timescale_stays_on()
    {
        Assert.Equal(100, new S.RuleConstants().RevolutionResearchCostPercent);
        var option = SetupPolicies.GameOptions(Content.Value).Single(o => o.Id == "foundation:gameoption_realistic_timescale");
        Assert.True(option.Default);
    }

    // Owner: rate query. Literal vectors use high-precision independent powers. Existing tests cover
    // only the fallback. These catch stacked bonuses, non-strict prevalence boundaries and wrong weights.
    [Theory]
    [InlineData(2, 1, 2, "peace", 104)]
    [InlineData(2, 1, 2, "war", 107)]
    [InlineData(2, 1, 2, "open", 114)]
    [InlineData(4, 1, 2, "peace", 100)]
    [InlineData(3, 1, 2, "peace", 103)]
    [InlineData(3, 2, 3, "peace", 107)]
    [InlineData(4, 3, 4, "peace", 126)]
    [InlineData(5, 2, 3, "peace", 105)]
    [InlineData(5, 2, 5, "peace", 107)]
    public void Diffusion_uses_living_contact_prevalence_and_relationship_weights(int players, int holders, int met, string relation, int expected)
    {
        var sim = Scenario(players);
        var diplomacy = Service<IDiplomacyService>(sim);
        for (int p = 1; p < met; p++) diplomacy.EstablishContact(sim, 0, p);
        for (int p = 1; p <= holders; p++) sim.TeamOf(p).Techs.Add(Agriculture);
        if (relation == "war") sim.State.SetWar(0, 1, true);
        if (relation == "open") diplomacy.SignAgreement(sim, 0, 1, DiplomaticAgreement.OpenBorders);
        var before = SaveGame.Save(sim);
        Assert.Equal(expected, Service<IResearchService>(sim).RateModifier(sim, 0, Agriculture));
        Assert.Equal(before, SaveGame.Save(sim));
    }

    // Public rate-query contract: self-held repeatable knowledge has one half-unit of weight.
    // Existing vectors have only rival holders and miss diplomacy's self-open-borders shortcut.
    // Independent exponents 0.5 and 4.5 give bonuses 4 and 26; no production test seam is needed.
    [Theory]
    [InlineData(2, 0, 104)]
    [InlineData(4, 2, 126)]
    public void Self_held_repeatable_technology_uses_neutral_diffusion_weight(int players, int rivalHolders, int expected)
    {
        const string tech = "research:future_tech";
        var sim = Scenario(players);
        sim.TeamOf(0).Techs.Add(tech);
        var diplomacy = Service<IDiplomacyService>(sim);
        for (int p = 1; p < players; p++) diplomacy.EstablishContact(sim, 0, p);
        for (int p = 1; p <= rivalHolders; p++) sim.TeamOf(p).Techs.Add(tech);
        Assert.Equal(expected, Service<IResearchService>(sim).RateModifier(sim, 0, tech));
    }

    // Lifecycle risk distinct from the formula vectors: eliminated and barbarian holders must not dilute
    // the eligible denominator or supply exposure, including when diffusion is explicitly disabled.
    [Fact]
    public void Opt_out_restores_basic_bonus_and_excludes_dead_and_barbarian_teams()
    {
        var sim = Scenario(4);
        var diplomacy = Service<IDiplomacyService>(sim);
        for (int p = 1; p < 4; p++) { diplomacy.EstablishContact(sim, 0, p); sim.TeamOf(p).Techs.Add(Agriculture); }
        sim.State.RemoveCity(sim.State.Cities.Single(c => c.Owner == 2).Id);
        sim.State.PlayerById(3).Extensions.Set(sim.Content, new MilitaryPlayerState { Barbarian = true });
        sim.Settings.GameOptions["foundation:gameoption_no_tech_diffusion"] = true;
        Assert.Equal(125, Service<IResearchService>(sim).RateModifier(sim, 0, Agriculture));
        sim.Settings.GameOptions["foundation:gameoption_no_tech_diffusion"] = false;
        Assert.Equal(104, Service<IResearchService>(sim).RateModifier(sim, 0, Agriculture));
    }

    // Welfare owns era-weighted technology score and the existing total-score contract, not raw tech
    // counts. One ancient tech versus ancient + modern gives 14%, not 50%. Zero total stays finite.
    [Theory]
    [InlineData(false, 364)]
    [InlineData(true, 104)]
    public void Welfare_uses_era_weighted_technology_and_total_scores_with_a_neutral_zero_guard(bool zero, int expected)
    {
        var sim = Scenario();
        sim.State.Cities.Single(c => c.Owner == 0).Population = zero ? 0 : 10;
        sim.State.Cities.Single(c => c.Owner == 1).Population = 40;
        if (!zero) sim.TeamOf(0).Techs.Add("research:fishing");
        sim.TeamOf(1).Techs.UnionWith([Agriculture, "research:advanced_flight"]);
        Service<IDiplomacyService>(sim).EstablishContact(sim, 0, 1);
        // Deficit 75 - floor(100/7) = 61; round(100*.98^61)=29; floor(71*88/24)=260.
        Assert.Equal(expected, Service<IResearchService>(sim).RateModifier(sim, 0, Agriculture));
    }

    // Cost service owns timeline and truncation. 252 shipped technologies / 7 era intervals = 36.
    // Independent vectors straddle integer raw-power thresholds; no production arithmetic is reused.
    [Theory]
    [InlineData(0, 0, 100, 43)]
    [InlineData(18, 0, 190, 81)]
    [InlineData(36, 0, 372, 159)]
    [InlineData(0, 100, 49, 21)]
    [InlineData(1, 1, 102, 43)]
    [InlineData(1, 2, 101, 43)]
    [InlineData(1, 3, 100, 43)]
    [InlineData(1, 4, 100, 43)]
    [InlineData(0, 1200, 9, 3)]
    public void Timeline_default_uses_fractional_progress_and_reference_integer_stages(int known, int elapsed, long percent, long cost)
    {
        var sim = Scenario();
        sim.TeamOf(0).Techs.UnionWith(sim.Content.Get<S.TechDef>().All.Take(known).Select(t => t.Id));
        sim.State.Turn = elapsed + 1;
        var before = SaveGame.Save(sim);
        var result = Service<IResearchService>(sim).CalculateCost(sim, 0, Agriculture);
        Assert.Equal(percent, result.Ledger.Total("research:timeline_percent"));
        Assert.Equal(cost, result.Cost); // 30*250/100=75; *133/100=99; *84/100=83; *65/100=53; *82/100=43.
        Assert.Equal(elapsed * 7L * 1_000_000 / 1200, result.Ledger.Total("research:expected_era_millionths"));
        Assert.Equal(known * 1_000_000L / 36, result.Ledger.Total("research:player_era_millionths"));
        Assert.Equal(before, SaveGame.Save(sim));
        sim.Settings.GameOptions["foundation:gameoption_realistic_timescale"] = false;
        Assert.Equal(43, Service<IResearchService>(sim).CalculateCost(sim, 0, Agriculture).Cost);
    }

    // Distinct timeline inputs: speed and late-start duration. All earlier eras are known, so current
    // medieval progress starts at exactly 2. These elapsed turns all give expected era 2 + 5/12.
    [Theory]
    [InlineData("normal", 72)]
    [InlineData("quick", 54)]
    [InlineData("marathon", 144)]
    public void Late_start_and_speed_use_schedule_turns_not_calendar_dates(string speed, int elapsed)
    {
        var sim = Scenario(speed: speed, era: "foundation:era_medieval");
        sim.TeamOf(0).Techs.UnionWith(sim.Content.Get<S.TechDef>().All.Where(t => t.Era is "foundation:era_ancient" or "foundation:era_classical").Select(t => t.Id));
        sim.State.Turn = elapsed + 1;
        var result = Service<IResearchService>(sim).CalculateCost(sim, 0, Agriculture);
        Assert.Equal(60, result.Ledger.Total("research:timeline_percent"));
        Assert.Equal(2_416_666, result.Ledger.Total("research:expected_era_millionths"));
        Assert.Equal(2_000_000, result.Ledger.Total("research:player_era_millionths"));
    }

    [Fact]
    public void Leader_era_compensates_missing_old_technologies_and_terminal_option_changes_expected_progress()
    {
        var sim = Scenario();
        sim.State.PlayerById(0).Extensions.Set(sim.Content, new CityPlayerState { CurrentEra = "foundation:era_classical" });
        sim.TeamOf(0).Techs.UnionWith(sim.Content.Get<S.TechDef>().All.Where(t => t.Era == "foundation:era_ancient").Take(16).Select(t => t.Id));
        var service = Service<IResearchService>(sim);
        Assert.Equal(205, service.CalculateCost(sim, 0, Agriculture).Ledger.Total("research:timeline_percent"));
        sim.State.Turn = 101;
        sim.Settings.GameOptions["foundation:gameoption_no_future"] = true;
        // End=transhuman (6), not modern (5); inclusive techCount=249, integer denominator=41.
        Assert.Equal(500_000, service.CalculateCost(sim, 0, Agriculture).Ledger.Total("research:expected_era_millionths"));
    }

    // Contribution boundary owns completion, not cost previews or advancing the timeline. Theft pricing
    // and direct grants must consume that same effective cost; this is distinct from numeric previews.
    [Fact]
    public void Cost_changes_leave_progress_untouched_until_contribution_and_theft_uses_effective_cost()
    {
        var sim = Scenario(); var research = Service<IResearchService>(sim);
        Assert.True(sim.TryApply(new SetTeamResearchCommand { Player = 0, Tech = Agriculture }, out var error), error);
        research.Contribute(sim, 0, 40);
        sim.State.Turn = 101;
        var before = SaveGame.Save(sim);
        Assert.Equal(21, research.CalculateCost(sim, 0, Agriculture).Cost);
        Assert.Equal(40, sim.TeamOf(0).ResearchProgress);
        Assert.DoesNotContain(Agriculture, sim.TeamOf(0).Techs);
        Assert.Equal(42, Service<IEspionageService>(sim).MissionCost(sim, 0, sim.State.Cities.Single(c => c.Owner == 1).Id, EspionageMissionKind.StealTech, Agriculture));
        Assert.Equal(before, SaveGame.Save(sim));
        research.Contribute(sim, 0, 0);
        Assert.Contains(Agriculture, sim.TeamOf(0).Techs);
        Assert.Equal(19, sim.TeamOf(0).Extensions.Get<ResearchTeamState>(sim.Content)!.Overflow);
        research.AddProgress(sim, 0, "research:fishing", long.MaxValue);
        Assert.Contains("research:fishing", sim.TeamOf(0).Techs);
    }

    // Custom content contracts: missing inputs stay neutral with diagnostics; excessive exponents fail
    // explicitly rather than wrapping or adding a gameplay cap. No test-only production entry points.
    [Theory]
    [InlineData("probe:no_schedule", "foundation:era_ancient", "missing_schedule")]
    [InlineData("normal", "probe:empty", "sparse_technologies")]
    public void Unsupported_custom_timeline_inputs_are_neutral_and_diagnostic(string speed, string era, string reason)
    {
        var sim = Scenario(speed: speed, era: era, content: CustomContent.Value);
        var result = Service<IResearchService>(sim).CalculateCost(sim, 0, Agriculture);
        Assert.Equal(100, result.Ledger.Total("research:timeline_percent"));
        Assert.Equal(1, result.Ledger.Total("research:timeline_neutral:" + reason));
    }

    [Fact]
    public void Missing_technology_era_is_not_inferred_and_excessive_power_is_checked()
    {
        var missing = Scenario(content: MissingEraContent.Value);
        Assert.Equal(1, Service<IResearchService>(missing).CalculateCost(missing, 0, Agriculture).Ledger.Total("research:timeline_neutral:missing_technology_era"));
        var excessive = Scenario(content: CustomContent.Value);
        excessive.TeamOf(0).Techs.UnionWith(excessive.Content.Get<S.TechDef>().All.Select(t => t.Id));
        var before = SaveGame.Save(excessive);
        Assert.Throws<OverflowException>(() => Service<IResearchService>(excessive).CalculateCost(excessive, 0, Agriculture));
        Assert.Equal(before, SaveGame.Save(excessive));
    }

    private sealed class CustomRules : IMod
    {
        public void Register(IModContext context)
        {
            context.Content<S.GameSpeedDef>().Register(new() { Id = "probe:no_schedule", ResearchPercent = 100 });
            for (int i = 0; i < 200; i++) context.Content<S.EraDef>().Register(new() { Id = "probe:era_" + i, StartPercent = 100, ResearchPercent = 100 });
            context.Content<S.EraDef>().Register(new() { Id = "probe:empty", StartPercent = 100, ResearchPercent = 100 });
        }
    }
    private sealed class MissingEra : IMod
    {
        public void Register(IModContext context) => context.Content<S.TechDef>().Register(new() { Id = "probe:missing", Cost = 10 });
    }
}
