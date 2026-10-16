using Civ.Client.Terrain;
using Civ.Game;
using Godot;

namespace Civ.Client;

internal sealed partial class MapRenderer
{
    /// <summary>Half width of a road ribbon out to the faint edge of its verge, in world units. The shader draws the
    /// narrower, frayed track inside it.</summary>
    private const float RoadHalfWidth = 0.14f;
    /// <summary>Largest sideways bow of a road link, and of its S-shaped second harmonic, in world units.</summary>
    private const float RoadBow = 0.17f, RoadWiggle = 0.08f;
    /// <summary>Ribbon segments per half link (tile centre to shared edge).</summary>
    private const int RoadSteps = 8;

    /// <summary>Each road link between two tile centres is one gently swerving curve: a bow plus a weaker S-bend, both
    /// zero at the centres. The bend is hashed from the unordered tile pair, so each tile draws its own half and the halves
    /// meet exactly at the shared edge with the same tangent. A round blob at the centre joins the links. Fog is the
    /// shader's job, per pixel, so a half link in fog meets one in sight without a step.</summary>
    private void DrawRoute(RoadMesh b, TileId tile, Vector3 centre, Plot plot)
    {
        if (plot.Route == null) return;
        var grid = _session.Sim.State.Grid;
        var hex = grid.HexOf(tile);
        for (int d = 0; d < 6; d++)
        {
            if (grid.Neighbor(tile, d) is not TileId n || n == tile || _session.Sim.State.Plots[n.Value].Route == null) continue;
            // Unwrapped neighbour centre, so a link across the wrap seam stays short.
            var other = PositionOf(hex.Neighbor(d));
            bool first = tile.Value < n.Value;
            var link = new RoadLink(first ? centre : other, first ? other : centre,
                Signed(Hash(Math.Min(tile.Value, n.Value), 2 * Math.Max(tile.Value, n.Value) + 1)) * RoadBow,
                Signed(Hash(Math.Min(tile.Value, n.Value), 2 * Math.Max(tile.Value, n.Value) + 2)) * RoadWiggle);
            b.Ribbon(link, first ? 0f : 0.5f, RoadHalfWidth, RoadSteps);
        }
        b.Blob(centre, RoadHalfWidth, 12);
    }

    /// <summary>A road curve from <c>From</c> (t = 0) to <c>To</c> (t = 1) with a sideways offset
    /// <c>Bow·sin(πt) + Wiggle·sin(2πt)</c>.</summary>
    private readonly record struct RoadLink(Vector3 From, Vector3 To, float Bow, float Wiggle)
    {
        private Vector3 Normal => new Vector3(-(To.Z - From.Z), 0, To.X - From.X).Normalized();

        public Vector3 At(float t) =>
            From.Lerp(To, t) + Normal * (Bow * Mathf.Sin(Mathf.Pi * t) + Wiggle * Mathf.Sin(Mathf.Tau * t));

        public Vector3 Tangent(float t) =>
            To - From + Normal * (Bow * Mathf.Pi * Mathf.Cos(Mathf.Pi * t) + Wiggle * Mathf.Tau * Mathf.Cos(Mathf.Tau * t));
    }

    /// <summary>Road geometry draped on the terrain field, with the ground normal so it is lit like the terrain. UV.x runs
    /// across a ribbon (−1 to 1) or out from a blob centre (0 to 1); see road.gdshader.</summary>
    private sealed class RoadMesh(TerrainField field)
    {
        private readonly List<Vector3> _vertices = new();
        private readonly List<Vector3> _normals = new();
        private readonly List<Vector2> _uvs = new();
        private readonly List<int> _indices = new();

        /// <summary>Half a link, from t0 to t0 + ½, three vertices across so it follows the ground under its width.</summary>
        public void Ribbon(RoadLink link, float t0, float halfWidth, int steps)
        {
            for (int i = 0; i <= steps; i++)
            {
                float t = t0 + 0.5f * i / steps;
                var tangent = link.Tangent(t);
                var side = new Vector3(-tangent.Z, 0, tangent.X).Normalized() * halfWidth;
                var point = link.At(t);
                int row = Vertex(point - side, new Vector2(-1, 0));
                Vertex(point, Vector2.Zero);
                Vertex(point + side, new Vector2(1, 0));
                if (i == 0) continue;
                for (int k = 0; k < 2; k++)
                {
                    int a = row - 3 + k, b = row + k;
                    Triangle(a, a + 1, b + 1);
                    Triangle(a, b + 1, b);
                }
            }
        }

        /// <summary>A disc whose UV.x is the distance from its centre, so it ends like a ribbon cross-section.</summary>
        public void Blob(Vector3 centre, float radius, int segments)
        {
            int middle = Vertex(centre, new Vector2(0, 1));
            for (int k = 0; k < segments; k++)
            {
                float a = Mathf.Tau * k / segments;
                Vertex(centre + new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a)) * radius, new Vector2(1, 1));
            }
            for (int k = 0; k < segments; k++) Triangle(middle, middle + 1 + (k + 1) % segments, middle + 1 + k);
        }

        private int Vertex(Vector3 p, Vector2 uv)
        {
            double surface = field.SurfaceHeightAt(p.X, p.Z);
            // Over water the surface is flat; elsewhere the field's own normal, as the terrain chunks use.
            var n = surface == field.HeightAt(p.X, p.Z) ? field.NormalAt(p.X, p.Z) : System.Numerics.Vector3.UnitY;
            _vertices.Add(new Vector3(p.X, (float)surface + YLines, p.Z));
            _normals.Add(new Vector3(n.X, n.Y, n.Z));
            _uvs.Add(uv);
            return _vertices.Count - 1;
        }

        /// <summary>Wound to face up (Godot's front faces are clockwise), so the shader never sees a flipped normal.</summary>
        private void Triangle(int a, int b, int c)
        {
            var up = (_vertices[b] - _vertices[a]).Cross(_vertices[c] - _vertices[a]).Y;
            if (up > 0) (b, c) = (c, b);
            _indices.Add(a); _indices.Add(b); _indices.Add(c);
        }

        public ArrayMesh? Build(Material material)
        {
            if (_indices.Count == 0) return null;
            var arrays = new Godot.Collections.Array();
            arrays.Resize((int)Mesh.ArrayType.Max);
            arrays[(int)Mesh.ArrayType.Vertex] = _vertices.ToArray();
            arrays[(int)Mesh.ArrayType.Normal] = _normals.ToArray();
            arrays[(int)Mesh.ArrayType.TexUV] = _uvs.ToArray();
            arrays[(int)Mesh.ArrayType.Index] = _indices.ToArray();
            var mesh = new ArrayMesh();
            mesh.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles, arrays);
            mesh.SurfaceSetMaterial(0, material);
            return mesh;
        }
    }
}
