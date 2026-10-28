using Ariel.Content;
using Ariel.Game;
using S = Ariel.Content.Schema;

namespace Ariel.Mods.World;

/// <summary>CvPlot::canHaveFeature / canHaveBonus and CvMapGenerator::calculateNumBonusesToAdd.</summary>
internal static class Placement
{
    internal static bool FeatureValid(HexGrid grid, Plot[] plots, ContentDb db, TileId tile, S.FeatureDef feature)
    {
        var plot = plots[tile.Value];
        if (!feature.Terrains.GetValueOrDefault(plot.Terrain)) return false;
        if (!FeatureGroundValid(plot, feature)) return false;
        if (feature.NoCoast && Geography.Coast(grid, plots, db, tile)) return false;
        return !feature.NoAdjacent || grid.Neighbors(tile).All(n => plots[n.Value].Feature != feature.Id);
    }

    private static bool FeatureGroundValid(Plot plot, S.FeatureDef feature)
    {
        if ((plot.Kind == PlotKind.Peak) != feature.RequiresPeaks) return false;
        if (feature.RequiresFlatlands && plot.Kind != PlotKind.Flat) return false;
        if (feature.RequiresRiver && plot.RiverEdges == 0) return false;
        return !feature.NoRiver || plot.RiverEdges == 0;
    }

    internal static void AddFeatures(HexGrid grid, Plot[] plots, DeterministicRng rng, ContentDb db)
    {
        var tuning = Geography.Tuning(db);
        foreach (var feature in db.Get<S.FeatureDef>().All)
            foreach (var tile in grid.Tiles)
            {
                if (plots[tile.Value].Feature is not null || !FeatureValid(grid, plots, db, tile, feature)) continue;
                int appearance = feature.Appearance;
                if (tuning.FeatureClimate.TryGetValue(feature.Id, out var climate))
                {
                    int latitude = Geography.Latitude(grid, tile);
                    if (latitude < climate.MinLatitude || latitude > climate.MaxLatitude) continue;
                    appearance = climate.Appearance;
                }
                if (appearance > 0 && rng.Next(10000) < appearance) plots[tile.Value].Feature = feature.Id;
            }
    }

    /// <summary>Whether the bonus may sit on the tile. Water plots are flat plots, but the relief test (flatlands, hills,
    /// peaks) is for land only, as in CvPlot::canHaveBonus: sea resources allow none of the three.</summary>
    internal static bool BonusValid(HexGrid grid, Plot[] plots, ContentDb db, TileId tile, S.BonusDef bonus, int areaSize)
    {
        var plot = plots[tile.Value];
        bool terrain = plot.Feature is { } f
            ? bonus.Features.GetValueOrDefault(f) && bonus.FeatureTerrains.GetValueOrDefault(plot.Terrain)
            : bonus.Terrains.GetValueOrDefault(plot.Terrain);
        if (!terrain || areaSize < bonus.MinAreaSize) return false;
        bool relief = Geography.Water(db, plot)
            || plot.Kind switch { PlotKind.Hills => bonus.Hills, PlotKind.Peak => bonus.Peaks, _ => bonus.Flatlands };
        if (!relief || (bonus.NoRiverSide && plot.RiverEdges != 0)) return false;
        int latitude = Geography.Latitude(grid, tile);
        return latitude >= bonus.MinLatitude && latitude <= bonus.MaxLatitude;
    }

