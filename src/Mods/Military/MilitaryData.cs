using System.Globalization;
using Ariel.Content;
using Ariel.Game;
using S = Ariel.Content.Schema;

namespace Ariel.Mods.Military;

internal static class MilitaryData
{
    internal static S.UnitDef Definition(Simulation sim, Unit unit) => sim.Content.Get<S.UnitDef>(unit.Type);
    internal static S.RuleConstants Constants(Simulation sim) => sim.Content.Get<S.RuleConstantsDef>().All.Single().Values;
    internal static int Raw(Simulation sim, string key, int fallback) =>
        sim.Content.Get<S.RuleConstantsDef>().All.Single().Raw.TryGetValue(key, out var value)
        && int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var number) ? number : fallback;
    internal static MilitaryUnitState Read(Simulation sim, Unit unit) => unit.Extensions.Get<MilitaryUnitState>(sim.Content) ?? new();
    internal static MilitaryUnitState Edit(Simulation sim, Unit unit)
    {
        var state = unit.Extensions.Get<MilitaryUnitState>(sim.Content);
        if (state is not null) return state;
        state = new(); unit.Extensions.Set(sim.Content, state); return state;
    }
    internal static MilitaryPlayerState Player(Simulation sim, int player) => sim.State.PlayerById(player).Extensions.Get<MilitaryPlayerState>(sim.Content) ?? new();
    internal static MilitaryPlayerState EditPlayer(Simulation sim, int player)
    {
        var state = sim.State.PlayerById(player).Extensions.Get<MilitaryPlayerState>(sim.Content);
        if (state is not null) return state;
        state = new(); sim.State.PlayerById(player).Extensions.Set(sim.Content, state); return state;
    }
    internal static bool VisibleEnemy(Simulation sim, int player, Unit other) => Enemy(sim, player, other.Owner)
        && sim.Content.Services.Get<IVisibilityService>().Single().IsUnitVisible(sim, other.Id, sim.TeamOf(player).Id);
    internal static bool Enemy(Simulation sim, int player, int other) => sim.TeamOf(player).Wars.Contains(sim.TeamOf(other).Id);
    internal static bool Friendly(Simulation sim, int player, int? other) => other is { } owner && sim.TeamOf(player).Id == sim.TeamOf(owner).Id;
    internal static IEnumerable<S.PromotionDef> Promotions(Simulation sim, Unit unit) => unit.Promotions.Select(sim.Content.Get<S.PromotionDef>);
    internal static IEnumerable<S.TechDef> Techs(Simulation sim, int player) => sim.TeamOf(player).Techs.Select(sim.Content.Get<S.TechDef>);
    internal static ModifierSink Effects(Simulation sim, Unit unit) => ModifierSink.Collect(new(sim, ModifierScope.Unit, unit.Owner,
        sim.State.CityAt(unit.Position), sim.State.Grid.TileAt(unit.Position), unit, sim.TeamOf(unit.Owner)));
    internal static int Effect(ModifierSink sink, string key) => checked((int)sink.Ledger.Total("military:" + key));
    internal static void Sight(Simulation sim)
    {
        sim.Content.Services.Get<IVisibilityService>().Single().Recompute(sim);
        sim.Content.Services.Get<IDiplomacyService>().SingleOrDefault()?.DiscoverContacts(sim);
    }
    internal static void RemoveUnit(Simulation sim, int unit)
    {
        foreach (int cargo in sim.Content.Services.Get<IUnitService>().Single().CargoUnits(sim, unit)) sim.State.RemoveUnit(cargo);
        sim.State.RemoveUnit(unit);
    }
    internal static void MoveUnit(Simulation sim, int unit, Hex target)
    {
        foreach (int cargo in sim.Content.Services.Get<IUnitService>().Single().CargoUnits(sim, unit)) sim.State.MoveUnit(cargo, target);
        sim.State.MoveUnit(unit, target);
    }
    internal static bool CrossesCliff(Simulation sim, TileId from, TileId to)
    {
        for (int direction = 0; direction < 6; direction++)
            if (sim.State.Grid.Neighbor(from, direction) == to)
                return (sim.State.Plots[from.Value].CliffEdges & (1 << direction)) != 0;
        return false;
    }
    internal static bool CrossesRiver(Simulation sim, TileId from, TileId to)
    {
        for (int direction = 0; direction < 6; direction++)
            if (sim.State.Grid.Neighbor(from, direction) == to) return (sim.State.Plots[from.Value].RiverEdges & (1 << direction)) != 0;
        return false;
    }
    internal static IEnumerable<S.BuildingDef> Buildings(Simulation sim, City city) => city.Buildings.Order(StringComparer.Ordinal)
        .Select(sim.Content.Get<S.BuildingDef>).Where(b => b.ObsoleteTech is null || !sim.KnowsTech(city.Owner, b.ObsoleteTech))
        .Where(b => !b.RequiresActiveCivics || sim.Content.Requirements.EvaluateKind(b.Requirements, new(sim, city.Owner, city, sim.State.Grid.TileAt(city.Position)), "civic").Success);
    internal static Unit Spawn(Simulation sim, int owner, string type, Hex position)
    {
        var unit = new Unit { Id = sim.State.NextUnitId++, Owner = owner, Type = type, Position = position };
        sim.State.AddUnit(unit); sim.Events.Publish(new UnitCreated(unit.Id, owner, type, position)); return unit;
    }
}

