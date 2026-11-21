using Ariel.Content;
using Ariel.Game;
using Ariel.Rules.Foundation;
using S = Ariel.Content.Schema;
namespace Ariel.Rules.Cities;

/// <summary>A city leaves its owner and founds a nation. Unlike a capture nothing is destroyed or looted: the same
/// people keep the same city under a new flag.</summary>
internal static class CitySecession
{
    internal static string? Validate(Simulation sim, int cityId)
    {
        if (sim.State.FindCity(cityId) is not { } city) return "Unknown city";
        if (PlayerRules.IsNation(sim, city.Owner))
        {
            if (CityCapital.IsCapital(sim, city)) return "A capital does not break away";
            if (CityData.Owned(sim, city.Owner).Count() <= 1) return "A nation's last city does not break away";
        }
        if (Leader(sim, city.Owner) is null) return "No computer player can lead a new nation";
        return FreeNation(sim) ? null : "Every civilization is already in the game";
    }

    internal static int Secede(Simulation sim, City city, SecessionTerms terms)
    {
        if (Validate(sim, city.Id) is { } refusal) throw new InvalidOperationException(refusal);
        int former = city.Owner;
        var state = CityData.Edit(sim, city);
        var formerCivilization = sim.Content.Get<S.CivilizationDef>(sim.State.PlayerById(former).Civilization);
        // A city that remembers a civilization no longer in the game takes it up again; a colony takes its owner's
        // derivative; otherwise the new nation is drawn from the civilizations that are left.
        var nation = sim.Rules().Peoples.DrawNation(sim, [state.OriginalCivilization, formerCivilization.DerivativeCivilization])!.Value;
        int player = sim.Rules().Peoples.AddPlayer(sim, new(nation.Civilization, nation.Leader, Leader(sim, former),
            Techs: sim.TeamOf(former).Techs.ToArray()));

        var units = sim.Rules().Units;
        if (PlayerRules.IsNation(sim, former)) units.Expel(sim, city.Position, former);
        else foreach (var unit in sim.State.UnitsAt(city.Position).Where(u => u.Owner == former).OrderBy(u => u.Id).ToArray())
            units.Transfer(sim, unit.Id, player);

        var seceded = Hand(sim, city, state, player);
        TakeLand(sim, seceded, former, player);
        CityCapital.RelocateIfNeeded(sim, former);
        CityCapital.RelocateIfNeeded(sim, player);
        foreach (var other in CityData.Owned(sim, former).ToArray()) sim.Rules().CityWork.Assign(sim, other.Id);
        sim.Rules().CityWork.Assign(sim, seceded.Id);
        // The newcomer knows what its former owner's team knew, so its defenders are the best it could train now.
        if (terms.Garrison > 0 && RoleUnits.Best(sim, player, RoleUnits.CityDefense) is { } defender)
            for (int i = 0; i < terms.Garrison; i++) units.Create(sim, player, defender.Id, seceded.Position);

        var relations = sim.Rules().Relations;
        int formerTeam = sim.TeamOf(former).Id, team = sim.TeamOf(player).Id;
        relations.EstablishContact(sim, formerTeam, team);
        if (terms.WarWithFormerOwner && !sim.State.TeamById(team).Wars.Contains(formerTeam) && relations.ValidateWar(sim, player, former) is null)
            relations.DeclareWar(sim, player, former);
        sim.Rules().Visibility.Recompute(sim);
        relations.DiscoverContacts(sim);
        sim.Events.Publish(new CitySeceded(city.Id, former, player));
        return player;
    }

