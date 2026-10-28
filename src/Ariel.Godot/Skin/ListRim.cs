using Godot;

namespace Ariel.Client;

/// <summary>
/// Draws an ItemList's brass rim again, over its rows. An ItemList clips its rows at its own outer edge, so a row
/// scrolled half out of view ran over the rim; with the rim on top, rows end at the rim's inner edge. It also tells
/// <see cref="BrushedStyleBox"/> how far the list is scrolled, so each row keeps its own scrapes while it moves.
/// Lists whose panel is not a brushed box with a <see cref="BrushedStyleBox.Rim"/> are left as they are.
/// </summary>
internal sealed partial class ListRim : Control
{
    private const string Marker = "brass_list_rim";

    private ItemList? _list;

    /// <summary>Gives every ItemList in <paramref name="tree"/>, now and later, a rim drawn over its rows.</summary>
    public static void Attach(SceneTree tree)
    {
        foreach (var node in tree.Root.FindChildren("*", nameof(ItemList), true, false)) Add(node);
        tree.NodeAdded += Add;
    }

    private static void Add(Node node)
    {
        if (node is not ItemList list || list.HasMeta(Marker)) return;
        list.SetMeta(Marker, true);
        // The list may be in the middle of entering the tree, when it cannot take new children.
        list.CallDeferred(Node.MethodName.AddChild, new ListRim(), false, (int)InternalMode.Back);
    }

    public override void _EnterTree()
    {
        _list = GetParent() as ItemList;
        // Anchors alone would keep the new control's zero size; the offsets must go to zero too.
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        MouseFilter = MouseFilterEnum.Ignore;
        if (_list == null) return;
        var bars = _list.GetChildren(true).OfType<ScrollBar>().ToArray();
        BrushedStyleBox.SetScroll(_list.GetCanvasItem(), () =>
        {
            var offset = Vector2.Zero;
            foreach (var bar in bars) offset += bar is VScrollBar ? new Vector2(0, (int)bar.Value) : new Vector2((int)bar.Value, 0);
            return offset;
        });
        _list.QueueRedraw();
    }

    public override void _ExitTree()
    {
        if (_list != null) BrushedStyleBox.SetScroll(_list.GetCanvasItem(), null);
    }

    public override void _Draw()
    {
        if (_list?.GetThemeStylebox("panel") is BrushedStyleBox { Rim: { } rim }) DrawStyleBox(rim, new Rect2(Vector2.Zero, Size));
    }
}
