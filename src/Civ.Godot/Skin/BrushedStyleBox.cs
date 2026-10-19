using Godot;

namespace Civ.Client;

/// <summary>
/// Draws a 9-slice plate frame, then the scraped-paint overlay at 1:1, so it never stretches however large the
/// control is: the field over the whole fill, then a wear band inside each closed side of the rim.
/// Each control starts the tiles at its own pseudo-random phase, so no two buttons show the same scrapes.
/// </summary>
internal sealed partial class BrushedStyleBox : StyleBox
{
    /// <summary>
    /// Scroll offsets of controls that draw scrolled boxes on their own canvas item (a list's rows), by canvas
    /// item. <see cref="VaryByPosition"/> adds the offset back, so a row keeps its scrapes while it scrolls.
    /// </summary>
    private static readonly Dictionary<ulong, Func<Vector2>> Scrolls = new();

    public StyleBox? Frame { get; set; }
    /// <summary>
    /// The outline and rim alone (a <see cref="Plate.Hollow"/> plate), for <see cref="ListRim"/> to draw over a
    /// list's rows; null when the control does not need it.
    /// </summary>
    public StyleBox? Rim { get; set; }
    /// <summary>
    /// Seeds the tile phase from the rect's position as well as the canvas item, for boxes that one control draws
    /// many times (a list's rows, a tab bar's tabs), so each one gets its own scrapes.
    /// </summary>
    public bool VaryByPosition { get; set; }
    public ScrapeSet? Scrapes { get; set; }
    /// <summary>Distance from each side (left, top, right, bottom) to where the overlay starts.</summary>
    public Vector4 Inset { get; set; }
    /// <summary>Sides with no rim, so no wear band.</summary>
    public Edges Open { get; set; }
    /// <summary>
    /// Space left empty on each side (left, top, right, bottom) of the rect it is given. An inactive tab uses it to
    /// stand lower than the active one, because the tab bar gives every tab the same rect.
    /// </summary>
    public Vector4 Shrink { get; set; }

    public override void _Draw(Rid toCanvasItem, Rect2 rect)
    {
        var seed = VaryByPosition ? SeedAt(toCanvasItem.Id, rect.Position) : toCanvasItem.Id;
        rect = rect.GrowIndividual(-Shrink.X, -Shrink.Y, -Shrink.Z, -Shrink.W);
        Frame?.Draw(toCanvasItem, rect);
        if (Scrapes is not { } s) return;
        foreach (var (texture, area, offset) in Pieces(s, rect, Inset, Open, new Phase(seed)))
            DrawTiled(toCanvasItem, area, texture, offset);
    }

    /// <summary>Records how far the control drawing on <paramref name="canvasItem"/> is scrolled; null forgets it.</summary>
    public static void SetScroll(Rid canvasItem, Func<Vector2>? offset)
    {
        if (offset == null) Scrolls.Remove(canvasItem.Id);
        else Scrolls[canvasItem.Id] = offset;
    }

    /// <summary>A seed from the canvas item and the box's position in the control's content, before scrolling.</summary>
    private static ulong SeedAt(ulong canvasItem, Vector2 position)
    {
        if (Scrolls.TryGetValue(canvasItem, out var scroll)) position += scroll();
        unchecked
        {
            return canvasItem ^ (ulong)Mathf.RoundToInt(position.X) * 0xC2B2AE3D27D4EB4FUL ^ (ulong)Mathf.RoundToInt(position.Y) * 0x165667B19E3779F9UL;
        }
    }

    /// <summary>
    /// Paints the overlay into <paramref name="image"/> as <see cref="_Draw"/> would over the whole image, for
    /// icons that should match the style boxes beside them (a scroll bar's arrow cells). <paramref name="seed"/>
    /// picks the tile phase.
    /// </summary>
    public static void Overlay(Image image, ScrapeSet scrapes, Vector4 inset, Edges open, ulong seed)
    {
        var rect = new Rect2(0, 0, image.GetWidth(), image.GetHeight());
        foreach (var (texture, area, offset) in Pieces(scrapes, rect, inset, open, new Phase(seed)))
        {
            var source = texture.GetImage();
            var tile = source.GetSize();
            var start = new Vector2I(Mathf.PosMod((int)offset.X, tile.X), Mathf.PosMod((int)offset.Y, tile.Y));
            for (var y = (int)area.Position.Y - start.Y; y < (int)area.End.Y; y += tile.Y)
            {
                for (var x = (int)area.Position.X - start.X; x < (int)area.End.X; x += tile.X)
                {
                    var piece = new Rect2I(x, y, tile.X, tile.Y).Intersection(new Rect2I((Vector2I)area.Position, (Vector2I)area.Size));
                    if (piece.Size.X <= 0 || piece.Size.Y <= 0) continue;
                    image.BlendRect(source, new Rect2I(piece.Position - new Vector2I(x, y), piece.Size), piece.Position);
                }
            }
        }
    }