    /// <summary>The same city under a new owner: its people, food, buildings, work and culture stay, the capital
    /// building and the production queue do not, and it is settled, not occupied.</summary>
    private static City Hand(Simulation sim, City city, CityRuleState state, int player)
    {
        state.OriginalCivilization = PlayerRules.IsNation(sim, city.Owner)
            ? state.OriginalCivilization ?? sim.State.PlayerById(city.Owner).Civilization : null;
        state.CapturedFrom = city.Owner;
        state.TurnAcquired = sim.State.Turn;
        state.Overflow = 0; state.FeatureProduction = 0; state.PendingCommit = null;
        state.PausedProduction.Clear(); state.ManualWork = false; state.Disorder = false;
        state.OccupationTurns = 0; state.Governance = CityGovernance.Integrated;
        var handed = new City
        {
            Id = city.Id, Owner = player, Name = city.Name, Position = city.Position, Population = city.Population,
            FoodStored = city.FoodStored, Culture = city.Culture, NextOrderId = city.NextOrderId, Extensions = city.Extensions,
            Buildings = city.Buildings.Where(id => !sim.Content.Get<S.BuildingDef>(id).Capital).ToList(),
            Worked = new(city.Worked), Specialists = new(city.Specialists, StringComparer.Ordinal),
        };
        sim.State.RemoveCity(city.Id); sim.State.AddCity(handed);
        return handed;
    }

    /// <summary>The city's plot, and every plot in its cultural reach that the former owner held and that lies nearer
    /// this city than any city the former owner keeps, go with it, with the former owner's culture on them.</summary>
    private static void TakeLand(Simulation sim, City city, int former, int player)
    {
        var grid = sim.State.Grid;
        var center = CityData.Tile(sim, city);
        var kept = CityData.Owned(sim, former).Select(c => c.Position).ToArray();
        foreach (var tile in grid.Range(center, CityCulture.CultureRadius(sim, city)))
        {
            var plot = sim.State.Plots[tile.Value];
            if (tile != center && plot.Owner != former) continue;
            int distance = grid.Distance(grid.HexOf(tile), city.Position);
            if (tile != center && kept.Any(position => grid.Distance(grid.HexOf(tile), position) <= distance)) continue;
            if (plot.Culture.Remove(former, out long culture))
                plot.Culture[player] = plot.Culture.GetValueOrDefault(player) + culture;
            plot.Owner = player;
        }
    }

    /// <summary>The computer player that leads the newcomer: its former owner's, else the lowest-numbered player's that
    /// has one, else the first registered.</summary>
    private static string? Leader(Simulation sim, int former) =>
        sim.State.PlayerById(former).Ai
        ?? sim.State.Players.OrderBy(p => p.Id).Select(p => p.Ai).FirstOrDefault(ai => ai is not null)
        ?? sim.Content.PlayerAIs.All.OrderBy(a => a.Id, StringComparer.Ordinal).FirstOrDefault()?.Id;

    private static bool FreeNation(Simulation sim)
    {
        var taken = sim.State.Players.Select(p => p.Civilization).ToHashSet(StringComparer.Ordinal);
        var leaders = sim.State.Players.Select(p => p.Leader).ToHashSet(StringComparer.Ordinal);
        return sim.Content.Get<S.CivilizationDef>().All.Any(c => c.Playable && c.AiPlayable && !taken.Contains(c.Id)
            && c.Leaders.Any(l => l.Value && !leaders.Contains(l.Key) && sim.Content.TryGet<S.LeaderDef>(l.Key, out _)));
    }
}

/// <summary>A city a faction has held for the faction's <see cref="S.FactionDef.BreakawayTurns"/> breaks away as a
/// nation, at peace with everyone but factions that are always at war. A global step at the end of the turn, so the
/// newcomer's first turn is a whole one.</summary>
internal sealed class FactionBreakaway : ITurnSystem
{
    public string Id => "cities:faction_breakaway";
    public int Order => TurnPhase.GameEnd;
    public void Run(Simulation sim)
    {
        foreach (var city in sim.State.Cities.OrderBy(c => c.Id).ToArray())
        {
            if (PlayerRules.Faction(sim, city.Owner) is not { BreakawayTurns: > 0 } faction) continue;
            if (sim.State.Turn - CityData.Read(sim, city).TurnAcquired < GamePace.Scale(sim, faction.BreakawayTurns)) continue;
            if (CitySecession.Validate(sim, city.Id) is null) CitySecession.Secede(sim, city, new SecessionTerms(Garrison: faction.BreakawayGarrison));
        }
    }
}
