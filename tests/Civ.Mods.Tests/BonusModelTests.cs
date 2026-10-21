using System.Text;
using System.Text.Json;
using Civ.Client.Terrain;
using Civ.Game;
using Xunit;
using S = Civ.Content.Schema;

namespace Civ.Mods.Tests;

/// <summary>Map resources (bonuses) have 3D models on the map: every resource that a terrain or feature may carry has
/// one, manufactured goods have none, and the model stands on a spot that suits the tile.</summary>
public sealed class BonusModelTests(LoadedMods mods) : IClassFixture<LoadedMods>
{
    private const string Manufactured = "world:bonusclass_manufactured";

    private IEnumerable<S.BonusDef> Bonuses => mods.Content.Get<S.BonusDef>().All;

    [Fact]
    public void Every_resource_that_can_appear_on_the_map_has_a_model_and_manufactured_ones_do_not()
    {
        var onMap = Bonuses.Where(BonusSpot.OnMap).Select(b => b.Id).Order(StringComparer.Ordinal).ToArray();
        var manufactured = Bonuses.Where(b => b.BonusClass == Manufactured).Select(b => b.Id).ToArray();
        Assert.Equal(51, onMap.Length);
        Assert.Equal(15, manufactured.Length);
        Assert.Empty(onMap.Intersect(manufactured));
        Assert.All(onMap, id => Assert.True(mods.Catalog.ModelPath(id) is not null, $"{id} has no art/models/<id>.glb"));
        Assert.All(Bonuses.Where(b => !BonusSpot.OnMap(b)), b => Assert.Null(mods.Catalog.ModelPath(b.Id)));
        // Goods that only buildings make (future goods, diesel) are not map resources either.
        Assert.All(new[] { "world:diesel", "world:nanobots", "world:smart_medicine" },
            id => Assert.DoesNotContain(id, onMap));
    }

    [Fact]
    public void Resource_models_fit_their_spot_and_sea_models_float()
    {
        var water = WaterTerrains();
        foreach (var bonus in Bonuses.Where(BonusSpot.OnMap))
        {
            var (radius, low) = Extent(mods.Catalog.ModelPath(bonus.Id)!);
            bool sea = SeaOnly(bonus, water);
            // From any spot, the model stays inside its own tile (inner radius ~0.87); land models stay near the
            // cleared disc so trees do not grow through them.
            Assert.True(radius <= (sea ? 0.45 : 0.34), $"{bonus.Id} reaches {radius:F3} from its spot");
            if (sea) Assert.True(low < 0, $"{bonus.Id} should reach below the sea surface");
        }
    }

    [Theory]
    [InlineData("world:continents", 3UL)]
    [InlineData("world:inland_sea", 42UL)]
    [InlineData("world:twin_continents", 7UL)]
    public void Generated_resources_respect_their_terrain_and_shore(string generator, ulong seed)
    {
        var sim = new Simulation(mods.Content, GameFactory.Settings(seed) with { MapGenerator = generator });
        var water = WaterTerrains();
        int placed = 0;
        foreach (var tile in sim.State.Grid.Tiles)
        {
            var plot = sim.State.Plots[tile.Value];
            if (plot.Bonus is not { } id) continue;
            var bonus = mods.Content.Get<S.BonusDef>(id);
            placed++;
            Assert.True(BonusSpot.OnMap(bonus), $"{id} is not a map resource");
            Assert.NotEqual(Manufactured, bonus.BonusClass);
            bool allowed = plot.Feature is { } feature
                ? bonus.Features.GetValueOrDefault(feature) && bonus.FeatureTerrains.GetValueOrDefault(plot.Terrain)
                : bonus.Terrains.GetValueOrDefault(plot.Terrain);
            Assert.True(allowed, $"{id} on {plot.Terrain}/{plot.Feature}");
            bool wet = water.Contains(plot.Terrain);
            if (!wet)
                Assert.True(plot.Kind switch { PlotKind.Hills => bonus.Hills, PlotKind.Peak => bonus.Peaks, _ => bonus.Flatlands },
                    $"{id} on {plot.Kind}");
            // Sea models float; land models stand on the ground.
            if (SeaOnly(bonus, water)) Assert.True(wet, $"{id} on land");
            if (!bonus.Terrains.Concat(bonus.FeatureTerrains).Any(t => t.Value && water.Contains(t.Key))) Assert.False(wet, $"{id} at sea");
        }
        Assert.True(placed > 0);
    }

    [Fact]
    public void Sea_resources_spawn_in_the_water()
    {
        // Water plots are "flat" plots, but sea resources allow neither flatlands nor hills: like the original rules,
        // the relief test applies to land only.
        var sim = GameFactory.Create(mods.Content, 5);
        var water = WaterTerrains();
        var placed = sim.State.Plots.Where(p => p.Bonus is not null).Select(p => p.Bonus!).ToHashSet(StringComparer.Ordinal);
        var sea = Bonuses.Where(b => SeaOnly(b, water)).Select(b => b.Id).ToArray();
        Assert.Equal(["world:clam", "world:crab", "world:fish", "world:methane", "world:pearls", "world:shrimp", "world:whale"],
            sea.Order(StringComparer.Ordinal));
        Assert.All(sea, id => Assert.Contains(id, placed));
        Assert.Contains(sim.State.Plots, p => p.Bonus == "world:oil" && water.Contains(p.Terrain));
    }

