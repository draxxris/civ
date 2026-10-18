using Civ.Game;
using Godot;

namespace Civ.Client;

internal sealed partial class MapRenderer
{
    /// <summary>Draw the route a right click would take, using red for an attack.</summary>
    public void ShowPath(Unit? unit, MovePreview? preview)
    {
        if (unit == null || preview == null) { _path.Mesh = null; return; }
        var b = new PathMesh(GroundAt);
        var colour = preview.Attack != null ? new Color(1f, 0.3f, 0.25f, 0.95f) : new Color(0.4f, 1f, 0.5f, 0.95f);
        var previous = PositionOf(unit.Position, YPath);
        foreach (var step in preview.Path)
        {
            var next = PositionOf(step, YPath);
            if (Period > 0) next.X += Mathf.Round((previous.X - next.X) / Period) * Period;
            b.Line(previous, next);
            b.Disc(next);
            previous = next;
        }
        b.Ring(previous);
        _path.Mesh = b.Build(colour, _pathMaterial);
    }

    /// <summary>Terrain-draped glow shapes. UV length is zero at the bright core and one at the transparent edge.</summary>
    private sealed class PathMesh(Func<float, float, float> ground)
    {
        private const float HalfWidth = 0.16f, DrapeStep = 0.125f;
        private readonly MeshBuilder _mesh = new() { Ground = ground };

        public void Line(Vector3 from, Vector3 to)
        {
            var direction = to - from;
            if (direction.LengthSquared() < 1e-8f) return;
            var side = new Vector3(-direction.Z, 0, direction.X).Normalized() * HalfWidth;
            int count = Mathf.CeilToInt(direction.Length() / DrapeStep);
            for (int i = 0; i < count; i++)
            {
                var start = from.Lerp(to, (float)i / count);
                var end = from.Lerp(to, (float)(i + 1) / count);
                Band(start - side, start + side, end - side, end + side);
            }
        }

        public void Disc(Vector3 centre)
        {
            const int segments = 24;
            int middle = _mesh.Vertex(centre, Colors.White, Vector2.Zero);
            for (int i = 0; i < segments; i++)
            {
                var a = Vector2.FromAngle(Mathf.Tau * i / segments);
                var b = Vector2.FromAngle(Mathf.Tau * (i + 1) / segments);
                _mesh.Triangle(middle,
                    _mesh.Vertex(centre + new Vector3(a.X, 0, a.Y) * HalfWidth, Colors.White, a),
                    _mesh.Vertex(centre + new Vector3(b.X, 0, b.Y) * HalfWidth, Colors.White, b));
            }
        }

        public void Ring(Vector3 centre)
        {
            const int segments = 48;
            const float radius = 0.4f, halfWidth = 0.09f;
            for (int i = 0; i < segments; i++)
            {
                float a = Mathf.Tau * i / segments, b = Mathf.Tau * (i + 1) / segments;
                var from = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a));
                var to = new Vector3(Mathf.Cos(b), 0, Mathf.Sin(b));
                Band(centre + from * (radius - halfWidth), centre + from * (radius + halfWidth),
                    centre + to * (radius - halfWidth), centre + to * (radius + halfWidth));
            }
        }

        private void Band(Vector3 innerFrom, Vector3 outerFrom, Vector3 innerTo, Vector3 outerTo)
        {
            // A centre column follows the terrain under the core as well as under the halo.
            int first = _mesh.Vertex(innerFrom, Colors.White, new Vector2(-1, 0));
            _mesh.Vertex((innerFrom + outerFrom) / 2, Colors.White, Vector2.Zero);
            _mesh.Vertex(outerFrom, Colors.White, new Vector2(1, 0));
            _mesh.Vertex(innerTo, Colors.White, new Vector2(-1, 0));
            _mesh.Vertex((innerTo + outerTo) / 2, Colors.White, Vector2.Zero);
            _mesh.Vertex(outerTo, Colors.White, new Vector2(1, 0));
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
