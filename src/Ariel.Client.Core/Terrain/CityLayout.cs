using Ariel.Game;

namespace Ariel.Client.Terrain;

/// <summary>One building shape. The renderer has one model per kind.</summary>
public enum CityLotKind
{
    /// <summary>Round wattle walls under a conical thatch roof.</summary>
    RoundHut,
    /// <summary>A small rectangular hut under a thatched gable roof.</summary>
    Hut,
    /// <summary>A timber-framed house with two or more floors under a steep gable roof.</summary>
    TimberHouse,
    /// <summary>A detached brick house under a slate gable roof, with a chimney.</summary>
    House,
    /// <summary>A long row of joined brick houses under one slate roof, with chimneys.</summary>
    Terrace,
    /// <summary>A stone church with a tower and a spire.</summary>
    Church,
    /// <summary>A flat-roofed modern block.</summary>
    Block,
    /// <summary>A glass tower. Tall ones have a narrower crown and a mast.</summary>
    Tower,
}

/// <summary>
/// One building, relative to the city tile centre in world units (+Z toward the camera). Width runs along the local X
/// axis and Depth along the local Z axis before the turn. Yaw turns about +Y, as Godot's <c>Basis(Vector3.Up, yaw)</c>
/// does, so local +X ends at (cos yaw, -sin yaw). Height is the wall height above the ground, without the roof.
/// Outside is true for a building that spills outside the city walls.
/// </summary>
public readonly record struct CityLot(CityLotKind Kind, double X, double Z, double Yaw, double Width, double Depth,
    double Height, int Floors, int Variant, bool Outside)
{
    /// <summary>The four footprint corners, in order around the rectangle.</summary>
    public (double X, double Z)[] Corners()
    {
        double c = Math.Cos(Yaw), s = Math.Sin(Yaw), hw = Width / 2, hd = Depth / 2, x = X, z = Z;
        (double, double) At(double lx, double lz) => (x + lx * c + lz * s, z - lx * s + lz * c);
        return [At(-hw, -hd), At(hw, -hd), At(hw, hd), At(-hw, hd)];
    }

    /// <summary>Half the footprint diagonal: every corner is this close to the centre.</summary>
    public double Reach => Math.Sqrt(Width * Width + Depth * Depth) / 2;
}

/// <summary>
/// The city wall ring in tile-local coordinates (world units, tile centre at the origin). Obstacles are the convex
/// footprints of the wall parts (curtains, towers, gate, ditch). Radius is the hex distance of the wall line
/// (<see cref="CityLayout.HexDistance"/>). Outer is the hex distance of the outermost wall part. GateAngle is the
/// direction of the gate from the centre (atan2 of z and x), or null when the ring has no gate.
/// </summary>
public sealed record CityWalls(IReadOnlyList<(double X, double Z)[]> Obstacles, double Radius, double Outer, double? GateAngle);

/// <summary>
/// The input for one city layout. Seed makes the layout repeatable. Roads is a bit mask in <c>Hex.Directions</c> order:
/// a set bit keeps a street clear from the centre toward that neighbour. Buildable says whether a tile-local point may
/// hold a building (dry, flat enough, not on a river). The renderer adds more rules for points on neighbour tiles.
/// </summary>
public sealed record CityLayoutInput(int Seed, int Population, CityStyle Style, CityWalls? Walls, int Roads,
    Func<double, double, bool> Buildable);

/// <summary>A planned city: its buildings and the hex distance that the ground underlay must cover.</summary>
public sealed record CityPlan(IReadOnlyList<CityLot> Lots, double GroundRadius);

