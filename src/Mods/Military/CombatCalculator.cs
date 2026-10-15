using Civ.Content;
using Civ.Game;
using S = Civ.Content.Schema;

namespace Civ.Mods.Military;

internal static class CombatCalculator
{
    internal static CombatPreview Calculate(Simulation sim, Unit attacker, Unit defender)
    {
        var a = MilitaryData.Definition(sim, attacker); var d = MilitaryData.Definition(sim, defender);
        var ae = MilitaryData.Effects(sim, attacker); var de = MilitaryData.Effects(sim, defender);
        var attack = Strength(sim, attacker, ae);
        AddDefense(sim, attacker, defender, ae, de);
        var defense = Strength(sim, defender, de);
        var constants = MilitaryData.Constants(sim);
        int odds = constants.CombatDieSides * defense.Current / Math.Max(1, attack.Current + defense.Current);
        odds = BarbarianOdds(sim, attacker, defender, odds, constants.CombatDieSides);
        int factor = (attack.Firepower + defense.Firepower + 1) / 2;
        int damageA = Math.Max(1, constants.CombatDamage * (defense.Firepower + factor) / (attack.Firepower + factor));
        int damageD = Math.Max(1, constants.CombatDamage * (attack.Firepower + factor) / (defense.Firepower + factor));
        bool immuneA = a.FirstStrikeImmune || MilitaryData.Effect(ae, "first_strike_immune") > 0;
        bool immuneD = d.FirstStrikeImmune || MilitaryData.Effect(de, "first_strike_immune") > 0;
        var preview = new CombatPreview(attacker.Id, defender.Id, attack, defense, odds, constants.CombatDieSides, damageA, damageD, 0, 0,
            immuneD ? 0 : Math.Max(0, a.FirstStrikes + MilitaryData.Effect(ae, "first_strikes")),
            immuneD ? 0 : Math.Max(0, a.ChanceFirstStrikes + MilitaryData.Effect(ae, "chance_first_strikes")),
            immuneA ? 0 : Math.Max(0, d.FirstStrikes + MilitaryData.Effect(de, "first_strikes")),
            immuneA ? 0 : Math.Max(0, d.ChanceFirstStrikes + MilitaryData.Effect(de, "chance_first_strikes")),
            Math.Clamp(a.WithdrawalPercent + MilitaryData.Effect(ae, "withdrawal"), 0, 100), a.CombatLimit);
        var success = CombatOdds.Success(preview, attacker.Health, defender.Health, constants.MaxHitPoints);
        int withdrawal = (1_000_000 - success) * preview.WithdrawalPercent / 100;
        return preview with { SuccessOdds = success, WithdrawalOdds = withdrawal };
    }
    private static CombatStrength Strength(Simulation sim, Unit unit, ModifierSink sink)
    {
        var def = MilitaryData.Definition(sim, unit);
        int strength = def.Strength + MilitaryData.Techs(sim, unit.Owner).Sum(t => t.UnitClassStrengthChanges.GetValueOrDefault(def.DefaultUnit));
        int modifier = MilitaryData.Effect(sink, "combat_percent");
        int maximum = modifier > 0 ? strength * (100 + modifier) : strength * 10000 / (100 - modifier);
        maximum = Math.Max(1, maximum);
        int current = maximum * unit.Health / MilitaryData.Constants(sim).MaxHitPoints;
        int firepower = (maximum + current + 1) / 2;
        sink.Add(unit.Type, "military:base_strength", strength);
        sink.Add("military:total", "military:maximum_strength", maximum);
        sink.Add("military:total", "military:current_strength", current);
        sink.Add("military:total", "military:firepower", firepower);
        return new(maximum, current, firepower, sink.Ledger);
    }
    private static void AddDefense(Simulation sim, Unit attacker, Unit defender, ModifierSink ae, ModifierSink de)
    {
        var plot = sim.State.PlotAt(defender.Position);
        var a = MilitaryData.Definition(sim, attacker); var d = MilitaryData.Definition(sim, defender);
        if (!d.NoDefensiveBonus) PlotDefense(sim, defender, a.IgnoreBuildingDefense, de);
        if (Fortifiable(d)) Add(de, "military:fortify", MilitaryData.Read(sim, defender).FortifyTurns * MilitaryData.Raw(sim, "FORTIFY_MODIFIER_PER_TURN", 5));
        AddLocations(sim, attacker, defender, plot, ae, de);
        AddOpponents(a, d, ae, de);
        AddHandicap(sim, attacker, defender, de);
        if (MilitaryData.CrossesRiver(sim, sim.State.Grid.TileAt(attacker.Position), sim.State.Grid.TileAt(defender.Position)) && MilitaryData.Effect(ae, "river_attack") == 0)
            Add(de, "military:river", -MilitaryData.Constants(sim).RiverAttackModifier);
        if (sim.Content.Get<S.TerrainDef>(sim.State.PlotAt(attacker.Position).Terrain).IsWater
            && !sim.Content.Get<S.TerrainDef>(plot.Terrain).IsWater && MilitaryData.Effect(ae, "amphibious") == 0)
            Add(de, "military:amphibious", -MilitaryData.Constants(sim).AmphibAttackModifier);
        Add(de, "military:kamikaze", -MilitaryData.Effect(ae, "kamikaze"));
    }
    internal static bool Fortifiable(S.UnitDef def) => def.Strength > 0 && def.Domain == "military:domain_land" && !def.NoDefensiveBonus;
    private static void PlotDefense(Simulation sim, Unit defender, bool ignoreBuilding, ModifierSink sink)
    {
        var plot = sim.State.PlotAt(defender.Position);
        int terrain = plot.Feature is { } f ? sim.Content.Get<S.FeatureDef>(f).DefensePercent : sim.Content.Get<S.TerrainDef>(plot.Terrain).DefensePercent;
        Add(sink, "world:terrain", terrain);
        if (plot.Kind == PlotKind.Hills) Add(sink, "world:hills", MilitaryData.Constants(sim).HillsExtraDefense);
        if (plot.Kind == PlotKind.Peak) Add(sink, "world:peaks", MilitaryData.Raw(sim, "PEAK_EXTRA_DEFENSE", 75));
        if (plot.Improvement is { } improvement && (plot.Owner is null || MilitaryData.Friendly(sim, defender.Owner, plot.Owner)))
            Add(sink, improvement, sim.Content.Get<S.ImprovementDef>(improvement).DefenseModifier);
        if (sim.State.CityAt(defender.Position) is { } city) Add(sink, "cities:defense", CityDefense(sim, city, ignoreBuilding));
    }
    private static int CityDefense(Simulation sim, City city, bool ignoreBuilding)
    {
        int culture = sim.Content.Services.Get<ICityCultureService>().SingleOrDefault()?.Level(sim, city.Id).CityDefenseModifier
            ?? sim.Content.Get<S.CultureLevelDef>().All.Where(l => l.SpeedThresholds.TryGetValue(sim.Settings.GameSpeed, out var threshold) && city.Culture >= threshold)
                .OrderBy(l => l.SpeedThresholds[sim.Settings.GameSpeed]).ThenBy(l => l.Id, StringComparer.Ordinal)
                .LastOrDefault()?.CityDefenseModifier ?? 0;
        int building = ignoreBuilding ? 0 : MilitaryData.Buildings(sim, city).Sum(b => b.DefensePercent);
        int global = sim.State.Cities.Where(c => c.Owner == city.Owner).OrderBy(c => c.Id).Sum(c => MilitaryData.Buildings(sim, c).Sum(b => b.AllCityDefense));
        return Math.Max(culture, building) + global;
    }
    private static void AddLocations(Simulation sim, Unit attacker, Unit defender, Plot plot, ModifierSink ae, ModifierSink de)
    {
        var a = MilitaryData.Definition(sim, attacker); var d = MilitaryData.Definition(sim, defender);
        if (sim.State.CityAt(defender.Position) is { } city)
        {
            Add(de, "military:city", d.CityDefense + MilitaryData.Effect(de, "city_defense") - a.CityAttack - MilitaryData.Effect(ae, "city_attack"));
            Add(de, "cities:combat_type", MilitaryData.Buildings(sim, city).Sum(b => b.UnitCombatExtraStrengths.GetValueOrDefault(d.CombatType ?? "")));
        }
        if (plot.Kind == PlotKind.Hills)
            Add(de, "military:hills", d.HillsDefense + MilitaryData.Effect(de, "hills_defense") - a.HillsAttack - MilitaryData.Effect(ae, "hills_attack"));
        Add(de, "military:terrain", MilitaryData.Effect(de, "terrain_defense:" + plot.Terrain));
        // Unit definitions have terrain attack maps; native terrain defense is supplied by promotions.
        if (plot.Feature is { } feature)
            Add(de, "military:feature", d.FeatureDefenses.GetValueOrDefault(feature) + MilitaryData.Effect(de, "feature_defense:" + feature)
                - a.FeatureAttacks.GetValueOrDefault(feature) - MilitaryData.Effect(ae, "feature_attack:" + feature));
        else Add(de, "military:terrain_attack", -a.TerrainAttacks.GetValueOrDefault(plot.Terrain) - MilitaryData.Effect(ae, "terrain_attack:" + plot.Terrain));
    }
    private static void AddOpponents(S.UnitDef a, S.UnitDef d, ModifierSink ae, ModifierSink de)
    {
        Add(de, "military:class", d.UnitDefenseModifiers.GetValueOrDefault(a.DefaultUnit) - a.UnitAttackModifiers.GetValueOrDefault(d.DefaultUnit));
        Add(de, "military:combat_type", d.CombatTypeModifiers.GetValueOrDefault(a.CombatType ?? "") + MilitaryData.Effect(de, "combat_type:" + a.CombatType)
            - a.CombatTypeModifiers.GetValueOrDefault(d.CombatType ?? "") - MilitaryData.Effect(ae, "combat_type:" + d.CombatType));
        Add(de, "military:domain", d.DomainMods.GetValueOrDefault(a.Domain ?? "") + MilitaryData.Effect(de, "domain:" + a.Domain)
            - a.DomainMods.GetValueOrDefault(d.Domain ?? "") - MilitaryData.Effect(ae, "domain:" + d.Domain));
        if (a.Animal) Add(de, "military:animal", d.AnimalCombat);
        if (d.Animal) Add(de, "military:animal", -a.AnimalCombat);
    }
    private static void AddHandicap(Simulation sim, Unit a, Unit d, ModifierSink sink)
    {
        var handicap = sim.Content.Get<S.HandicapDef>(sim.Settings.Handicap);
        bool humanA = sim.State.PlayerById(a.Owner).Ai is null; bool humanD = sim.State.PlayerById(d.Owner).Ai is null;
        if (MilitaryData.Player(sim, d.Owner).Barbarian) Add(sink, "foundation:barbarian", humanA ? handicap.BarbarianBonus : handicap.AiBarbarianBonus);
        if (MilitaryData.Player(sim, a.Owner).Barbarian) Add(sink, "foundation:barbarian", -(humanD ? handicap.BarbarianBonus : handicap.AiBarbarianBonus));
        if (MilitaryData.Definition(sim, d).Animal) Add(sink, "foundation:animal", humanA ? handicap.AnimalBonus : handicap.AiAnimalBonus);
        if (MilitaryData.Definition(sim, a).Animal) Add(sink, "foundation:animal", -(humanD ? handicap.AnimalBonus : handicap.AiAnimalBonus));
    }
    private static int BarbarianOdds(Simulation sim, Unit a, Unit d, int odds, int die)
    {
        int free = sim.Content.Get<S.HandicapDef>(sim.Settings.Handicap).FreeWinsAgainstBarbarians;
        if (MilitaryData.Player(sim, d.Owner).Barbarian && MilitaryData.Player(sim, a.Owner).WinsAgainstBarbarians < free) odds = Math.Min(die / 10, odds);
        if (MilitaryData.Player(sim, a.Owner).Barbarian && MilitaryData.Player(sim, d.Owner).WinsAgainstBarbarians < free) odds = Math.Max(die * 9 / 10, odds);
        return odds;
    }
    private static void Add(ModifierSink sink, string source, int amount) => sink.Add(source, "military:combat_percent", amount);
}
