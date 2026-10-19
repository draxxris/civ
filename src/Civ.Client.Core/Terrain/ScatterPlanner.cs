namespace Civ.Client.Terrain;

/// <summary>A prop rooted on the terrain. Tile is the containing tile, used for sight updates.</summary>
/// <summary>A prop rooted on the terrain. Tile is the containing tile, used for sight updates. Stretch scales height on top
/// of Scale (cliff rock columns).</summary>
public readonly record struct ScatterInstance(string Set, int ModelIndex, float X, float Y, float Z, float Yaw, float Scale, int Tile,
    float Stretch = 1);

/// <summary>Deterministic global scatter, blended with the same warped corner triangle as the terrain.
/// Reuse one planner on the rendering thread; asset and appearance lookups are cached across chunks.</summary>
public sealed class ScatterPlanner(GameCatalog catalog)
{
    /// <summary>Per-tile extras bit: keep the centre clear for a city, unit token or improvement.</summary>
    public const int ClearCentre = 1;
    /// <summary>Per-tile extras bit: suppress all props on a cleared tile.</summary>
    public const int ClearTile = 2;
    public const double ClearRadius = 0.35;
    /// <summary>Per-tile extras bit: keep a disc round the resource model's spot clear; the spot index is in the bits
    /// from <see cref="BonusShift"/> up (see <see cref="BonusClearing"/>).</summary>
    public const int ClearBonus = 4;
    private const int BonusShift = 3;

    /// <summary>The extras bits that clear the resource model's spot (a <see cref="BonusSpot"/> index).</summary>
    public static int BonusClearing(int spot) => ClearBonus | spot << BonusShift;
    private const double Spacing = 0.2;
    /// <summary>Props keep this far from river banks, so no trunk stands in the water.</summary>
    private const double RiverMargin = 0.05;
    /// <summary>Multiplier on every appearance's scatter density (rocks excluded); a debug knob, 1 in normal play.</summary>
    public double DensityScale { get; init; } = 1;

    private readonly Dictionary<(string Feature, string Terrain), (string? Set, double Density)> _appearances = new();
    private readonly Dictionary<string, int> _modelCounts = new(StringComparer.Ordinal);
    private readonly record struct Contributor(string Set, double Weight);

    /// <summary>Plan the half-open chunk rectangle. Cells divide the wrap period exactly; hashing wrapped cell
    /// coordinates makes the candidate lattice repeat without an extra row at the seam.</summary>
    public IReadOnlyList<ScatterInstance> Plan(TerrainChunk chunk, TerrainSnapshot snapshot, TerrainField field,
        TerrainPalette palette, IReadOnlyList<int>? extras = null)
    {
        int columns = Math.Max(1, (int)Math.Round(snapshot.Period / Spacing));
        double stepX = snapshot.Period / columns;
        var result = new List<ScatterInstance>();
        var contributors = new List<Contributor>(6);
        for (int row = (int)Math.Floor(chunk.Z0 / Spacing); row < Math.Ceiling(chunk.Z1 / Spacing); row++)
            for (int col = (int)Math.Floor(chunk.X0 / stepX); col < Math.Ceiling(chunk.X1 / stepX); col++)
            {
                int hashCol = snapshot.Grid.Spec.Wrap ? (col % columns + columns) % columns : col;
                double x = (col + 0.1 + 0.8 * Hash(hashCol, row, field.Seed, 1)) * stepX;
                double z = (row + 0.1 + 0.8 * Hash(hashCol, row, field.Seed, 2)) * Spacing;
                if (!chunk.Contains(x, z)) continue;
                var instance = Candidate(x, z, hashCol, row, snapshot, field, palette, extras, contributors);
                if (instance is { } prop) result.Add(prop);
            }
        AddSeaStacks(chunk, snapshot, field, result);
        AddCliffRocks(chunk, snapshot, field, result);
        return result;
    }

