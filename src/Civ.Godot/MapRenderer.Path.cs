using Civ.Game;
using Godot;

namespace Civ.Client;

internal sealed partial class MapRenderer
{
    /// <summary>Where the ribbon leaves the unit (under the selection ring) and stops short of the destination ring.</summary>
    private const float PathStartGap = 0.36f, PathEndGap = 0.38f, DestinationRadius = 0.42f;

    /// <summary>Draw the route a right click would take, using red for an attack.</summary>
    public void ShowPath(Unit? unit, MovePreview? preview)
    {
        if (unit == null || preview == null) { _path.Mesh = null; _destination.Mesh = null; return; }
        var colour = preview.Attack != null ? new Color(1f, 0.3f, 0.25f, 0.95f) : new Color(0.4f, 1f, 0.5f, 0.95f);
        var points = new List<Vector3> { PositionOf(unit.Position, YPath) };
        foreach (var step in preview.Path)
        {
            var next = PositionOf(step, YPath);
            if (Period > 0) next.X += Mathf.Round((points[^1].X - next.X) / Period) * Period;
            points.Add(next);
        }
        var b = new PathMesh(GroundAt);
        float along = 0;
        for (int i = 1; i < points.Count; i++)
        {
            var (from, to) = (points[i - 1], points[i]);
            float length = from.DistanceTo(to);
            if (length < 1e-4f) continue;
            float start = i == 1 ? Mathf.Min(PathStartGap, length / 2) : 0;
            float end = i == points.Count - 1 ? Mathf.Min(PathEndGap, length / 2) : 0;
            b.Line(from.Lerp(to, start / length), to.Lerp(from, end / length), along + start);
            along += length;
        }
        for (int i = 1; i < points.Count - 1; i++) b.Pip(points[i]);
        _path.Mesh = b.Build(colour, _pathMaterial);
        _destinationMaterial.SetShaderParameter("marker_colour", colour with { A = 1 });
        _destinationMaterial.SetShaderParameter("attack", preview.Attack != null);
        _destination.Mesh = DrapedSquare(points[^1] with { Y = YPath }, DestinationRadius * MarkerExtent, _destinationMaterial);
    }

    /// <summary>
    /// Terrain-draped ribbon and waypoint pips for <c>shaders/path.gdshader</c>. Ribbon vertices are white with UV
    /// (across -1..1, distance along the route); pip vertices are black with a radial UV of length zero to one.
    /// </summary>
    private sealed class PathMesh(Func<float, float, float> ground)
    {
        private const float HalfWidth = 0.2f, PipRadius = 0.15f, DrapeStep = 0.125f;
        private readonly MeshBuilder _mesh = new() { Ground = ground };

        public void Line(Vector3 from, Vector3 to, float along)
        {
            var direction = to - from;
            if (direction.LengthSquared() < 1e-8f) return;
            var side = new Vector3(-direction.Z, 0, direction.X).Normalized() * HalfWidth;
            float length = direction.Length();
            int count = Mathf.CeilToInt(length / DrapeStep);
            for (int i = 0; i < count; i++)
            {
                float t0 = (float)i / count, t1 = (float)(i + 1) / count;
                var start = from.Lerp(to, t0);
                var end = from.Lerp(to, t1);
                Band(start - side, start + side, end - side, end + side, along + t0 * length, along + t1 * length);
            }
        }

        public void Pip(Vector3 centre)
        {
            const int segments = 24;
            int middle = _mesh.Vertex(centre, Colors.Black, Vector2.Zero);
            for (int i = 0; i < segments; i++)
            {
                var a = Vector2.FromAngle(Mathf.Tau * i / segments);
                var b = Vector2.FromAngle(Mathf.Tau * (i + 1) / segments);
                _mesh.Triangle(middle,
                    _mesh.Vertex(centre + new Vector3(a.X, 0, a.Y) * PipRadius, Colors.Black, a),
                    _mesh.Vertex(centre + new Vector3(b.X, 0, b.Y) * PipRadius, Colors.Black, b));
            }
        }

        private void Band(Vector3 innerFrom, Vector3 outerFrom, Vector3 innerTo, Vector3 outerTo, float alongFrom, float alongTo)
        {
            // A centre column follows the terrain under the core as well as under the halo.
            int first = _mesh.Vertex(innerFrom, Colors.White, new Vector2(-1, alongFrom));
            _mesh.Vertex((innerFrom + outerFrom) / 2, Colors.White, new Vector2(0, alongFrom));
            _mesh.Vertex(outerFrom, Colors.White, new Vector2(1, alongFrom));
            _mesh.Vertex(innerTo, Colors.White, new Vector2(-1, alongTo));
            _mesh.Vertex((innerTo + outerTo) / 2, Colors.White, new Vector2(0, alongTo));
            _mesh.Vertex(outerTo, Colors.White, new Vector2(1, alongTo));
            for (int i = 0; i < 2; i++)
            {
                _mesh.Triangle(first + i, first + i + 1, first + i + 4);
                _mesh.Triangle(first + i, first + i + 4, first + i + 3);
            }
        }

        public ArrayMesh? Build(Color colour, ShaderMaterial material)
        {
            material.SetShaderParameter("path_colour", colour);
            return _mesh.Build(material);
        }
    }
}
