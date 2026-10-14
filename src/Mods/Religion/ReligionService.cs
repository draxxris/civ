using Civ.Content;
using Civ.Content.Schema;
using Civ.Game;
using BuildingDef = Civ.Content.Schema.BuildingDef;

namespace Civ.Mods.Religion;

internal sealed partial class ReligionService : IReligionService
{
    public string? StateReligion(Simulation sim, int player) => ReligionData.StateReligion(sim, player);
    public IReadOnlyList<string> Present(Simulation sim, int city) => ReligionData.Present(sim, sim.State.CityById(city));
    public int? HolyCity(Simulation sim, string religion)
    {
        var founding = ReligionData.Game(sim).Founded.GetValueOrDefault(religion);
        return founding is not null && sim.State.FindCity(founding.HolyCity) is not null ? founding.HolyCity : null;
    }
    public IReadOnlyList<ReligionDef> Choices(Simulation sim, int player) => sim.Content.Get<ReligionDef>().All
        .Where(r => sim.State.Cities.Any(c => c.Owner == player && ReligionData.Has(sim, c, r.Id)))
        .OrderBy(r => r.Id, StringComparer.Ordinal).ToArray();
    public IReadOnlyList<Civ.Content.Schema.UnitDef> Missionaries(Simulation sim, string religion) => sim.Content.Get<Civ.Content.Schema.UnitDef>().All
        .Where(u => u.ReligionSpreads.GetValueOrDefault(religion) > 0 && u.DefaultUnitAi == "military:unitai_missionary")
        .OrderBy(u => u.Id, StringComparer.Ordinal).ToArray();
    public string? EvaluateCondition(Requirement.Leaf leaf, RequirementContext context)
    {
        bool success = leaf.Id switch
        {
            "foundation:any_religion" => context.City is { } c && Present(context.Sim, c.Id).Count > 0,
            "foundation:state_religion" => context.City is { } c && StateReligion(context.Sim, context.Player) is { } r && ReligionData.Has(context.Sim, c, r),
            _ => false
        };
        return success ? null : "Required religion condition: " + leaf.Id;
    }
    public string? ValidateBuilding(Simulation sim, City city, BuildingDef building)
    {
        var state = StateReligion(sim, city.Owner);
        if (building.FreeFromStateReligion || state is null) return null;
        if (ReligionData.Civics(sim, city.Owner).Any(c => c.FreeReligion)) return null;
        var required = ContentReferences.Leaves(building.Requirements ?? new Requirement.All([]))
            .FirstOrDefault(l => l.Kind == "religion" && l.Param is null);
        return required is null || required.Id == state ? null : "Requires matching state religion: " + required.Id;
    }
    public string? ValidateConversion(Simulation sim, int player, string? religion)
    {
        var owner = sim.State.Players.FirstOrDefault(p => p.Id == player);
        if (owner is null || owner.Ai == "military:barbarians") return "Invalid player";
        if (GovernmentStatus.IsAnarchy(sim, player)) return "Player is in anarchy";
        var current = ReligionData.Player(sim, player);
        if (current.ConversionTimer > 0) return "Religion conversion cooldown is active";
        if (!ReligionData.Civics(sim, player).Any(c => c.RequiresStateReligion)) return "Civics do not enable state religion";
        if (current.LastStateReligion == religion) return "No state religion change";
        if (religion is not null && !Choices(sim, player).Any(r => r.Id == religion)) return "Religion is not present in your cities";
        return null;
    }
    public ReligionConversion PreviewConversion(Simulation sim, int player)
    {
        var rules = sim.Content.Get<RuleConstantsDef>().All.Single().Values;
        var ledger = ModifierSink.Collect(new(sim, ModifierScope.Player, player)).Ledger;
        int cap = ledger.Lines.Where(l => l.Key == "government:anarchy_cap_delta")
            .Select(l => rules.MaximumAnarchyTurns + (int)l.Amount).Append(rules.MaximumAnarchyTurns).Min();
        int percent = (int)Math.Max(0, 100 + ledger.Total("government:anarchy_percent"));
        int turns = ConversionAnarchy(sim, player, cap, percent, rules.BaseReligionAnarchyTurns);
        int cooldown = Math.Max(1, ReligionData.Raw(sim, "MIN_CONVERSION_TURNS", 5) * percent / 100) + turns;
        var lines = ledger.Lines.Where(l => l.Key.StartsWith("government:", StringComparison.Ordinal)).ToList();
        lines.Add(new("religion:conversion", "religion:anarchy_turns", turns));
        lines.Add(new("religion:conversion", "religion:cooldown_turns", cooldown));
        return new(turns, cooldown, new(lines));
    }
    private static int ConversionAnarchy(Simulation sim, int player, int cap, int percent, int baseline)
    {
        if (cap <= 0 || sim.State.PlayerById(player).Extensions.Get<CityPlayerState>(sim.Content)?.GoldenAge == true) return 0;
        int cities = sim.State.Cities.Count(c => c.Owner == player);
        int turns = (baseline + cities * sim.Content.Get<WorldSizeDef>(sim.Settings.WorldSize!).NumCitiesAnarchyPercent / 100) * percent / 100;
        if (turns == 0) return 0;
        turns = turns * sim.Content.Get<GameSpeedDef>(sim.Settings.GameSpeed).AnarchyPercent / 100;
        turns = turns * sim.Content.Get<EraDef>(sim.Settings.StartingEra).AnarchyPercent / 100;
        return Math.Clamp(turns, 1, cap);
    }
    public void Convert(Simulation sim, int player, string? religion)
    {
        var preview = PreviewConversion(sim, player);
        var state = ReligionData.EditPlayer(sim, player);
        state.LastStateReligion = religion;
        state.ConversionTimer = preview.CooldownTurns;
        var owner = sim.State.PlayerById(player);
        var government = owner.Extensions.Get<GovernmentPlayerState>(sim.Content);
        if (government is null) { government = new(); owner.Extensions.Set(sim.Content, government); }
        government.AnarchyTurns += preview.AnarchyTurns;
        sim.Events.Publish(new StateReligionChanged(player, religion));
    }
}
internal sealed class ReligionRequirements(IReligionService service) : IContentRequirementEvaluator
{
    public string Kind => "religion";
    public Type DefinitionType => typeof(ReligionDef);
    public string? Evaluate(Requirement.Leaf leaf, RequirementContext context)
    {
        bool success = leaf.Param switch
        {
            1 => service.StateReligion(context.Sim, context.Player) == leaf.Id,
            2 => context.City is { } c && service.HolyCity(context.Sim, leaf.Id) == c.Id,
            null or 0 => context.City is { } c && service.Present(context.Sim, c.Id).Contains(leaf.Id, StringComparer.Ordinal),
            _ => false
        };
        return success ? null : "Required religion: " + leaf.Id;
    }
}
