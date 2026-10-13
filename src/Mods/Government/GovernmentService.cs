using System.Globalization;
using Civ.Content;
using Civ.Content.Schema;
using Civ.Game;
using CivicDef = Civ.Content.Schema.CivicDef;

namespace Civ.Mods.Government;

internal sealed class GovernmentService : IGovernmentService
{
    public string? Validate(Simulation sim, int player, IReadOnlyList<string> civics)
    {
        if (!sim.State.Players.Any(p => p.Id == player)) return "Unknown player";
        var state = Read(sim, player);
        if (state.AnarchyTurns > 0) return "Player is in anarchy";
        if (state.RevolutionTimer > 0) return "Revolution cooldown is active";
        if (ValidateChoices(sim, civics) is { } error) return error;
        var proposed = Proposed(sim, player, civics);
        if (!Changed(sim, player, civics).Any()) return "No civic changes";
        // Validate the final cabinet, including retained choices. Never mutate state to preview a revolution.
        foreach (var id in proposed.Values)
        {
            var result = sim.Content.Requirements.Evaluate(sim.Content.Get<CivicDef>(id).Requirements,
                new(sim, player, ProposedCivics: proposed));
            if (!result.Success) return string.Join("; ", result.Failures.Select(f => f.Explanation));
        }
        return null;
    }
    private static string? ValidateChoices(Simulation sim, IReadOnlyList<string> civics)
    {
        var options = new HashSet<string>(StringComparer.Ordinal);
        foreach (string id in civics)
        {
            if (!sim.Content.TryGet<CivicDef>(id, out var def) || def.Option is null) return "Unknown civic";
            if (!options.Add(def.Option)) return "Choose one civic per option";
        }
        return null;
    }
    private static SortedDictionary<string, string> Proposed(Simulation sim, int player, IReadOnlyList<string> civics)
    {
        var result = new SortedDictionary<string, string>(sim.State.PlayerById(player).Civics, StringComparer.Ordinal);
        foreach (var id in civics) result[sim.Content.Get<CivicDef>(id).Option!] = id;
        return result;
    }
    private static IEnumerable<CivicDef> Changed(Simulation sim, int player, IReadOnlyList<string> civics) => civics
        .Select(sim.Content.Get<CivicDef>).Where(c => sim.State.PlayerById(player).Civics.GetValueOrDefault(c.Option!) != c.Id);
    public CivicSwitchResult Calculate(Simulation sim, int player, IReadOnlyList<string> civics)
    {
        var rules = sim.Content.Get<RuleConstantsDef>().All.Single();
        var effects = ModifierSink.Collect(new(sim, ModifierScope.Player, player));
        var lines = effects.Ledger.Lines.Where(l => l.Key.StartsWith("government:", StringComparison.Ordinal)).ToList();
        var changes = Changed(sim, player, civics).ToArray();
        int maximum = MaximumAnarchy(rules.Values.MaximumAnarchyTurns, effects.Ledger);
        int percent = (int)Math.Max(0, 100 + effects.Ledger.Total("government:anarchy_percent"));
        int turns = AnarchyLength(sim, player, changes, maximum, percent, lines);
        int minimum = rules.Raw.TryGetValue("MIN_REVOLUTION_TURNS", out var text) ? int.Parse(text, CultureInfo.InvariantCulture) : 5;
        int cooldown = changes.Length == 0 ? 0 : Math.Max(1, minimum * percent / 100) + turns;
        lines.Add(new("government:total", "government:anarchy_turns", turns));
        lines.Add(new("government:total", "government:cooldown_turns", cooldown));
        return new(turns, cooldown, new(lines));
    }
    private static int MaximumAnarchy(int baseline, Ledger ledger) => ledger.Lines
        .Where(l => l.Key == "government:anarchy_cap_delta").Select(l => baseline + (int)l.Amount).Append(baseline).Min();
    private static int AnarchyLength(Simulation sim, int player, CivicDef[] changes, int maximum, int percent, List<LedgerLine> lines)
    {
        if (changes.Length == 0 || maximum == 0) return 0;
        if (sim.State.PlayerById(player).Extensions.Get<CityPlayerState>(sim.Content)?.GoldenAge == true) return 0;
        int baseline = sim.Content.Get<RuleConstantsDef>().All.Single().Values.BaseCivicAnarchyTurns;
        int cities = sim.State.Cities.Count(c => c.Owner == player);
        int cityTurns = cities * sim.Content.Get<WorldSizeDef>(sim.Settings.WorldSize!).NumCitiesAnarchyPercent / 100;
        lines.Add(new("government:base", "government:base_anarchy", baseline));
        lines.Add(new("government:cities", "government:base_anarchy", cityTurns));
        foreach (var civic in changes) lines.Add(new(civic.Id, "government:base_anarchy", civic.AnarchyLength));
        int turns = (baseline + cityTurns + changes.Sum(c => c.AnarchyLength)) * percent / 100;
        if (turns == 0) return 0;
        turns = turns * sim.Content.Get<GameSpeedDef>(sim.Settings.GameSpeed).AnarchyPercent / 100;
        turns = turns * sim.Content.Get<EraDef>(sim.Settings.StartingEra).AnarchyPercent / 100;
        return Math.Clamp(turns, 1, maximum);
    }
    public void Adopt(Simulation sim, int player, IReadOnlyList<string> civics)
    {
        var result = Calculate(sim, player, civics);
        var owner = sim.State.PlayerById(player);
        foreach (var id in civics) owner.Civics[sim.Content.Get<CivicDef>(id).Option!] = id;
        owner.Extensions.Set(sim.Content, new GovernmentPlayerState { AnarchyTurns = result.AnarchyTurns, RevolutionTimer = result.CooldownTurns });
    }
    private static GovernmentPlayerState Read(Simulation sim, int player) => sim.State.PlayerById(player).Extensions.Get<GovernmentPlayerState>(sim.Content) ?? new();
}

internal sealed class GovernmentTimers : IPlayerTurnSystem
{
    public string Id => "government:timers";
    public int Order => TurnPhase.PlayerEnd + 1;
    public void Run(Simulation sim, int player)
    {
        var state = sim.State.PlayerById(player).Extensions.Get<GovernmentPlayerState>(sim.Content);
        if (state is null) return;
        state.AnarchyTurns = Math.Max(0, state.AnarchyTurns - 1);
        state.RevolutionTimer = Math.Max(0, state.RevolutionTimer - 1);
    }
    public void Run(Simulation sim) { foreach (var player in sim.State.Players.OrderBy(p => p.Id)) Run(sim, player.Id); }
}