    internal static void AddBonuses(HexGrid grid, Plot[] plots, DeterministicRng rng, ContentDb db, int players)
    {
        var areaSizes = plots.GroupBy(p => p.AreaId).ToDictionary(g => g.Key, g => g.Count());
        foreach (var bonus in db.Get<S.BonusDef>().All.OrderBy(b => b.PlacementOrder).ThenBy(b => b.Id, StringComparer.Ordinal))
        {
            // Manufactured resources have no map appearance.
            if (bonus.ConstAppearance <= 0 && bonus.AppearanceRandomRanges.All(r => r <= 0)) continue;
            var candidates = grid.Tiles.Where(t => plots[t.Value].Bonus is null && BonusValid(grid, plots, db, t, bonus, areaSizes[plots[t.Value].AreaId])).ToArray();
            Shuffle(candidates, rng);
            int count = DesiredCount(bonus, candidates.Length, players, rng);
            PlaceBonus(grid, plots, db, bonus, candidates, count, rng);
        }
    }
    private static int DesiredCount(S.BonusDef bonus, int possible, int players, DeterministicRng rng)
    {
        int appearance = bonus.ConstAppearance;
        foreach (int range in bonus.AppearanceRandomRanges) if (range > 0) appearance += rng.Next(range);
        int tiles = bonus.TilesPer > 0 ? possible / bonus.TilesPer : 0;
        int count = Math.Max(1, appearance * (tiles + players * bonus.Player / 100) / 100);
        if (bonus.MinimumPerWorld >= 0) count = Math.Max(count, bonus.MinimumPerWorld);
        if (bonus.MaximumPerWorld >= 0) count = Math.Min(count, bonus.MaximumPerWorld);
        return count;
    }
    private static void PlaceBonus(HexGrid grid, Plot[] plots, ContentDb db, S.BonusDef bonus, TileId[] candidates, int count, DeterministicRng rng)
    {
        int? area = null;
        var valid = candidates.ToHashSet();
        int placed = 0, land = 0;
        foreach (var tile in candidates)
        {
            if (placed >= count) break;
            if (!MatchingArea(plots[tile.Value], bonus, area)) continue;
            if (!CanPlace(grid, plots, db, tile, bonus)) continue;
            if (Geography.Water(db, plots[tile.Value]) && land * 100 < bonus.MinLandPercent * (placed + 1)) continue;
            plots[tile.Value].Bonus = bonus.Id;
            placed++;
            if (!Geography.Water(db, plots[tile.Value])) land++;
            area ??= plots[tile.Value].AreaId;
            var cluster = PlaceCluster(grid, plots, db, tile, bonus, valid, count - placed, rng);
            placed += cluster.Total;
            land += cluster.Land;
        }
    }
    private static bool MatchingArea(Plot plot, S.BonusDef bonus, int? area) =>
        area is null || !bonus.Area || plot.AreaId == area;

    private static (int Total, int Land) PlaceCluster(HexGrid grid, Plot[] plots, ContentDb db, TileId center,
        S.BonusDef bonus, HashSet<TileId> valid, int remaining, DeterministicRng rng)
    {
        int total = 0, land = 0;
        foreach (var tile in grid.Range(center, bonus.GroupRange))
        {
            if (total >= remaining) break;
            if (!ClusterEligible(plots, tile, center, bonus, valid)) continue;
            if (rng.Next(100) >= bonus.GroupRand) continue;
            // Native clusters bypass unique spacing, but never bypass physical placement validity.
            plots[tile.Value].Bonus = bonus.Id;
            total++;
            if (!Geography.Water(db, plots[tile.Value])) land++;
        }
        return (total, land);
    }
    private static bool ClusterEligible(Plot[] plots, TileId tile, TileId center, S.BonusDef bonus, HashSet<TileId> valid) =>
        valid.Contains(tile) && plots[tile.Value].Bonus is null
        && (!bonus.Area || plots[tile.Value].AreaId == plots[center.Value].AreaId);

    private static bool CanPlace(HexGrid grid, Plot[] plots, ContentDb db, TileId tile, S.BonusDef bonus)
    {
        if (plots[tile.Value].Bonus is not null) return false;
        int classRange = bonus.BonusClass is { } c ? db.Get<S.BonusClassDef>(c).Unique : 0;
        foreach (var neighbor in grid.Range(tile, Math.Max(bonus.Unique, classRange)))
        {
            if (plots[neighbor.Value].Bonus is not { } otherId) continue;
            var other = db.Get<S.BonusDef>(otherId);
            int distance = grid.Distance(tile, neighbor);
            if (other.Id == bonus.Id && distance <= bonus.Unique) return false;
            if (classRange > 0 && other.BonusClass == bonus.BonusClass && distance <= classRange) return false;
        }
        return true;
    }
    internal static void AddGoodies(HexGrid grid, Plot[] plots, DeterministicRng rng, ContentDb db)
    {
        foreach (var goody in db.Get<S.ImprovementDef>().All.Where(i => i.Goody && i.TilesPerGoody > 0))
        {
            var candidates = grid.Tiles.Where(t => !Geography.Water(db, plots[t.Value]) && !Geography.Impassable(db, plots[t.Value])).ToArray();
            Shuffle(candidates, rng);
            int remaining = candidates.Length / goody.TilesPerGoody;
            foreach (var tile in candidates)
            {
                if (remaining == 0) break;
                if (grid.Range(tile, goody.GoodyRange).Any(t => plots[t.Value].Improvement == goody.Id)) continue;
                plots[tile.Value].Improvement = goody.Id;
                remaining--;
            }
        }
    }
    private static void Shuffle(TileId[] tiles, DeterministicRng rng)
    {
        for (int i = tiles.Length - 1; i > 0; i--) { int j = rng.Next(i + 1); (tiles[i], tiles[j]) = (tiles[j], tiles[i]); }
    }
}