/// <summary>Promotions remain data, never permanently baked into the unit's base definition.</summary>
internal sealed class PromotionEffects : IModifierSource
{
    public void Contribute(ModifierContext context, ModifierSink sink)
    {
        if (context.Scope != ModifierScope.Unit || context.Unit is not { } unit) return;
        foreach (var p in MilitaryData.Promotions(context.Sim, unit))
        {
            AddNumbers(p, sink);
            AddMap(p.Id, "terrain_attack:", p.TerrainAttacks, sink);
            AddMap(p.Id, "terrain_defense:", p.TerrainDefenses, sink);
            AddMap(p.Id, "feature_attack:", p.FeatureAttacks, sink);
            AddMap(p.Id, "feature_defense:", p.FeatureDefenses, sink);
            AddMap(p.Id, "combat_type:", p.CombatTypeModifiers, sink);
            AddMap(p.Id, "domain:", p.DomainMods, sink);
        }
    }
    private static void AddNumbers(S.PromotionDef p, ModifierSink sink)
    {
        var values = new (string Key, int Value)[] {
            ("combat_percent", p.CombatPercent), ("city_attack", p.CityAttack), ("city_defense", p.CityDefense),
            ("hills_attack", p.HillsAttack), ("hills_defense", p.HillsDefense), ("moves", p.MovesChange),
            ("move_discount", p.MoveDiscountChange), ("first_strikes", p.FirstStrikesChange), ("chance_first_strikes", p.ChanceFirstStrikesChange),
            ("withdrawal", p.WithdrawalChange), ("collateral_percent", p.CollateralDamageChange), ("collateral_protection", p.CollateralDamageProtection),
            ("friendly_heal", p.FriendlyHealChange), ("neutral_heal", p.NeutralHealChange), ("enemy_heal", p.EnemyHealChange),
            ("same_tile_heal", p.SameTileHealChange), ("adjacent_heal", p.AdjacentTileHealChange), ("upgrade_discount", p.UpgradeDiscount),
            ("experience_percent", p.ExperiencePercent), ("kamikaze", p.KamikazePercent), ("pillage_percent", p.PillageChange),
            ("bombard_rate", p.BombardRateChange), ("cargo", p.CargoChange),
            ("air_range", p.AirRangeChange), ("intercept", p.InterceptChange), ("evasion", p.EvasionChange),
            ("first_strike_immune", p.ImmuneToFirstStrikes ? 1 : 0), ("river_attack", p.RiverAttack ? 1 : 0),
            ("amphibious", p.Amphibious ? 1 : 0), ("blitz", p.Blitz ? 1 : 0), ("always_heal", p.AlwaysHeal ? 1 : 0),
            ("enemy_route", p.EnemyRoute ? 1 : 0), ("move_peaks", p.CanMovePeaks ? 1 : 0), ("zone_control", p.ZoneOfControl ? 1 : 0) };
        foreach (var (key, value) in values) sink.Add(p.Id, "military:" + key, value);
    }
    private static void AddMap(string source, string prefix, IReadOnlyDictionary<string, int> map, ModifierSink sink)
    {
        foreach (var pair in map.OrderBy(p => p.Key, StringComparer.Ordinal)) sink.Add(source, "military:" + prefix + pair.Key, pair.Value);
    }
}
