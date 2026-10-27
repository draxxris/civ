using Godot;

namespace Civ.Client;

/// <summary>
/// Style boxes drawn one over another, each inset from the rect by its own margins (left, top, right, bottom):
/// a scroll bar's raised frame with its recessed well inside. Content margins are this box's own.
/// </summary>
internal sealed partial class LayeredStyleBox : StyleBox
{
    public (StyleBox Box, Vector4 Inset)[] Layers { get; init; } = [];

    public override void _Draw(Rid toCanvasItem, Rect2 rect)
    {
        foreach (var (box, inset) in Layers)
        {
            var area = rect.GrowIndividual(-inset.X, -inset.Y, -inset.Z, -inset.W);
            if (area.Size.X > 0 && area.Size.Y > 0) box.Draw(toCanvasItem, area);
        }
    }
}