    [Fact]
    public void Spot_prefers_the_back_left_corner_on_open_land_and_stays_inside_the_tile()
    {
        var snapshot = TerrainTestMaps.Uniform(ReliefClass.Flat);
        var field = new TerrainField(snapshot, 3, new TerrainTuning { WarpAmplitude = 0, FlatNoise = 0 });
        var (x, z) = TerrainTestMaps.Centre(snapshot.Grid.HexOf(new TileId(5 * 16 + 5)));
        Assert.Equal(0, BonusSpot.Choose(field, x, z, water: false));
        var (ox, oz) = BonusSpot.Offset(0);
        Assert.True(ox < 0 && oz < 0, "spot 0 is back-left");
        for (int spot = 0; spot < BonusSpot.Count; spot++)
        {
            var (sx, sz) = BonusSpot.Offset(spot);
            Assert.Equal(BonusSpot.Distance, Math.Sqrt(sx * sx + sz * sz), 6);
            // The cleared disc stays inside the tile: every edge line (inner radius sqrt(3)/2) is at least its radius away.
            for (int edge = 0; edge < 6; edge++)
            {
                double a = edge * Math.PI / 3;
                Assert.True(Math.Sqrt(3) / 2 - (sx * Math.Cos(a) + sz * Math.Sin(a)) >= BonusSpot.ClearRadius);
            }
        }
    }

    [Fact]
    public void Spot_moves_away_from_rivers()
    {
        var snapshot = TerrainTestMaps.Mixed(seed: 11);
        var field = new TerrainField(snapshot, 3);
        int moved = 0, checkedTiles = 0;
        for (int tile = 0; tile < snapshot.Relief.Length; tile++)
        {
            if (snapshot.Rivers[tile] == 0 || snapshot.Relief[tile] is not (ReliefClass.Flat or ReliefClass.Hills)) continue;
            var (x, z) = TerrainTestMaps.Centre(snapshot.Grid.HexOf(new TileId(tile)));
            bool Wet(int spot)
            {
                // The spot or the edge of a model standing on it.
                var (ox, oz) = BonusSpot.Offset(spot);
                return Enumerable.Range(-1, 7).Any(k => field.Rivers.Near(
                    x + ox + (k < 0 ? 0 : 0.16 * Math.Cos(k * Math.PI / 3)), z + oz + (k < 0 ? 0 : 0.16 * Math.Sin(k * Math.PI / 3)), 0.06));
            }
            int chosen = BonusSpot.Choose(field, x, z, water: false);
            checkedTiles++;
            if (Wet(0)) moved++;
            if (Enumerable.Range(0, BonusSpot.Count).Any(s => !Wet(s))) Assert.False(Wet(chosen), $"tile {tile} spot {chosen}");
        }
        Assert.True(checkedTiles > 10);
        Assert.True(moved > 0, "no preferred spot was in a river, so nothing was tested");
    }

    [Fact]
    public void Spot_stays_in_the_water_for_sea_resources_and_ashore_for_land_ones()
    {
        // West half deep water, east half land: the tiles on the shore line offer both kinds of spot.
        var snapshot = TerrainTestMaps.Uniform(ReliefClass.Flat);
        for (int row = 0; row < snapshot.Height; row++)
            for (int col = 0; col < 8; col++) snapshot.Relief[row * snapshot.Width + col] = ReliefClass.DeepWater;
        var field = new TerrainField(snapshot, 3, new TerrainTuning { WarpAmplitude = 0, FlatNoise = 0 });
        foreach (int col in new[] { 7, 8 })
        {
            int tile = 6 * snapshot.Width + col;
            var (x, z) = TerrainTestMaps.Centre(snapshot.Grid.HexOf(new TileId(tile)));
            bool water = col == 7;
            var (ox, oz) = BonusSpot.Offset(BonusSpot.Choose(field, x, z, water));
            double h = field.HeightAt(x + ox, z + oz);
            if (water) Assert.True(h < -0.04, $"sea spot on the beach ({h:F3})");
            else Assert.True(h > 0.02, $"land spot in the sea ({h:F3})");
        }
    }

