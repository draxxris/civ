using Godot;

namespace Ariel.Client;

/// <summary>
/// Shader-drawn ground markers: the animated marker under the selected unit (<c>shaders/selection.gdshader</c>) and the
/// go-to destination (<c>shaders/destination.gdshader</c>).
/// </summary>
internal sealed partial class MapRenderer
{
    /// <summary>Half the draped square's side, in ring radii. Must match <c>uv_scale</c> in both shaders.</summary>
    private const float MarkerExtent = 1.45f;
    private const int MarkerSteps = 12;
    private const float YSelection = 0.05f;

    /// <summary>Show the selection marker around <paramref name="at"/>; null clears it.</summary>
    private void ShowSelection(Vector3? at, float ringRadius) =>
        _selection.Mesh = at is { } centre ? DrapedSquare(centre with { Y = YSelection }, ringRadius * MarkerExtent, _selectionMaterial) : null;

    /// <summary>A terrain-draped square around <paramref name="centre"/> with UV -1..1 across it.</summary>
    private ArrayMesh? DrapedSquare(Vector3 centre, float half, Material material)
    {
        var b = new MeshBuilder { Ground = GroundAt };
        int first = b.VertexCount;
        for (int z = 0; z <= MarkerSteps; z++)
            for (int x = 0; x <= MarkerSteps; x++)
            {
                var uv = new Vector2(2f * x / MarkerSteps - 1, 2f * z / MarkerSteps - 1);
                b.Vertex(centre + new Vector3(uv.X * half, 0, uv.Y * half), Colors.White, uv);
            }
        int row = MarkerSteps + 1;
        for (int z = 0; z < MarkerSteps; z++)
            for (int x = 0; x < MarkerSteps; x++)
            {
                int i = first + z * row + x;
                b.Triangle(i, i + 1, i + row + 1);
                b.Triangle(i, i + row + 1, i + row);
            }
        return b.Build(material);
    }
}
