using Civ.Content.Schema;

namespace Civ.Client.Terrain;

/// <summary>
/// Where a resource (bonus) model stands on its tile. The model stands off the tile centre, so a unit token, a city
/// marker or an improvement model can share the tile: on one of six spots toward the tile corners, the back-left one
/// (where the resource icon sits) first. A spot is skipped when it is near a river, on the wrong side of the shore
/// (a land resource in the water, a sea resource on the beach) or on a steep face. The scatter planner keeps a
/// <see cref="ClearRadius"/> disc around the chosen spot free of trees and rocks.
/// </summary>
public static class BonusSpot
{
    /// <summary>Spots, in order of preference.</summary>
    public const int Count = 6;
    /// <summary>Distance of a spot from the tile centre (corner radius 1, inner radius ~0.87).</summary>
    public const double Distance = 0.42;
    /// <summary>Radius kept clear of scatter props around the spot; resource models fit inside it.</summary>
    public const double ClearRadius = 0.26;
    /// <summary>Radius of the model's footprint checked against rivers and the shore.</summary>
    private const double Footprint = 0.16;
    private const double RiverMargin = 0.06;
    /// <summary>Land models need ground above this; sea models need the seabed below <see cref="SeaFloor"/>.</summary>
    private const double Shore = 0.02, SeaFloor = -0.04;
    private const float Steep = 0.55f;

    // Back-left, back-right, front-left, front-right, back, front: +Z faces the default camera.
    private static readonly double[] Angles = [210, 330, 150, 30, 270, 90];

    /// <summary>The offset of a spot from the tile centre.</summary>
    public static (double X, double Z) Offset(int spot)
    {
        double a = Angles[spot] * Math.PI / 180;
        return (Math.Cos(a) * Distance, Math.Sin(a) * Distance);
    }

    /// <summary>True for a resource that can appear on the map: one that some terrain or feature may carry. Manufactured
    /// resources (and future goods that only buildings make) have neither and never get a model.</summary>
    public static bool OnMap(BonusDef bonus) => bonus.Terrains.Values.Any(v => v) || bonus.Features.Values.Any(v => v);

    /// <summary>The best spot on the tile centred at (x, z): the first one without a fault, otherwise the one with the
    /// lightest faults (river, then shore, then slope).</summary>
    public static int Choose(TerrainField field, double x, double z, bool water)
    {
        int best = 0, bestPenalty = int.MaxValue;
        for (int spot = 0; spot < Count; spot++)
        {
            var (ox, oz) = Offset(spot);
            int penalty = Penalty(field, x + ox, z + oz, water);
            if (penalty == 0) return spot;
            if (penalty < bestPenalty) (best, bestPenalty) = (spot, penalty);
        }
        return best;
    }

    private static int Penalty(TerrainField field, double x, double z, bool water)
    {
        int penalty = 0;
        for (int k = -1; k < 6; k++)
        {
            double px = x, pz = z;
            if (k >= 0)
            {
                double a = k * Math.PI / 3;
                px += Math.Cos(a) * Footprint;
                pz += Math.Sin(a) * Footprint;
            }
            if (field.Rivers.Near(px, pz, RiverMargin)) penalty |= 4;
            double h = field.HeightAt(px, pz);
            if (water ? h > SeaFloor : h <= Shore) penalty |= 2;
        }
        if (!water && field.NormalAt(x, z).Y < Steep) penalty |= 1;
        return penalty;
    }
}
