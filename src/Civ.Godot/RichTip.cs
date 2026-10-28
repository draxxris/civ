using Godot;

namespace Civ.Client;

/// <summary>
/// A panel whose hover tooltip is a built control tree (icons, coloured text) instead of the plain string Godot
/// shows by default. The tree is built only when the tooltip opens, so idle widgets carry no extra nodes. The
/// plain <see cref="Control.TooltipText"/> is still set, because Godot shows no tooltip at all when it is empty.
/// </summary>
internal sealed partial class RichTipPanel : PanelContainer
{
    public Func<Control>? Build;

    public override GodotObject _MakeCustomTooltip(string forText) => Build?.Invoke()!;
}

/// <summary>A button with the same rich tooltip support as <see cref="RichTipPanel"/>.</summary>
internal sealed partial class RichTipButton : Button
{
    public Func<Control>? Build;

    public override GodotObject _MakeCustomTooltip(string forText) => Build?.Invoke()!;
}