    /// <summary>Lone rocks off the foot of known cliffs, standing on their shoals with the base just under the surface.
    /// Their foot is below sea level, which the renderer uses to keep them all (see ScatterRenderer).</summary>
    private void AddSeaStacks(TerrainChunk chunk, TerrainSnapshot snapshot, TerrainField field, List<ScatterInstance> result)
    {
        int models = ModelCount(SeaStackSet);
        if (models == 0) return;
        foreach (var stack in field.SeaStacks)
        {
            if (!chunk.Contains(stack.X, stack.Z) || Hidden(snapshot, stack.Tile)) continue;
            result.Add(new(SeaStackSet, Math.Min(models - 1, (int)(stack.Model * models)), (float)stack.X,
                (float)Math.Min(stack.Y - 0.01, -0.005), (float)stack.Z, (float)(stack.Yaw * Math.Tau),
                (float)(1.1 + 0.8 * stack.Scale), stack.Tile));
        }
    }

    /// <summary>The rock walls of known cliffs: stretched rock columns from the seabed to just above the bluff.</summary>
    private void AddCliffRocks(TerrainChunk chunk, TerrainSnapshot snapshot, TerrainField field, List<ScatterInstance> result)
    {
        int models = ModelCount(SeaStackSet);
        if (models == 0) return;
        foreach (var rock in field.CliffRocks(chunk.X0, chunk.X1, chunk.Z0, chunk.Z1))
        {
            if (!chunk.Contains(rock.X, rock.Z) || Hidden(snapshot, rock.Tile)) continue;
            result.Add(new(SeaStackSet, Math.Min(models - 1, (int)(rock.Model * models)), (float)rock.X, (float)rock.Y,
                (float)rock.Z, (float)rock.Yaw, (float)rock.Scale, rock.Tile, (float)rock.Stretch));
        }
    }

    /// <summary>Models of sea stacks: the same rocks that lie at mountain feet, standing taller out of the sea.</summary>
    public const string SeaStackSet = "rocks";

    private ScatterInstance? Candidate(double x, double z, int col, int row, TerrainSnapshot snapshot,
        TerrainField field, TerrainPalette palette, IReadOnlyList<int>? extras, List<Contributor> contributors)
    {
        var hex = Hex.FromPixel(x, z, 1);
        if (!snapshot.TryTile(hex, out int tile)) return null;
        if (Hidden(snapshot, tile) || Cleared(x, z, hex, tile, extras)) return null;
        var (ox, oz) = field.Warp.Sample(x, z);
        var tri = HexBlend.CornerTriangle(x + ox * field.Tuning.WarpAmplitude, z + oz * field.Tuning.WarpAmplitude);
        var weights = HexBlend.Weights(tri.Lambda, field.Tuning.MaterialSharpness);
        if (CloudCovered(snapshot, tri, weights)) return null;
        double y = field.HeightAt(x, z);
        if (y <= 0.02 || field.Rivers.Near(x, z, RiverMargin)) return null;
        // Nothing grows on a cliff face.
        if (field.CliffAt(x, z) > 0.2 && field.NormalAt(x, z).Y < 0.8f) return null;
        Gather(contributors, snapshot, field, palette, tri, weights, x, y, z);
        double total = contributors.Sum(c => c.Weight);
        if (Hash(col, row, field.Seed, 3) >= total) return null;
        string set = Choose(contributors, Hash(col, row, field.Seed, 4) * total);
        int model = (int)(Hash(col, row, field.Seed, 5) * ModelCount(set));
        return new(set, model, (float)x, (float)y, (float)z,
            (float)(Hash(col, row, field.Seed, 6) * Math.Tau),
            (float)(0.8 + Hash(col, row, field.Seed, 7) * 0.4), tile);
    }