/// <summary>
/// Places the buildings of a city on its tile. The placement is procedural and repeatable: the same input always gives
/// the same layout. Walls come first: no building overlaps a wall part, and buildings stay inside the ring unless the
/// city is large enough to spill outside. City size sets how many buildings there are, how close they stand and how
/// tall they are:
/// <list type="bullet">
/// <item>Size 1-5: a few scattered buildings near the centre.</item>
/// <item>Size 6-10: more buildings, closer together.</item>
/// <item>Size 11-15: a very dense town. A few buildings spill outside the walls.</item>
/// <item>Size 16 and up: very dense, with multi-storey buildings. More buildings spill outside the walls.</item>
/// </list>
/// Buildings never stand in a street (from the centre toward each road and toward the gate).
/// </summary>
public static class CityLayout
{
    /// <summary>Hex distance that buildings keep to when the city has no walls.</summary>
    public const double OpenEdge = 0.78;
    /// <summary>Half the width of a street.</summary>
    public const double StreetHalfWidth = 0.04;
    /// <summary>Clearance between a building and a wall part.</summary>
    public const double WallGap = 0.025;
    /// <summary>Every building size and storey height is multiplied by this. The city_building shader gets the same
    /// value, so its windows keep one row per storey.</summary>
    public const double BuildingScale = 1.6;
    /// <summary>The storey height per style, in world units. The shader draws one row of windows per storey.</summary>
    public static double StoreyHeight(CityStyle style) => BuildingScale * style switch
    {
        CityStyle.Huts => 0.04,
        CityStyle.Victorian => 0.036,
        _ => 0.03,
    };

    /// <summary>Density tier 1-4 from city size.</summary>
    public static int Tier(int population) => population switch
    {
        <= 5 => 1,
        <= 10 => 2,
        <= 15 => 3,
        _ => 4,
    };

    /// <summary>Hex distance from the tile centre: the largest projection on the three edge normals of a pointy-top hex
    /// (0, 60 and 120 degrees). A point on the edge of a hex with inradius r has a hex distance of r.</summary>
    public static double HexDistance(double x, double z)
    {
        const double S = 0.8660254037844386;
        return Math.Max(Math.Abs(x), Math.Max(Math.Abs(0.5 * x + S * z), Math.Abs(-0.5 * x + S * z)));
    }

    /// <summary>Direction of neighbour <paramref name="direction"/> (Hex.Directions order) as an angle atan2(z, x).</summary>
    public static double DirectionAngle(int direction) => direction switch
    {
        0 => 0,
        1 => -Math.PI / 3,
        2 => -2 * Math.PI / 3,
        3 => Math.PI,
        4 => 2 * Math.PI / 3,
        _ => Math.PI / 3,
    };

    /// <summary>How many buildings stand inside the city (or on its tile when it has no walls).</summary>
    public static int InsideCount(int population)
    {
        int p = Math.Max(1, population);
        return Tier(p) switch
        {
            1 => 1 + p,
            2 => 7 + 2 * (p - 6),
            3 => 17 + 2 * (p - 11),
            _ => 27 + 2 * Math.Min(p - 16, 12),
        };
    }

    /// <summary>How many buildings spill outside the walls. Zero below size 11 and for cities without walls.</summary>
    public static int OutsideCount(int population, bool walls)
    {
        if (!walls) return 0;
        return Tier(population) switch
        {
            3 => 2 + (population - 11),
            4 => 7 + Math.Min(population - 16, 10),
            _ => 0,
        };
    }

    public static CityPlan Plan(CityLayoutInput input)
    {
        var random = new Random(input.Seed);
        int tier = Tier(input.Population);
        var streets = Streets(input);
        var lots = new List<CityLot>();
        double edge = InnerEdge(input);
        double reach = Math.Min(edge, tier == 1 ? 0.3 + 0.07 * input.Population : edge);
        double gap = tier switch { 1 => 0.07, 2 => 0.035, 3 => 0.018, _ => 0.014 };

        if (tier >= 2 && input.Style == CityStyle.Victorian)
            Place(lots, random, input, streets, CityLotKind.Church, edge, Math.Min(reach, 0.4), gap, 1, false, 0);
        Place(lots, random, input, streets, null, edge, reach, gap, InsideCount(input.Population) - lots.Count, false, 0);
        if (input.Walls is { } walls)
        {
            double from = walls.Outer + WallGap;
            double band = tier == 3 ? 0.18 : 0.32;
            Place(lots, random, input, streets, null, from + band, from + band, gap, OutsideCount(input.Population, true),
                true, from);
        }
        double ground = input.Walls is { } ring ? ring.Radius
            : Math.Min(OpenEdge + 0.04, lots.Where(l => !l.Outside).Select(MaxHexDistance).DefaultIfEmpty(0.2).Max() + 0.06);
        return new CityPlan(lots, ground);
    }

    /// <summary>Hex distance that buildings inside the city keep within.</summary>
    private static double InnerEdge(CityLayoutInput input) =>
        input.Walls is { } walls ? walls.Radius - WallGap : OpenEdge;