    [Fact]
    public void Scatter_keeps_the_resource_spot_clear_but_not_the_rest_of_the_tile()
    {
        var palette = new TerrainPalette(mods.Catalog);
        var session = TerrainTestMaps.RevealedSession(mods.Content, 16, 16);
        foreach (var plot in session.Sim.State.Plots) plot.Feature = "world:forest";
        var snapshot = TerrainSnapshot.From(session, palette.LayerOf, palette.FeatureGroundOf);
        var field = new TerrainField(snapshot, 17, new TerrainTuning { WarpAmplitude = 0, FlatNoise = 0 });
        var extras = new int[snapshot.Relief.Length];
        int spot = 2;
        Array.Fill(extras, ScatterPlanner.BonusClearing(spot));
        var planner = new ScatterPlanner(mods.Catalog);
        var chunk = new TerrainChunk(0, 0, 0, 0, snapshot.Width, -2, snapshot.Height + 1);
        var open = planner.Plan(chunk, snapshot, field, palette);
        var cleared = planner.Plan(chunk, snapshot, field, palette, extras);
        var (ox, oz) = BonusSpot.Offset(spot);
        bool InSpot(ScatterInstance p)
        {
            // The unwrapped hex under the prop: props past the seam keep their unwrapped x.
            var (cx, cz) = Hex.FromPixel(p.X, p.Z, 1).ToPixel(1);
            double dx = p.X - cx - ox, dz = p.Z - cz - oz;
            return dx * dx + dz * dz < BonusSpot.ClearRadius * BonusSpot.ClearRadius;
        }
        Assert.Contains(open, InSpot);
        Assert.DoesNotContain(cleared, InSpot);
        Assert.Equal(open.Count(p => !InSpot(p)), cleared.Count);
    }

    [Fact]
    public void Resource_showcase_scenario_puts_every_resource_on_a_tile_it_may_spawn_on()
    {
        string file = Path.Combine(AppContext.BaseDirectory, "../../../../../tools/shots/scenarios/resources.json");
        using var doc = JsonDocument.Parse(File.ReadAllText(file));
        var shown = new List<string>();
        foreach (var tile in doc.RootElement.GetProperty("tiles").EnumerateArray())
        {
            if (!tile.TryGetProperty("bonus", out var id)) continue;
            var bonus = mods.Content.Get<S.BonusDef>(id.GetString()!);
            string terrain = tile.GetProperty("terrain").GetString()!;
            string kind = tile.GetProperty("kind").GetString()!;
            bool allowed = tile.TryGetProperty("feature", out var feature)
                ? bonus.Features.GetValueOrDefault(feature.GetString()!) && bonus.FeatureTerrains.GetValueOrDefault(terrain)
                : bonus.Terrains.GetValueOrDefault(terrain);
            Assert.True(allowed, $"{bonus.Id} on {terrain}");
            bool sea = mods.Content.Get<S.TerrainDef>(terrain).IsWater;
            Assert.True(sea || (kind == "Hills" ? bonus.Hills : kind == "Peak" ? bonus.Peaks : bonus.Flatlands), $"{bonus.Id} on {kind}");
            shown.Add(bonus.Id);
        }
        Assert.Equal(Bonuses.Where(BonusSpot.OnMap).Select(b => b.Id).Order(StringComparer.Ordinal), shown.Order(StringComparer.Ordinal));
    }

    private HashSet<string> WaterTerrains() =>
        mods.Content.Get<S.TerrainDef>().All.Where(t => t.IsWater).Select(t => t.Id).ToHashSet(StringComparer.Ordinal);

    private static bool SeaOnly(S.BonusDef bonus, HashSet<string> water)
    {
        var allowed = bonus.Terrains.Where(t => t.Value).Select(t => t.Key)
            .Concat(bonus.Features.Any(f => f.Value) ? bonus.FeatureTerrains.Where(t => t.Value).Select(t => t.Key) : [])
            .ToArray();
        return allowed.Length > 0 && allowed.All(water.Contains);
    }

    /// <summary>The model's reach from its origin in the ground plane (each part's offset plus its bounding box) and its
    /// lowest point, read from the GLB's JSON chunk. The box corners are an upper bound.</summary>
    private static (double Radius, double Low) Extent(string path)
    {
        var bytes = File.ReadAllBytes(path);
        Assert.True(Encoding.ASCII.GetString(bytes, 0, 4) == "glTF", $"{path} is not a GLB (run git lfs pull)");
        int length = BitConverter.ToInt32(bytes, 12);
        using var json = JsonDocument.Parse(bytes.AsMemory(20, length));
        var root = json.RootElement;
        var accessors = root.GetProperty("accessors");
        var meshes = root.GetProperty("meshes");
        double radius = 0, low = double.MaxValue;
        foreach (var node in root.GetProperty("nodes").EnumerateArray())
        {
            double tx = 0, tz = 0;
            if (node.TryGetProperty("translation", out var t)) (tx, tz) = (t[0].GetDouble(), t[2].GetDouble());
            int position = meshes[node.GetProperty("mesh").GetInt32()].GetProperty("primitives")[0]
                .GetProperty("attributes").GetProperty("POSITION").GetInt32();
            var min = accessors[position].GetProperty("min");
            var max = accessors[position].GetProperty("max");
            low = Math.Min(low, min[1].GetDouble());
            foreach (double x in new[] { min[0].GetDouble(), max[0].GetDouble() })
                foreach (double z in new[] { min[2].GetDouble(), max[2].GetDouble() })
                    radius = Math.Max(radius, Math.Sqrt((tx + x) * (tx + x) + (tz + z) * (tz + z)));
        }
        return (radius, low);
    }
}
