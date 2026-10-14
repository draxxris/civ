using Civ.Content;
using Civ.Game;
using S = Civ.Content.Schema;

namespace Civ.Mods.GreatPeople;

internal static class GoldenAges
{
    internal static int Length(Simulation sim, int player)
    {
        long turns = GreatPeopleData.Raw(sim, "GOLDEN_AGE_LENGTH", GreatPeopleData.Constants(sim).Values.GoldenAgeTurns);
        turns = turns * GreatPeopleData.Speed(sim).GoldenAgePercent / 100;
        long modifier = sim.State.Cities.Where(c => c.Owner == player).SelectMany(c => GreatPeopleData.Active(sim, c)).Sum(b => (long)b.GoldenAgeModifier);
        modifier += GreatPeopleData.Effects(sim, ModifierScope.Player, player).Ledger.Total("great_people:golden_age_length_percent");
        return (int)Math.Clamp(turns * Math.Max(0, 100 + modifier) / 100, 1, int.MaxValue);
    }
    internal static void Start(Simulation sim, int player, bool fromBuilding)
    {
        var state = GreatPeopleData.Edit(sim, player);
        int turns = Length(sim, player);
        if (state.GoldenAgeTurns == 0) state.FirstGoldenAgeTurn = sim.State.Turn + (fromBuilding ? 1 : 0);
        state.GoldenAgeTurns += turns;
        Sync(sim, player, true);
        sim.Events.Publish(new GoldenAgeStarted(player, turns));
    }
    internal static void Completed(Simulation sim, BuildingCompleted completed)
    {
        if (sim.Content.Get<S.BuildingDef>(completed.Building).GoldenAge)
            Start(sim, sim.State.CityById(completed.City).Owner, true);
    }
    internal static void Tick(Simulation sim, int player)
    {
        var state = sim.State.PlayerById(player).Extensions.Get<GreatPeoplePlayerState>(sim.Content);
        if (state is null || state.GoldenAgeTurns <= 0 || sim.State.Turn < state.FirstGoldenAgeTurn) return;
        state.GoldenAgeTurns--;
        Sync(sim, player, state.GoldenAgeTurns > 0);
    }
    private static void Sync(Simulation sim, int player, bool active)
    {
        var owner = sim.State.PlayerById(player);
        var cityState = owner.Extensions.Get<CityPlayerState>(sim.Content) ?? new();
        cityState.GoldenAge = active;
        owner.Extensions.Set(sim.Content, cityState);
    }
}

internal sealed class GreatPeopleEffects : IModifierSource, IPlotYieldFinalizer
{
    public void Contribute(ModifierContext context, ModifierSink sink)
    {
        if (context.Scope != ModifierScope.City || context.City is not { } city) return;
        foreach (var pair in GreatPeopleData.Read(context.Sim, city).SettledSpecialists)
            sink.Add("great_people:settled", "cities:free:" + pair.Key, pair.Value);
        if (GreatPeopleData.Read(context.Sim, context.Player).GoldenAgeTurns > 0)
            sink.Add("great_people:golden_age", "cities:great_people_rate_percent", GreatPeopleData.Constants(context.Sim).Values.GoldenAgeGreatPeoplePercent);
    }
    public YieldVector Adjust(Simulation sim, int player, TileId tile, Plot plot, YieldVector yields, ModifierSink sink)
    {
        if (GreatPeopleData.Read(sim, player).GoldenAgeTurns <= 0) return yields;
        long Extra(string id, long value)
        {
            var def = sim.Content.Get<S.YieldDef>(id);
            return value >= def.GoldenAgeYieldThreshold ? def.GoldenAgeYield : 0;
        }
        var extra = new YieldVector(Extra("foundation:yield_food", yields.Food), Extra("foundation:yield_production", yields.Production), Extra("foundation:yield_commerce", yields.Commerce));
        sink.AddYield("great_people:golden_age", extra);
        return yields + extra;
    }
}