    private static double MaxHexDistance(CityLot lot) => lot.Corners().Max(c => HexDistance(c.X, c.Z));

    /// <summary>Street directions as unit vectors: one per road, plus the gate.</summary>
    private static List<(double X, double Z)> Streets(CityLayoutInput input)
    {
        var angles = new List<double>();
        for (int d = 0; d < 6; d++)
            if ((input.Roads & (1 << d)) != 0) angles.Add(DirectionAngle(d));
        if (input.Walls?.GateAngle is { } gate && !angles.Any(a => Math.Abs(Math.IEEERemainder(a - gate, Math.Tau)) < 0.2))
            angles.Add(gate);
        return angles.Select(a => (Math.Cos(a), Math.Sin(a))).ToList();
    }

    /// <summary>Try random spots until <paramref name="count"/> buildings fit or the attempts run out. Inside buildings
    /// keep within hex distance <paramref name="reach"/>; outside ones lie between <paramref name="from"/> and
    /// <paramref name="reach"/>.</summary>
    private static void Place(List<CityLot> lots, Random random, CityLayoutInput input, List<(double X, double Z)> streets,
        CityLotKind? kind, double edge, double reach, double gap, int count, bool outside, double from)
    {
        int placed = 0;
        for (int attempt = 0; attempt < 60 * count && placed < count; attempt++)
        {
            double x = (random.NextDouble() * 2 - 1) * reach / 0.8660254037844386;
            double z = (random.NextDouble() * 2 - 1) * reach;
            double d = HexDistance(x, z);
            if (d > reach || (outside && d < from)) continue;
            var lot = Shape(random, input, kind ?? KindAt(random, input, d / Math.Max(edge, 0.01), outside), x, z, d / edge, outside);
            // A long terrace that does not fit becomes a single house on the same spot, so dense towns stay full.
            if (!Fits(lot, lots, input, streets, edge, gap, outside, from)
                && (lot.Kind != CityLotKind.Terrace
                    || !Fits(lot = Shape(random, input, CityLotKind.House, x, z, d / edge, outside), lots, input, streets, edge, gap, outside, from)))
                continue;
            lots.Add(lot);
            placed++;
        }
    }

    private static CityLotKind KindAt(Random random, CityLayoutInput input, double rim, bool outside)
    {
        int tier = Tier(input.Population);
        double roll = random.NextDouble();
        return input.Style switch
        {
            CityStyle.Huts => tier == 4 && !outside ? (roll < 0.7 ? CityLotKind.TimberHouse : CityLotKind.Hut)
                : tier >= 3 && roll < 0.3 ? CityLotKind.TimberHouse
                : roll < 0.5 ? CityLotKind.RoundHut : CityLotKind.Hut,
            CityStyle.Victorian => tier >= 2 && roll < 0.55 && !outside ? CityLotKind.Terrace : CityLotKind.House,
            _ => !outside && tier >= 2 && rim < 0.35 + 0.1 * tier && roll < 0.1 * tier
                ? CityLotKind.Tower : CityLotKind.Block,
        };
    }