    private void Gather(List<Contributor> contributors, TerrainSnapshot snapshot, TerrainField field,
        TerrainPalette palette, TileTriangle tri, Blend3 weights, double x, double y, double z)
    {
        contributors.Clear();
        for (int k = 0; k < 3; k++)
        {
            if (weights[k] <= 0) continue;
            int tile = snapshot.ClampedTile(tri[k]);
            var (set, density) = Appearance(snapshot.Feature[tile], snapshot.Terrain[tile], palette);
            if (set != null && ModelCount(set) > 0) contributors.Add(new(set, weights[k] * density * DensityScale));
            double rocks = RockDensity(snapshot.Relief[tile], field, x, y, z);
            if (rocks > 0 && ModelCount("rocks") > 0) contributors.Add(new("rocks", weights[k] * rocks));
        }
    }

    private (string? Set, double Density) Appearance(string? feature, string? terrain, TerrainPalette palette)
    {
        if (feature == null || terrain == null) return (null, 0);
        var key = (feature, terrain);
        if (_appearances.TryGetValue(key, out var cached)) return cached;
        var appearance = palette.ScatterFor(feature, terrain);
        return _appearances[key] = (appearance?.Scatter, Math.Clamp(appearance?.ScatterDensity ?? 0, 0, 100) / 100.0);
    }

    private int ModelCount(string set)
    {
        if (_modelCounts.TryGetValue(set, out int count)) return count;
        return _modelCounts[set] = catalog.ScatterModels(set).Count;
    }

    private static double RockDensity(ReliefClass relief, TerrainField field, double x, double y, double z)
    {
        // Hills read from their raised mounds alone; only low, gentle mountain feet get rocks.
        if (relief != ReliefClass.Peak || y > field.Tuning.PeakBase + field.Tuning.PeakRise * 0.2) return 0;
        return field.NormalAt(x, z).Y >= 0.65f ? 0.18 : 0;
    }

    private static bool CloudCovered(TerrainSnapshot snapshot, TileTriangle tri, Blend3 weights)
    {
        for (int k = 0; k < 3; k++)
            if (weights[k] > 0 && Hidden(snapshot, snapshot.ClampedTile(tri[k]))) return true;
        return false;
    }

    private static bool Hidden(TerrainSnapshot snapshot, int tile) =>
        snapshot.Sight[tile] == Sight.Hidden || snapshot.Relief[tile] == ReliefClass.Hidden;

    private static bool Cleared(double x, double z, Hex hex, int tile, IReadOnlyList<int>? extras)
    {
        int flags = extras?[tile] ?? 0;
        if ((flags & ClearTile) != 0) return true;
        if ((flags & (ClearCentre | ClearBonus)) == 0) return false;
        var (cx, cz) = hex.ToPixel(1);
        if ((flags & ClearCentre) != 0 && (x - cx) * (x - cx) + (z - cz) * (z - cz) < ClearRadius * ClearRadius) return true;
        if ((flags & ClearBonus) == 0) return false;
        var (ox, oz) = BonusSpot.Offset(flags >> BonusShift);
        double dx = x - cx - ox, dz = z - cz - oz;
        return dx * dx + dz * dz < BonusSpot.ClearRadius * BonusSpot.ClearRadius;
    }

    private static string Choose(List<Contributor> contributors, double pick)
    {
        foreach (var contributor in contributors)
        {
            pick -= contributor.Weight;
            if (pick < 0) return contributor.Set;
        }
        return contributors[^1].Set;
    }

    private static double Hash(int col, int row, int seed, uint channel)
    {
        uint h = Mix((uint)col * 0x8da6b343u ^ Mix((uint)row * 0xd8163841u ^ (uint)seed));
        return Mix(h ^ channel * 0x9e3779b9u) / 4294967296.0;
    }

    private static uint Mix(uint h)
    {
        h ^= h >> 16; h *= 0x7feb352du;
        h ^= h >> 15; h *= 0x846ca68bu;
        return h ^ (h >> 16);
    }
}
