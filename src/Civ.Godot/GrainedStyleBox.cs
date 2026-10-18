using Godot;

namespace Civ.Client;

/// <summary>
/// Draws the first frame whose minimum height fits the rect (so short buttons can use a thinner
/// bevel), then tiles a texture over the inside at 1:1, so the overlay never stretches.
/// </summary>
internal sealed partial class GrainedStyleBox : StyleBox
{
    /// <summary>A frame used for rects at least <paramref name="MinHeight"/> tall; grain starts <paramref name="GrainInset"/> inside.</summary>
    public readonly record struct Frame(float MinHeight, StyleBox Box, float GrainInset);

    /// <summary>Frames ordered from the tallest minimum height down; the last one should accept any height.</summary>
    public Frame[] Frames { get; set; } = [];
    public Texture2D? Grain { get; set; }

    public override void _Draw(Rid toCanvasItem, Rect2 rect)
    {
        if (Frames.Length == 0) return;
        var frame = Array.Find(Frames, f => rect.Size.Y >= f.MinHeight);
        if (frame.Box == null) frame = Frames[^1];
        frame.Box.Draw(toCanvasItem, rect);
        if (Grain != null) DrawTiled(toCanvasItem, rect.Grow(-frame.GrainInset), Grain);
    }

    private static void DrawTiled(Rid canvasItem, Rect2 area, Texture2D texture)
    {
        var tile = texture.GetSize();
        for (var y = area.Position.Y; y < area.End.Y; y += tile.Y)
        {
            for (var x = area.Position.X; x < area.End.X; x += tile.X)
            {
                var size = new Vector2(Mathf.Min(tile.X, area.End.X - x), Mathf.Min(tile.Y, area.End.Y - y));
                RenderingServer.CanvasItemAddTextureRectRegion(canvasItem, new Rect2(new Vector2(x, y), size), texture.GetRid(),
                    new Rect2(Vector2.Zero, size));
            }
        }
    }
}