    /// <summary>
    /// The overlay's pieces inside <paramref name="rect"/>: the field over the whole fill, then a wear band
    /// inside each closed side of the rim, each with the texture offset at its top-left corner.
    /// </summary>
    private static IEnumerable<(Texture2D Texture, Rect2 Area, Vector2 Offset)> Pieces(ScrapeSet s, Rect2 rect, Vector4 inset, Edges open, Phase phase)
    {
        var area = new Rect2(rect.Position + new Vector2(inset.X, inset.Y), rect.Size - new Vector2(inset.X + inset.Z, inset.Y + inset.W));
        if (area.Size.X <= 0 || area.Size.Y <= 0) yield break;
        yield return (s.Field, area, phase.Next());
        // Wear reaches into the groove under the rim, one pixel outside the field.
        var unit = s.Scale;
        area = area.GrowIndividual(open.HasFlag(Edges.Left) ? 0 : unit, open.HasFlag(Edges.Top) ? 0 : unit,
            open.HasFlag(Edges.Right) ? 0 : unit, open.HasFlag(Edges.Bottom) ? 0 : unit);
        var depth = Mathf.Min(s.EdgeDepth, area.Size.Y / 2f);
        if (!open.HasFlag(Edges.Top)) yield return (s.Top, new Rect2(area.Position, new Vector2(area.Size.X, depth)), phase.Next() with { Y = 0 });
        if (!open.HasFlag(Edges.Bottom))
            yield return (s.Bottom, new Rect2(area.Position.X, area.End.Y - depth, area.Size.X, depth), phase.Next() with { Y = s.EdgeDepth - depth });
        depth = Mathf.Min(s.EndDepth, area.Size.X / 2f);
        if (!open.HasFlag(Edges.Left)) yield return (s.Left, new Rect2(area.Position, new Vector2(depth, area.Size.Y)), phase.Next() with { X = 0 });
        if (!open.HasFlag(Edges.Right))
            yield return (s.Right, new Rect2(area.End.X - depth, area.Position.Y, depth, area.Size.Y), phase.Next() with { X = s.EndDepth - depth });
    }

    /// <summary>
    /// Tiles <paramref name="texture"/> over <paramref name="area"/>, each texel <paramref name="magnify"/> pixels
    /// across, with the point <paramref name="offset"/> pixels into the tiling at the area's top-left corner.
    /// </summary>
    public static void DrawTiled(Rid canvasItem, Rect2 area, Texture2D texture, Vector2 offset, int magnify = 1)
    {
        var tile = texture.GetSize() * magnify;
        offset = new Vector2(Mathf.PosMod(offset.X, tile.X), Mathf.PosMod(offset.Y, tile.Y));
        for (var y = area.Position.Y - offset.Y; y < area.End.Y; y += tile.Y)
        {
            for (var x = area.Position.X - offset.X; x < area.End.X; x += tile.X)
            {
                var piece = new Rect2(x, y, tile.X, tile.Y).Intersection(area);
                if (piece.Size.X <= 0 || piece.Size.Y <= 0) continue;
                var source = new Rect2((piece.Position - new Vector2(x, y)) / magnify, piece.Size / magnify);
                RenderingServer.CanvasItemAddTextureRectRegion(canvasItem, piece, texture.GetRid(), source);
            }
        }
    }

    /// <summary>Whole-pixel tile offsets drawn from a seed (a control's canvas item), stable for the life of the control.</summary>
    private sealed class Phase(ulong seed)
    {
        private ulong _state = seed * 0x9E3779B97F4A7C15UL + 1;

        public Vector2 Next()
        {
            _state ^= _state << 13;
            _state ^= _state >> 7;
            _state ^= _state << 17;
            return new Vector2((float)(_state % 4096), (float)((_state >> 20) % 4096));
        }
    }
}
