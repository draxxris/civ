using System.Numerics;

namespace Ariel.Client.Terrain;

/// <summary>Ray picking against the height field, so the mouse selects the slope it visibly points at rather than the
/// tile behind a mountain where the ray would cross y = 0.</summary>
public static class TerrainRay
{
    /// <summary>March step along the ray, in world units.</summary>
    public const double Step = 0.05;
    /// <summary>Bisection rounds after the march brackets the surface.</summary>
    public const int Bisections = 8;

    /// <summary>First point where the ray meets the terrain, or null when it points up or never reaches it. The caller maps
    /// the hit to a tile with <c>GameSession.Pick(hit.X, hit.Z)</c>. Set includeWaterSurface to pick the sea surface instead
    /// of the seabed.</summary>
    public static Vector3? Pick(Vector3 origin, Vector3 direction, TerrainField field, bool includeWaterSurface = false)
    {
        double len = direction.Length();
        if (!(len > 0)) return null;
        double ox = origin.X, oy = origin.Y, oz = origin.Z;
        double dx = direction.X / len, dy = direction.Y / len, dz = direction.Z / len;
        if (dy >= -1e-9) return null;
        // Between y = MaxHeight and y = MinHeight the ray must cross the surface.
        double start = oy > field.MaxHeight ? (field.MaxHeight - oy) / dy : 0;
        double end = (field.MinHeight - 1e-6 - oy) / dy;
        if (end < start) end = start;
        if (Below(start)) return At(start);
        double previous = start;
        for (double t = Math.Min(start + Step, end); ; t = Math.Min(t + Step, end))
        {
            if (Below(t))
            {
                double lo = previous, hi = t;
                for (int i = 0; i < Bisections; i++)
                {
                    double mid = (lo + hi) / 2;
                    if (Below(mid)) hi = mid; else lo = mid;
                }
                return At(hi);
            }
            if (t >= end) return null;
            previous = t;
        }

        bool Below(double t)
        {
            double x = ox + dx * t, z = oz + dz * t;
            double height = includeWaterSurface ? field.SurfaceHeightAt(x, z) : field.HeightAt(x, z);
            return oy + dy * t <= height;
        }
        Vector3 At(double t) => new((float)(ox + dx * t), (float)(oy + dy * t), (float)(oz + dz * t));
    }
}