    /// <summary>The size, turn and height of one building. <paramref name="rim"/> is 0 at the centre and 1 at the edge.</summary>
    private static CityLot Shape(Random random, CityLayoutInput input, CityLotKind kind, double x, double z, double rim, bool outside)
    {
        int tier = Tier(input.Population);
        double storey = StoreyHeight(input.Style);
        double Range(double a, double b) => a + random.NextDouble() * (b - a);
        int Floors(int a, int b) => random.Next(a, b + 1);
        // Town buildings face the nearest street axis (a multiple of 60 degrees); huts turn any way.
        double axis = Math.Round(Math.Atan2(-z, x) / (Math.PI / 3)) * (Math.PI / 3) + Math.PI / 2;
        double yaw = kind is CityLotKind.RoundHut or CityLotKind.Hut ? random.NextDouble() * Math.Tau : axis + Range(-0.04, 0.04);
        int variant = random.Next(1 << 16);
        int floors;
        double w, d;
        switch (kind)
        {
            case CityLotKind.RoundHut:
                w = d = Range(0.055, 0.075);
                floors = 1;
                break;
            case CityLotKind.Hut:
                (w, d, floors) = (Range(0.07, 0.1), Range(0.05, 0.065), 1);
                break;
            case CityLotKind.TimberHouse:
                (w, d, floors) = (Range(0.075, 0.1), Range(0.06, 0.075), tier == 4 ? Floors(2, 3) : 2);
                break;
            case CityLotKind.House:
                (w, d, floors) = (Range(0.07, 0.09), Range(0.06, 0.075), tier == 4 && !outside ? Floors(2, 3) : 2);
                break;
            case CityLotKind.Terrace:
                (w, d, floors) = (Range(0.14, 0.22), Range(0.065, 0.08), tier == 4 ? Floors(3, 4) : 2);
                break;
            case CityLotKind.Church:
                (w, d, floors) = (0.1, 0.2, 3);
                break;
            case CityLotKind.Tower:
                w = d = Range(0.08, 0.11);
                // Downtown: the tallest towers stand nearest the centre.
                double peak = (tier switch { 2 => 6, 3 => 10, _ => 24 }) * (1.1 - 0.6 * rim);
                floors = Math.Max(4, (int)Math.Round(peak * Range(0.7, 1.0)));
                break;
            default:
                (w, d) = (Range(0.08, 0.13), Range(0.07, 0.11));
                floors = outside ? Floors(1, 2) : tier switch { 1 => Floors(1, 2), 2 => Floors(1, 3), 3 => Floors(2, 4), _ => Floors(4, 9) };
                break;
        }
        return new CityLot(kind, x, z, yaw, w * BuildingScale, d * BuildingScale, floors * storey, floors, variant, outside);
    }

    private static bool Fits(CityLot lot, List<CityLot> lots, CityLayoutInput input, List<(double X, double Z)> streets,
        double edge, double gap, bool outside, double from)
    {
        var corners = lot.Corners();
        foreach (var (cx, cz) in corners)
        {
            double d = HexDistance(cx, cz);
            if (outside ? d < from : d > edge) return false;
        }
        if (InStreet(lot, streets)) return false;
        if (input.Walls is { } walls && walls.Obstacles.Any(o => Overlaps(Grow(lot, WallGap), o))) return false;
        foreach (var other in lots)
        {
            double reach = lot.Reach + other.Reach + gap;
            if ((lot.X - other.X) * (lot.X - other.X) + (lot.Z - other.Z) * (lot.Z - other.Z) >= reach * reach) continue;
            if (Overlaps(Grow(lot, gap), other.Corners())) return false;
        }
        if (!input.Buildable(lot.X, lot.Z)) return false;
        foreach (var (cx, cz) in corners)
            if (!input.Buildable(cx, cz)) return false;
        return true;
    }

    /// <summary>True when the footprint comes within a street's half width of a street's centre line.</summary>
    private static bool InStreet(CityLot lot, List<(double X, double Z)> streets)
    {
        foreach (var (ux, uz) in streets)
        {
            double along = lot.X * ux + lot.Z * uz;
            if (along < -lot.Reach) continue;
            double across = Math.Abs(lot.X * uz - lot.Z * ux);
            if (across < StreetHalfWidth + lot.Reach) return true;
        }
        return false;
    }

    /// <summary>The footprint grown outward by <paramref name="margin"/> on every side.</summary>
    private static (double X, double Z)[] Grow(CityLot lot, double margin) =>
        (lot with { Width = lot.Width + 2 * margin, Depth = lot.Depth + 2 * margin }).Corners();

    /// <summary>Separating-axis test for two convex polygons.</summary>
    public static bool Overlaps(IReadOnlyList<(double X, double Z)> a, IReadOnlyList<(double X, double Z)> b) =>
        !Separated(a, b) && !Separated(b, a);

    private static bool Separated(IReadOnlyList<(double X, double Z)> a, IReadOnlyList<(double X, double Z)> b)
    {
        for (int i = 0; i < a.Count; i++)
        {
            var p = a[i];
            var q = a[(i + 1) % a.Count];
            double nx = q.Z - p.Z, nz = p.X - q.X;
            double minA = double.MaxValue, maxA = double.MinValue, minB = double.MaxValue, maxB = double.MinValue;
            foreach (var v in a) { double t = v.X * nx + v.Z * nz; minA = Math.Min(minA, t); maxA = Math.Max(maxA, t); }
            foreach (var v in b) { double t = v.X * nx + v.Z * nz; minB = Math.Min(minB, t); maxB = Math.Max(maxB, t); }
            if (maxA < minB || maxB < minA) return true;
        }
        return false;
    }
}
