using Civ.Content;
using Civ.Game;
using S = Civ.Content.Schema;

namespace Civ.Mods.ScriptedWonders;

internal sealed class ScriptedWonderService : IScriptedWonderService, IModifierSource
{
    private static IEnumerable<WonderScriptDef> Rules(Simulation sim) => sim.Content.Get<WonderScriptDef>().All;

    public bool IsActive(Simulation sim, int cityId, string building)
    {
        var city = sim.State.CityById(cityId);
        if (!city.Buildings.Contains(building) || !sim.Content.TryGet<S.BuildingDef>(building, out var def)) return false;
        if (def.ObsoleteTech is { } tech && sim.KnowsTech(city.Owner, tech)) return false;
        return !def.RequiresActiveCivics || sim.Content.Requirements.EvaluateKind(def.Requirements,
            new(sim, city.Owner, city, sim.State.Grid.TileAt(city.Position)), "civic").Success;
    }

    private static int Interval(Simulation sim, WonderScriptDef rule) => rule.IntervalTurns == 0 ? 0
        : Math.Max(1, rule.IntervalTurns * sim.Content.Get<S.GameSpeedDef>(sim.Settings.GameSpeed).TrainPercent / 100);

    public IReadOnlyList<ScriptedWonderStatus> DescribeCity(Simulation sim, int cityId)
    {
        var city = sim.State.CityById(cityId);
        var state = city.Extensions.TryGet<ScriptedWonderCityState>(sim.Content);
        return Rules(sim).Where(r => city.Buildings.Contains(r.Building)).Select(r =>
        {
            var progress = state?.Wonders.GetValueOrDefault(r.Building);
            return new ScriptedWonderStatus(r.Building, IsActive(sim, cityId, r.Building),
                progress?.CompletionApplied ?? false, progress?.ElapsedTurns ?? 0, Interval(sim, r), r.Unit);
        }).ToArray();
    }

    private static ScriptedWonderProgress Edit(Simulation sim, City city, string building)
    {
        var state = city.Extensions.Get<ScriptedWonderCityState>(sim.Content);
        if (state is null) { state = new(); city.Extensions.Set(sim.Content, state); }
        if (!state.Wonders.TryGetValue(building, out var progress)) state.Wonders[building] = progress = new();
        return progress;
    }

    internal void Completed(Simulation sim, BuildingCompleted message)
    {
        var city = sim.State.FindCity(message.City);
        var rule = Rules(sim).SingleOrDefault(r => r.Building == message.Building);
        if (city is null || rule is null || !IsActive(sim, city.Id, rule.Building)) return;
        var progress = Edit(sim, city, rule.Building);
        if (progress.CompletionApplied) return;
        progress.CompletionApplied = true;
        progress.LastProcessedTurn = sim.State.Turn; // The construction turn is not a full active turn.
        for (int i = 0; i < rule.CompletionUnits; i++) Spawn(sim, city, rule.Unit!);
        if (rule.CompletionUnits > 0) Effect(sim, city, rule, "units", rule.CompletionUnits);
        if (rule.CompletionCulture > 0)
        {
            AddCulture(sim, city, rule.CompletionCulture);
            Effect(sim, city, rule, "culture", rule.CompletionCulture);
        }
        GrantBuildings(sim, city, rule);
    }

    private static void GrantBuildings(Simulation sim, City city, WonderScriptDef rule)
    {
        if (rule.EmpireFreeBuilding is not { } building) return;
        foreach (var target in sim.State.Cities.Where(c => c.Owner == city.Owner).OrderBy(c => c.Id).ToArray())
        {
            if (target.Buildings.Contains(building)) continue;
            target.Buildings.Add(building);
            sim.Events.Publish(new BuildingCompleted(target.Id, building));
        }
        Effect(sim, city, rule, "free_building", 1);
    }

    internal void Tick(Simulation sim, City city)
    {
        foreach (var rule in Rules(sim).Where(r => r.IntervalTurns > 0 && IsActive(sim, city.Id, r.Building)))
        {
            var progress = Edit(sim, city, rule.Building);
            if (progress.LastProcessedTurn == sim.State.Turn) continue;
            progress.LastProcessedTurn = sim.State.Turn;
            progress.ElapsedTurns++;
            int interval = Interval(sim, rule);
            if (progress.ElapsedTurns < interval) continue;
            progress.ElapsedTurns -= interval;
            Spawn(sim, city, rule.Unit!);
            Effect(sim, city, rule, "periodic_unit", 1);
        }
    }

    internal void Victory(Simulation sim, CombatResolved message)
    {
        var winner = sim.State.FindUnit(message.Winner);
        if (winner is null) return;
        foreach (var rule in Rules(sim).Where(r => r.VictoryExperience > 0))
        {
            var city = ActiveCity(sim, winner.Owner, rule);
            if (city is null) continue;
            winner.Experience += rule.VictoryExperience;
            Effect(sim, city, rule, "victory_experience", rule.VictoryExperience);
        }
    }

    private City? ActiveCity(Simulation sim, int player, WonderScriptDef rule) => sim.State.Cities
        .Where(c => c.Owner == player).OrderBy(c => c.Id).FirstOrDefault(c => IsActive(sim, c.Id, rule.Building));

    public void Contribute(ModifierContext context, ModifierSink sink)
    {
        if (context.Scope != ModifierScope.Unit || context.Unit is not { } unit) return;
        var sim = context.Sim;
        foreach (var rule in Rules(sim))
        {
            if (rule.UpgradeDiscount > 0 && ActiveCity(sim, unit.Owner, rule) is not null)
                sink.Add(rule.Building, "military:upgrade_discount", rule.UpgradeDiscount);
            if (rule.BarbarianBorderPenalty > 0 && ProtectedBarbarian(sim, unit, rule))
                sink.Add(rule.Building, "military:combat_percent", -rule.BarbarianBorderPenalty);
        }
    }

    private bool ProtectedBarbarian(Simulation sim, Unit unit, WonderScriptDef rule)
    {
        if (sim.State.PlayerById(unit.Owner).Extensions.TryGet<MilitaryPlayerState>(sim.Content)?.Barbarian != true) return false;
        if (sim.Content.Get<S.UnitDef>(unit.Type).Domain != "military:domain_land") return false;
        return sim.State.PlotAt(unit.Position).Owner is { } owner && owner != unit.Owner && ActiveCity(sim, owner, rule) is not null;
    }

    private static void AddCulture(Simulation sim, City city, int amount) =>
        sim.Content.Services.Get<ICityCultureService>().Single().AddCulture(sim, city.Id, amount, 1);

    private static void Spawn(Simulation sim, City city, string type)
    {
        var unit = new Unit { Id = sim.State.NextUnitId++, Owner = city.Owner, Type = type, Position = city.Position };
        sim.State.AddUnit(unit);
        sim.Events.Publish(new UnitCreated(unit.Id, unit.Owner, unit.Type, unit.Position));
        sim.Content.Services.Get<IVisibilityService>().SingleOrDefault()?.Recompute(sim);
    }

    private static void Effect(Simulation sim, City city, WonderScriptDef rule, string effect, long amount) =>
        sim.Events.Publish(new ScriptedWonderEffectApplied(city.Id, rule.Building, effect, amount));
}
