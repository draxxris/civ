using Ariel.Game;
using S = Ariel.Content.Schema;

namespace Ariel.Mods.World;

/// <summary>Worked improvement discovery and optional mine/deposit exhaustion.</summary>
internal sealed class ResourceTurnRules
{
    private readonly Simulation _sim;
    private readonly bool _depletion;
    private readonly int _constructPercent;
    private readonly Dictionary<int, int> _areaSizes;
    private readonly Dictionary<string, int> _bonusCounts;
    private readonly S.ImprovementDef? _depletedMine;
    private readonly long _worldSizeFactor;

    internal ResourceTurnRules(Simulation sim)
    {
        _sim = sim;
        _depletion = sim.IsOptionEnabled("foundation:gameoption_resource_depletion");
        _constructPercent = Math.Max(0, sim.Content.Get<S.GameSpeedDef>(sim.Settings.GameSpeed).ConstructPercent);
        _areaSizes = sim.State.Plots.GroupBy(p => p.AreaId).ToDictionary(g => g.Key, g => g.Count());
        _bonusCounts = sim.State.Plots.Where(p => p.Bonus is not null).GroupBy(p => p.Bonus!, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.Count(), StringComparer.Ordinal);
        _depletedMine = sim.Content.Get<S.ImprovementDef>().All.FirstOrDefault(i => i.DepletedMine);
        // Registries are ordinal, not the reference's size enum. Rank by map area, as the setup catalog does.
        var sizes = sim.Content.Get<S.WorldSizeDef>().All.OrderBy(w => (long)w.GridWidth * w.GridHeight)
            .ThenBy(w => w.Id, StringComparer.Ordinal).Select(w => w.Id).ToList();
        _worldSizeFactor = 12L * Math.Max(1, sizes.IndexOf(sim.Settings.WorldSize ?? "") + 1);
    }

    internal void Advance(TileId tile, bool worked)
    {
        var plot = _sim.State.Plots[tile.Value];
        if (plot.Owner is not { } owner || plot.Improvement is not { } id) return;
        var improvement = _sim.Content.Get<S.ImprovementDef>(id);
        if (worked)
        {
            if (plot.Bonus is null) Discover(tile, plot, owner, improvement);
            if (_depletion) ExhaustMine(tile, plot, owner, improvement);
        }
        if (_depletion) ExhaustDeposit(tile, plot, owner);
    }

    private void Discover(TileId tile, Plot plot, int owner, S.ImprovementDef improvement)
    {
        foreach (var bonus in _sim.Content.Get<S.BonusDef>().All)
        {
            int range = improvement.BonusEffects.GetValueOrDefault(bonus.Id)?.DiscoveryRandomRange ?? 0;
            if (range <= 0 || !Revealed(owner, bonus)) continue;
            if (!Placement.BonusValid(_sim.State.Grid, _sim.State.Plots, _sim.Content, tile, bonus, _areaSizes[plot.AreaId])) continue;
            Int128 odds = (Int128)range * _constructPercent / 100;
            if (_depletion) odds /= 10;
            if (_sim.Rng.Next(WorldTurnMath.Range(odds)) != 0) continue;
            plot.Bonus = bonus.Id;
            plot.DepletedMine = false;
            _bonusCounts[bonus.Id] = _bonusCounts.GetValueOrDefault(bonus.Id) + 1;
            _sim.Events.Publish(new BonusDiscovered(tile, bonus.Id, owner));
            break;
        }
    }

    private void ExhaustMine(TileId tile, Plot plot, int owner, S.ImprovementDef improvement)
    {
        if (improvement.DepletionRand <= 0 || _depletedMine is null) return;
        if (RevealedEffect(plot, owner, improvement)?.MakesValid == true) return;
        Int128 odds = (Int128)improvement.DepletionRand * improvement.DepletionRand * _constructPercent / 100;
        if (odds <= 0 || _sim.Rng.Next(WorldTurnMath.Range(odds)) != 0) return;
        plot.Improvement = _depletedMine.Id;
        plot.ImprovementTurns = 0;
        plot.DepletedMine = true;
        _sim.Events.Publish(new MineDepleted(tile, improvement.Id, _depletedMine.Id, owner));
    }

    private void ExhaustDeposit(TileId tile, Plot plot, int owner)
    {
        var improvement = _sim.Content.Get<S.ImprovementDef>(plot.Improvement!);
        var effect = RevealedEffect(plot, owner, improvement);
        if (effect?.MakesValid != true || effect.DepletionRandomRange <= 0) return;
        string bonus = plot.Bonus!;
        Int128 odds = (Int128)effect.DepletionRandomRange * _constructPercent / 100;
        odds = odds * _worldSizeFactor / _bonusCounts[bonus];
        // Truncation to zero disables exhaustion, as in the reference.
        if (odds <= 0 || _sim.Rng.Next(WorldTurnMath.Range(odds)) != 0) return;
        plot.Bonus = null;
        _bonusCounts[bonus]--;
        _sim.Events.Publish(new BonusDepleted(tile, bonus, owner));
    }

    private S.ImprovementBonus? RevealedEffect(Plot plot, int owner, S.ImprovementDef improvement)
    {
        if (plot.Bonus is not { } id || !Revealed(owner, _sim.Content.Get<S.BonusDef>(id))) return null;
        return improvement.BonusEffects.GetValueOrDefault(id);
    }

    // Exhaustion uses revealed bonuses, including obsolete ones; yield queries exclude obsolete bonuses.
    private bool Revealed(int owner, S.BonusDef bonus) => bonus.RevealTech is null || _sim.KnowsTech(owner, bonus.RevealTech);

    internal static void RevealResources(Simulation sim, TechDiscovered discovery)
    {
        foreach (var plot in sim.State.Plots)
        {
            if (plot.Owner is not { } owner || sim.TeamOf(owner).Id != discovery.Team) continue;
            if (plot.Bonus is { } id && sim.Content.Get<S.BonusDef>(id).RevealTech == discovery.Tech)
                plot.DepletedMine = false;
        }
    }
}
