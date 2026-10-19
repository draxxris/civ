using Civ.Game;
using Schema = Civ.Content.Schema;

namespace Civ.Client.Terrain;

/// <summary>Which height profile a tile uses. <see cref="Hidden"/> overrides everything else.</summary>
public enum ReliefClass : byte { Hidden, DeepWater, ShallowWater, Flat, Hills, Peak }

/// <summary>Per-tile terrain inputs as the local team knows them, indexed by row-major <see cref="TileId"/> value.
/// Tiles the team has never explored are <see cref="ReliefClass.Hidden"/> and carry no layer, feature, river or owner
/// data, so nothing derived from a snapshot can reveal them.</summary>
public sealed class TerrainSnapshot
{
    private static readonly double Sqrt3 = Math.Sqrt(3);

    /// <summary>An all-hidden snapshot for a map.</summary>
    public TerrainSnapshot(MapSpec map)
    {
        Grid = new HexGrid(map);
        int n = Grid.Count;
        Layer = new byte[n];
        Relief = new ReliefClass[n];
        Rivers = new byte[n];
        Cliffs = new byte[n];
        FeatureGround = new byte[n];
        Owner = new byte[n];
        Sight = new Sight[n];
        Terrain = new string?[n];
        Feature = new string?[n];
    }

    /// <summary>Map geometry and wrap policy.</summary>
    public HexGrid Grid { get; }
    /// <summary>Map columns.</summary>
    public int Width => Grid.Spec.Width;
    /// <summary>Map rows.</summary>
    public int Height => Grid.Spec.Height;
    /// <summary>World width of the map (<c>Width · √3</c>); the east-west repeat distance when the map wraps.</summary>
    public double Period => Width * Sqrt3;

    /// <summary>Terrain layer index from the palette.</summary>
    public byte[] Layer { get; }
    /// <summary>Height profile class.</summary>
    public ReliefClass[] Relief { get; }
    /// <summary>Six river-edge bits in <see cref="Hex.Directions"/> order, mirrored on the neighbour.</summary>
    public byte[] Rivers { get; }
    /// <summary>Six cliff-edge bits in <see cref="Hex.Directions"/> order, as this tile's memory knows them. A renderer
    /// trusts a bit only when both tiles are known and one is land, the other water.</summary>
    public byte[] Cliffs { get; }
    /// <summary>Feature ground index (0 = none).</summary>
    public byte[] FeatureGround { get; }
    /// <summary>Owner player id + 1 (0 = unowned).</summary>
    public byte[] Owner { get; }
    /// <summary>Local team sight.</summary>
    public Sight[] Sight { get; }
    /// <summary>Known terrain content ids; hidden tiles have no content ids.</summary>
    public string?[] Terrain { get; }
    /// <summary>Known feature content ids, for terrain-specific scatter lookup.</summary>
    public string?[] Feature { get; }

    /// <summary>Tile for an axial hex, or false when it is off the map. Wrapping maps resolve every column.</summary>
    public bool TryTile(Hex hex, out int tile)
    {
        bool ok = Grid.TryTile(hex, out var id);
        tile = id.Value;
        return ok;
    }

    /// <summary>Tile for any axial hex, clamping rows beyond the north and south edges to the nearest row
    /// (and columns of a non-wrapping map to the nearest column).</summary>
    public int ClampedTile(Hex hex)
    {
        var (col, row) = HexGrid.ToOffset(hex);
        row = Math.Clamp(row, 0, Height - 1);
        if (!Grid.Spec.Wrap) col = Math.Clamp(col, 0, Width - 1);
        Grid.TryTile(col, row, out var id);
        return id.Value;
    }

    /// <summary>Snapshot of the session's map as the local team sees it. <paramref name="layerOf"/> maps a terrain id to
    /// a palette layer; <paramref name="featureGroundOf"/> maps a feature id (or null) to a feature ground index.</summary>
    public static TerrainSnapshot From(GameSession session, Func<string, int> layerOf, Func<string?, int> featureGroundOf)
    {
        var state = session.Sim.State;
        var snapshot = new TerrainSnapshot(state.Map);
        for (int i = 0; i < state.Plots.Length; i++)
        {
            var sight = session.SightOf(state.Plots[i]);
            snapshot.Sight[i] = sight;
            if (session.PerceivedPlot(new TileId(i)) is not { } plot) continue;
            snapshot.Terrain[i] = plot.Terrain;
            snapshot.Feature[i] = plot.Feature;
            snapshot.Relief[i] = ReliefOf(session.Sim.Content.Get<Schema.TerrainDef>(plot.Terrain), plot);
            snapshot.Layer[i] = checked((byte)layerOf(plot.Terrain));
            snapshot.FeatureGround[i] = checked((byte)featureGroundOf(plot.Feature));
            snapshot.Rivers[i] = plot.RiverEdges;
            snapshot.Cliffs[i] = plot.CliffEdges;
            snapshot.Owner[i] = plot.Owner is int owner ? checked((byte)(owner + 1)) : (byte)0;
        }
        return snapshot;
    }

    /// <summary>Lakes and coast terrains are shallow water; ocean terrains are deep water.</summary>
    private static ReliefClass ReliefOf(Schema.TerrainDef terrain, RevealedPlotSnapshot plot)
    {
        if (plot.IsLake) return ReliefClass.ShallowWater;
        if (terrain.IsWater) return IsOcean(plot.Terrain) ? ReliefClass.DeepWater : ReliefClass.ShallowWater;
        return plot.Kind switch
        {
            PlotKind.Peak => ReliefClass.Peak,
            PlotKind.Hills => ReliefClass.Hills,
            _ => ReliefClass.Flat,
        };
    }

    /// <summary>Terrain content has no depth flag; deep-water terrain ids end in "ocean" (plain and slimy oceans).</summary>
    internal static bool IsOcean(string id) => id.EndsWith("ocean", StringComparison.Ordinal);
}
