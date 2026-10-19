using Godot;

namespace Civ.Client;

/// <summary>
/// Painted-brass widget skin after docs/references/ui.png: brushed slate-blue or charcoal paint inside a
/// hand-painted brass rim, lit from the top left. Every texture is generated at startup by
/// <see cref="BrassPainter"/>; nothing is cut from the reference image.
/// </summary>
internal static class BrassSkin
{
    public static readonly Color Text = Color.Color8(235, 221, 200);
    public static readonly Color TextBright = Color.Color8(255, 247, 230);
    public static readonly Color TextDim = Color.Color8(150, 144, 136);
    /// <summary>Warm dark backdrop of the reference sheet; the widgets are tuned to sit on it.</summary>
    public static readonly Color Backdrop = Color.Color8(50, 45, 38);

    private static readonly Color Slate = Color.Color8(44, 67, 83);
    private static readonly Color FieldSlate = Color.Color8(34, 45, 54);
    private static readonly Color TabSlate = Color.Color8(39, 63, 82);
    private static readonly Color BarBlue = Color.Color8(54, 88, 113);
    private static readonly Color GrabberBlue = Color.Color8(58, 90, 112);
    /// <summary>Cast-brass chevrons in the scroll bar's arrow cells, with a paler lit bevel.</summary>
    private static readonly Color ChevronBrass = Color.Color8(170, 148, 116);
    private static readonly Color ChevronLight = Color.Color8(228, 212, 178);
    private static readonly Color Charcoal = Color.Color8(47, 45, 43);
    /// <summary>The scroll bar's raised plate: a neutral grey, lighter than the charcoal well inside it.</summary>
    private static readonly Color ScrollGrey = Color.Color8(60, 59, 55);
    private static readonly Color WellCharcoal = Color.Color8(40, 39, 37);
    private static readonly Color GuideBrass = new(0.5f, 0.42f, 0.33f, 0.22f);

    private const int CheckSize = 22, InactiveTabDrop = 3;
    /// <summary>
    /// Scroll bar width; arrow cells are a little longer than wide, so the chevrons have room; the thumb leaves a
    /// gap each side of it.
    /// </summary>
    private const int ScrollWidth = 20, ArrowCellLength = 16, ThumbGap = 5;
    /// <summary>How far the scroll well sits inside the bar's outer edge, across the bar.</summary>
    private const float WellInset = 3f;

    /// <summary>Scraped-paint overlays by (brushwork, wear) strength; few combinations are in use.</summary>
    private static readonly Dictionary<(float, float, int), ScrapeSet> ScrapeSets = new();
    private static readonly Dictionary<Color, StyleBox> BarFills = new();
    /// <summary>Painted frames by plate (records compare by value), so repeated plates are painted once.</summary>
    private static readonly Dictionary<Plate, Texture2D> Frames = new();

    public enum Look { Normal, Hover, Pressed, Disabled }

    /// <summary>Pixels per unit while <see cref="Apply"/> runs; sizes in this file are in units.</summary>
    private static int _scale = 1;

    /// <summary>
    /// Fills a theme with the brass look for every stock control the game uses, painted at
    /// <paramref name="scale"/> pixels per unit: 1 for the game, larger for high-DPI screens or for comparing
    /// against the full-size reference. Call it from the main thread only.
    /// </summary>
    public static void Apply(Theme theme, int scale = 1)
    {
        _scale = scale;
        try { ApplyAll(theme); }
        finally { _scale = 1; }
    }

    private static void ApplyAll(Theme theme)
    {
        if (_scale > 1) theme.DefaultFontSize = Px(14);
        ApplyButtons(theme);
        ApplyChecks(theme);
        ApplyProgress(theme);
        ApplyTabs(theme);
        ApplyLists(theme);
        ApplyPopup(theme);
        ApplyScrollBars(theme);
        ApplyLineEdit(theme);
        ApplySpinBox(theme);
        ApplyBackdrop(theme);
    }

    /// <summary>A progress fill in <paramref name="tint"/>, darkened to sit with the paint; cached per colour.</summary>
    public static StyleBox BarFill(Color tint)
    {
        lock (BarFills)
        {
            if (BarFills.TryGetValue(tint, out var cached)) return cached;
            return BarFills[tint] = MakeBarFill(tint.Darkened(0.55f));
        }
    }

    // ---- Buttons ----------------------------------------------------------------------------------

    private static void ApplyButtons(Theme theme)
    {
        SetLooks(theme, "Button", look => ButtonPlate(Slate, look, 7));
        SetLooks(theme, "MenuButton", look => ButtonPlate(Slate, look, 7));
        SetLooks(theme, "OptionButton", look => ButtonPlate(FieldSlate, look, 3));
        foreach (var type in new[] { "Button", "MenuButton", "OptionButton" })
        {
            SetButtonFonts(theme, type);
            theme.SetStylebox("focus", type, Focus());
        }
        theme.SetIcon("arrow", "OptionButton", Icon(12, 8, null, Triangle(new(1.5f, 1.5f), new(10.5f, 1.5f), new(6f, 6.8f)) with { Colour = ChevronBrass, Highlight = ChevronLight }));
        theme.SetConstant("arrow_margin", "OptionButton", Px(9));
        theme.SetConstant("modulate_arrow", "OptionButton", 0);
    }

    /// <summary>Button plates per look. Shared seeds keep the rim pattern still while the colour changes.</summary>
    private static StyleBox ButtonPlate(Color fill, Look look, int seed)
    {
        var plate = look switch
        {
            Look.Hover => new Plate { Fill = Lift(fill, 1.2f), Glow = 0.6f, Brass = 1.1f, Seed = seed },
            Look.Pressed => new Plate { Fill = Lift(fill, 0.8f), Light = -1f, Well = 0.45f, Seed = seed },
            Look.Disabled => new Plate { Fill = Charcoal, Glow = 0.15f, Brass = 0.85f, Seed = seed },
            _ => new Plate { Fill = fill, Glow = 0.35f, Seed = seed },
        };
        var pressed = look == Look.Pressed;
        return Box(plate, 10, pressed ? 6 : 5, 10, pressed ? 4 : 5);
    }

    private static void SetButtonFonts(Theme theme, string type)
    {
        theme.SetColor("font_color", type, Text);
        theme.SetColor("font_focus_color", type, Text);
        theme.SetColor("font_hover_color", type, TextBright);
        theme.SetColor("font_pressed_color", type, Ui.Accent);
        theme.SetColor("font_hover_pressed_color", type, Ui.Accent);
        theme.SetColor("font_disabled_color", type, TextDim);
        // A soft dark outline stands in for the engraved text shadow in the reference.
        theme.SetColor("font_outline_color", type, new Color(0f, 0f, 0f, 0.55f));
        theme.SetConstant("outline_size", type, Px(3));
    }

    // ---- Check boxes and radio buttons ------------------------------------------------------------

    private static void ApplyChecks(Theme theme)
    {
        foreach (var type in new[] { "CheckBox", "CheckButton" })
        {
            foreach (var name in new[] { "normal", "hover", "pressed", "hover_pressed", "disabled", "focus" })
                theme.SetStylebox(name, type, Empty(2));
            theme.SetColor("font_color", type, Text);
            theme.SetColor("font_focus_color", type, Text);
            theme.SetColor("font_hover_color", type, TextBright);
            theme.SetColor("font_pressed_color", type, Text);
            theme.SetColor("font_hover_pressed_color", type, TextBright);
            theme.SetColor("font_disabled_color", type, TextDim);
            theme.SetConstant("h_separation", type, Px(7));
        }
        foreach (var (name, round, on, enabled) in CheckIcons())
            theme.SetIcon(name, "CheckBox", CheckIcon(round, on, enabled));
    }

    private static IEnumerable<(string Name, bool Round, bool On, bool Enabled)> CheckIcons()
    {
        foreach (var round in new[] { false, true })
        {
            var prefix = round ? "radio_" : "";
            yield return (prefix + "checked", round, true, true);
            yield return (prefix + "unchecked", round, false, true);
            yield return (prefix + "checked_disabled", round, true, false);
            yield return (prefix + "unchecked_disabled", round, false, false);
        }
    }

    /// <summary>
    /// A check box or radio button after docs/references/ui.png: a thick brass rim round a recessed well. A checked
    /// box holds a slate well and a cream tick; a chosen radio holds a slate well and a glossy blue bead. Empty wells
    /// are charcoal, a little lighter in the middle.
    /// </summary>
    private static Texture2D CheckIcon(bool round, bool on, bool enabled)
    {
        const float c = CheckSize / 2f;
        var fill = on ? Color.Color8(37, 57, 71) : Color.Color8(39, 40, 40);
        var plate = new Plate
        {
            Fill = enabled ? fill : Lift(fill, 0.8f), Rim = 2.2f, Radius = round ? CheckSize : 2.5f, Falloff = 3f,
            Well = 0.35f, Glow = 0f, Brass = enabled ? 0.85f : 0.6f, Finish = RimFinish.Dull, Mottle = 0.05f, Seed = round ? 41 : 43,
        };
        // A soft lift in the middle of the well, as if it were dished.
        var vignette = new Glyph((x, y) => new Vector2(x - c, y - c).Length() - (c - 3f), Colors.White,
            Shade: (x, y) => new Color(0.75f, 0.8f, 0.82f, 0.03f * Mathf.Clamp(1f - new Vector2(x - c, y - c).Length() / (c - 3f), 0f, 1f)),
            Outline: false);
        if (!on) return Icon(CheckSize, CheckSize, plate, vignette);
        if (round) return Icon(CheckSize, CheckSize, plate, vignette, Bead(c, c, 5f, enabled));
        var tick = new Glyph((x, y) => Polyline(x, y, new(c - 3.5f, c - 0.1f), new(c - 1.2f, c + 2.3f), new(c + 3.4f, c - 2.7f)) - 1.15f,
            enabled ? Color.Color8(216, 196, 168) : TextDim);
        return Icon(CheckSize, CheckSize, plate, vignette, tick);
    }

    /// <summary>A glossy painted bead: lit from the top left, darker underneath, with a small bright highlight.</summary>
    private static Glyph Bead(float cx, float cy, float radius, bool enabled)
    {
        Color top = Color.Color8(62, 106, 138), bottom = Color.Color8(40, 78, 106), shine = Color.Color8(150, 180, 198);
        if (!enabled) (top, bottom, shine) = (Color.Color8(78, 88, 96), Color.Color8(46, 52, 58), Color.Color8(120, 128, 134));
        return new Glyph((x, y) => new Vector2(x - cx, y - cy).Length() - radius, top, Shade: (x, y) =>
        {
            var p = new Vector2(x - cx, y - cy) / radius;
            var colour = top.Lerp(bottom, Mathf.Clamp(0.5f + 0.5f * (0.4f * p.X + 0.9f * p.Y), 0f, 1f));
            colour *= 1f - 0.25f * Mathf.Pow(Mathf.Min(p.Length(), 1f), 4f);
            var spot = Mathf.Exp(-(p - new Vector2(-0.38f, -0.42f)).LengthSquared() / 0.06f);
            return colour.Lerp(shine, 0.6f * spot) with { A = 1f };
        });
    }

    // ---- Progress bar -----------------------------------------------------------------------------

    private static void ApplyProgress(Theme theme)
    {
        var track = new Plate { Fill = WellCharcoal, Well = 0.5f, Strokes = 0.2f, Wear = 0.25f, Seed = 5 };
        var side = BrassPainter.MarginOf(track) - BrassPainter.ShadowMarginOf(track);  // units; Box scales
        theme.SetStylebox("background", "ProgressBar", Box(track, side, 2, side, 2));
        theme.SetStylebox("fill", "ProgressBar", MakeBarFill(BarBlue));
        theme.SetColor("font_color", "ProgressBar", Text);
        theme.SetColor("font_outline_color", "ProgressBar", new Color(0f, 0f, 0f, 0.6f));
        theme.SetConstant("outline_size", "ProgressBar", Px(3));
    }

    /// <summary>
    /// A rimless bar of paint inside the track, as in the reference: its own dark outline and rounded ends, a gap
    /// from the track's rim, a pale highlight along the top and only light wear.
    /// </summary>
    private static StyleBox MakeBarFill(Color fill)
    {
        var plate = new Plate
        {
            Fill = fill, Rim = 0f, Pad = 3f, Radius = 2.5f, Outline = true, Shadow = 0f, Glow = 0.55f, Falloff = 3f,
            Strokes = 0.8f, Wear = 0.3f, Seed = 9,
        };
        var margin = BrassPainter.MarginOf(plate);
        return Box(plate, margin, 0, margin, 0);
    }

    // ---- Tabs -------------------------------------------------------------------------------------

    private static void ApplyTabs(Theme theme)
    {
        const int tabSeed = 11;
        // The active tab stands taller than the others, as in the reference: inactive tabs leave space at the top
        // (and a pixel at each side) of the rect the tab bar gives them, and push their text down to match.
        StyleBox Tab(Color fill, float glow, float brass, bool active)
        {
            var box = (BrushedStyleBox)Box(new Plate { Fill = fill, Glow = glow, Brass = brass, Radius = 1f, Chamfer = 4.5f, Open = Edges.Bottom, Seed = tabSeed },
                active ? 18 : 16, active ? 7 : 9, active ? 18 : 16, 5);
            if (!active) box.Shrink = new Vector4(1, InactiveTabDrop, 1, 0) * _scale;
            return box;
        }
        var selected = Tab(TabSlate, 0.4f, 1.1f, true);
        var unselected = Tab(Charcoal, 0.15f, 0.85f, false);
        var hovered = Tab(Lift(Charcoal, 1.25f), 0.3f, 0.95f, false);
        var disabled = Tab(Lift(Charcoal, 0.85f), 0.1f, 0.65f, false);
        // The panel's thick top rim is the rail the tabs stand on.
        var panel = Box(new Plate { Fill = WellCharcoal, TopRim = 3.5f, Well = 0.3f, Strokes = 0.35f, Wear = 0.5f, Seed = 31 }, 12, 12, 12, 12);
        foreach (var type in new[] { "TabBar", "TabContainer" })
        {
            theme.SetStylebox("tab_selected", type, selected);
            theme.SetStylebox("tab_unselected", type, unselected);
            theme.SetStylebox("tab_hovered", type, hovered);
            theme.SetStylebox("tab_disabled", type, disabled);
            theme.SetStylebox("tab_focus", type, Empty(0));
            theme.SetColor("font_selected_color", type, TextBright);
            theme.SetColor("font_unselected_color", type, Text.Darkened(0.12f));
            theme.SetColor("font_hovered_color", type, TextBright);
            theme.SetColor("font_disabled_color", type, TextDim);
            theme.SetColor("font_outline_color", type, new Color(0f, 0f, 0f, 0.55f));
            theme.SetConstant("outline_size", type, Px(3));
        }
        theme.SetStylebox("panel", "TabContainer", panel);
        theme.SetStylebox("tabbar_background", "TabContainer", Empty(0));
    }

    // ---- Lists ------------------------------------------------------------------------------------

    /// <summary>The selected row: slate paint with a thin, dull rim, shared by lists and dropdown popups.</summary>
    private static StyleBox Row(float lift) =>
        Box(new Plate { Fill = Lift(Slate, lift), Rim = 1f, Brass = 0.75f, Radius = 3f, Outline = false, Shadow = 0f, Glow = 0.3f, Falloff = 2f, Seed = 13 }, 6, 3, 6, 3);

    private static StyleBox RowHover()
    {
        var box = new StyleBoxFlat { BgColor = new Color(1f, 0.93f, 0.8f, 0.06f) };
        box.SetCornerRadiusAll(Px(3));
        box.SetContentMarginAll(Px(3));
        return box;
    }

    /// <summary>A recessed charcoal well: list backgrounds, scroll tracks, text fields.</summary>
    private static Plate WellPlate(int seed) => new() { Fill = WellCharcoal, Well = 0.45f, Strokes = 0.35f, Wear = 0.5f, Seed = seed };

    private static void ApplyLists(Theme theme)
    {
        const string type = "ItemList";
        theme.SetStylebox("panel", type, Box(WellPlate(17), 3, 3, 3, 3));
        theme.SetStylebox("focus", type, Empty(0));
        var selected = Row(1f);
        foreach (var name in new[] { "selected", "selected_focus", "hovered_selected", "hovered_selected_focus" })
            theme.SetStylebox(name, type, selected);
        theme.SetStylebox("hovered", type, RowHover());
        theme.SetStylebox("cursor", type, Empty(0));
        theme.SetStylebox("cursor_unfocused", type, Empty(0));
        theme.SetColor("font_color", type, Text);
        theme.SetColor("font_hovered_color", type, TextBright);
        theme.SetColor("font_selected_color", type, TextBright);
        theme.SetColor("font_hovered_selected_color", type, TextBright);
        theme.SetColor("guide_color", type, GuideBrass);
        theme.SetColor("font_outline_color", type, new Color(0f, 0f, 0f, 0.5f));
        theme.SetConstant("outline_size", type, Px(2));
        theme.SetConstant("v_separation", type, Px(6));
        theme.SetConstant("h_separation", type, Px(6));
    }

    private static void ApplyPopup(Theme theme)
    {
        const string type = "PopupMenu";
        theme.SetStylebox("panel", type, Box(new Plate { Fill = Color.Color8(42, 41, 40), Glow = 0.1f, Strokes = 0.5f, Wear = 0.7f, Seed = 19 }, 3, 3, 3, 3));
        theme.SetStylebox("hover", type, Row(1f));
        var line = new StyleBoxLine { Color = GuideBrass, Thickness = Px(1), GrowBegin = -Px(4), GrowEnd = -Px(4) };
        theme.SetStylebox("separator", type, line);
        theme.SetStylebox("labeled_separator_left", type, line);
        theme.SetStylebox("labeled_separator_right", type, line);
        theme.SetColor("font_color", type, Text);
        theme.SetColor("font_hover_color", type, TextBright);
        theme.SetColor("font_disabled_color", type, TextDim);
        theme.SetColor("font_accelerator_color", type, TextDim);
        theme.SetColor("font_separator_color", type, TextDim);
        theme.SetConstant("v_separation", type, Px(5));
        theme.SetConstant("h_separation", type, Px(4));
        theme.SetConstant("item_start_padding", type, Px(3));
        theme.SetConstant("item_end_padding", type, Px(8));
        var dot = Icon(12, 12, null, new Glyph((x, y) => (Mathf.Abs(x - 6f) + Mathf.Abs(y - 6f)) / Mathf.Sqrt2 - 2.2f, Text));
        var blank = Icon(12, 12, null);
        theme.SetIcon("radio_checked", type, dot);
        theme.SetIcon("radio_checked_disabled", type, dot);
        theme.SetIcon("radio_unchecked", type, blank);
        theme.SetIcon("radio_unchecked_disabled", type, blank);
        // Check items get a small tick in the same 12-unit column as the radio dot: the menu reserves the widest
        // check icon's width on every row, so a full-size check box would push all the text to the right.
        static float Segment(Vector2 p, Vector2 a, Vector2 b)
        {
            var ab = b - a;
            var t = Mathf.Clamp((p - a).Dot(ab) / ab.LengthSquared(), 0f, 1f);
            return (p - (a + ab * t)).Length();
        }
        Texture2D Tick(Color colour) => Icon(12, 12, null, new Glyph((x, y) =>
        {
            var p = new Vector2(x, y);
            return Mathf.Min(Segment(p, new(2.5f, 6.5f), new(5f, 9f)), Segment(p, new(5f, 9f), new(9.8f, 3f))) - 1.1f;
        }, colour));
        theme.SetIcon("checked", type, Tick(Text));
        theme.SetIcon("unchecked", type, blank);
        theme.SetIcon("checked_disabled", type, Tick(TextDim));
        theme.SetIcon("unchecked_disabled", type, blank);
        theme.SetIcon("submenu", type, Icon(8, 12, null, Triangle(new(1.5f, 1.5f), new(6.8f, 6f), new(1.5f, 10.5f))));
    }

    // ---- Scroll bars ------------------------------------------------------------------------------

    /// <summary>
    /// After the reference: one raised grey plate in a brass rim runs the length of the bar. Its ends are the
    /// arrow cells, each holding a cast-brass triangle; between them a recessed dark well, with its own thin brass
    /// lip, holds the glossy blue thumb with a gap either side.
    /// </summary>
    private static void ApplyScrollBars(Theme theme)
    {
        StyleBox Grabber(bool vertical, float lift, float light, float glow)
        {
            var box = (BrushedStyleBox)Box(new Plate
            {
                Fill = Lift(GrabberBlue, lift), Rim = 0f, Radius = 0.8f, Chamfer = 1.4f, Bevel = 2.2f, Shadow = 0.7f, Falloff = 2f,
                Light = light, Glow = glow, Well = light < 0 ? 0.3f : 0f, Strokes = 0.8f, Wear = 0f, Seed = 23,
            }, ScrollWidth / 2, 6, ScrollWidth / 2, 6);
            var gap = ThumbGap * _scale;
            box.Shrink = vertical ? new Vector4(gap, 0, gap, 0) : new Vector4(0, gap, 0, gap);
            return box;
        }

        foreach (var (type, vertical) in new[] { ("VScrollBar", true), ("HScrollBar", false) })
        {
            var grabber = Grabber(vertical, 1f, 1f, 0.3f);
            var highlight = Grabber(vertical, 1.18f, 1f, 0.45f);
            var pressed = Grabber(vertical, 0.85f, -1f, 0f);
            theme.SetStylebox("scroll", type, ScrollTrack(vertical));
            theme.SetStylebox("scroll_focus", type, ScrollTrack(vertical));
            theme.SetStylebox("grabber", type, grabber);
            theme.SetStylebox("grabber_highlight", type, highlight);
            theme.SetStylebox("grabber_pressed", type, pressed);
            var (decrement, increment) = vertical ? (Arrow.Up, Arrow.Down) : (Arrow.Left, Arrow.Right);
            SetArrowIcons(theme, type, "decrement", decrement);
            SetArrowIcons(theme, type, "increment", increment);
        }
    }

    /// <summary>
    /// The middle of the bar: the grey frame, open at both ends where the arrow cells continue it, with the well
    /// inside. The content margins along the bar keep the thumb clear of the well's ends.
    /// </summary>
    private static StyleBox ScrollTrack(bool vertical)
    {
        var frame = Box(ScrollFrame(vertical ? Edges.Top | Edges.Bottom : Edges.Left | Edges.Right), 0, 0, 0, 0);
        var well = Box(new Plate
        {
            Fill = WellCharcoal, Rim = 0.8f, Brass = 0.75f, Radius = 2.5f, Outline = false, Shadow = 0f, Falloff = 2f, Well = 0.5f,
            Strokes = 0.3f, Wear = 0f, Seed = 25,
        }, 0, 0, 0, 0);
        float across = WellInset * _scale, along = 0.5f * _scale;
        var box = new LayeredStyleBox
        {
            Layers = [(frame, Vector4.Zero), (well, vertical ? new Vector4(across, along, across, along) : new Vector4(along, across, along, across))],
        };
        const int side = ScrollWidth / 2, end = 2;
        box.SetContentMargin(Side.Left, Px(vertical ? side : end));
        box.SetContentMargin(Side.Right, Px(vertical ? side : end));
        box.SetContentMargin(Side.Top, Px(vertical ? end : side));
        box.SetContentMargin(Side.Bottom, Px(vertical ? end : side));
        return box;
    }

    /// <summary>The bar's raised grey plate; <paramref name="open"/> sides join the next piece of it.</summary>
    private static Plate ScrollFrame(Edges open, float lift = 1f) => new()
    {
        Fill = Lift(ScrollGrey, lift), Radius = 3f, Shadow = 0f, Falloff = 2f, Glow = 0.15f, Strokes = 1f, Wear = 0.6f, Open = open, Seed = 21,
    };

    private enum Arrow { Up, Down, Left, Right }

    private static void SetArrowIcons(Theme theme, string type, string name, Arrow arrow)
    {
        theme.SetIcon(name, type, ArrowCell(arrow, Look.Normal));
        theme.SetIcon(name + "_highlight", type, ArrowCell(arrow, Look.Hover));
        theme.SetIcon(name + "_pressed", type, ArrowCell(arrow, Look.Pressed));
    }

    /// <summary>
    /// An end cell of the bar: the grey plate closed on the outer end and open towards the track, with a solid
    /// cast-brass triangle a little past its middle. Drawn for an up arrow, then turned for the other directions.
    /// </summary>
    private static Texture2D ArrowCell(Arrow arrow, Look look)
    {
        // Up-arrow geometry, in a cell s across and l along the bar, whose open side is at the bottom (y = l).
        const float s = ScrollWidth, l = ArrowCellLength;
        // Equilateral, whatever the cell's length: about 7 units wide, centred in the space inside the outer rim.
        const float half = 3.4f, tall = half * 1.732f, centre = (2.3f + l) / 2f;
        var triangle = new[]
        {
            new Vector2(s / 2f - half, centre + tall / 2f), new Vector2(s / 2f, centre - tall / 2f), new Vector2(s / 2f + half, centre + tall / 2f),
        };

        // Glyph distances take the cell's own coordinates and look them up in the up-arrow frame.
        Func<float, float, Vector2> back = arrow switch
        {
            Arrow.Down => (x, y) => new Vector2(x, l - y),
            Arrow.Left => (x, y) => new Vector2(y, x),
            Arrow.Right => (x, y) => new Vector2(y, l - x),
            _ => (x, y) => new Vector2(x, y),
        };
        var open = arrow switch { Arrow.Up => Edges.Bottom, Arrow.Down => Edges.Top, Arrow.Left => Edges.Right, _ => Edges.Left };
        var plate = ScrollFrame(open, look switch { Look.Pressed => 0.8f, Look.Hover => 1.12f, _ => 1f });
        var press = look == Look.Pressed ? 0.6f : 0f;
        var (body, lit) = look == Look.Hover ? (ChevronBrass.Lightened(0.15f), ChevronLight.Lightened(0.2f)) : (ChevronBrass, ChevronLight);
        var glyph = new Glyph((x, y) =>
        {
            var p = back(x, y);
            return ConvexDistance(new Vector2(p.X, p.Y - press), triangle);
        }, body, lit);
        var vertical = arrow is Arrow.Up or Arrow.Down;
        return PaintedIcon(vertical ? ScrollWidth : ArrowCellLength, vertical ? ArrowCellLength : ScrollWidth, plate, (ulong)arrow + 1, glyph);
    }

    // ---- Text fields ------------------------------------------------------------------------------

    /// <summary>
    /// Text fields and text areas sit in a softer well than lists: the paint is smoother, with faint relief, few
    /// scratches and little wear at the rim, so text reads cleanly over it.
    /// </summary>
    private static Plate SoftWellPlate(int seed) => new() { Fill = WellCharcoal, Well = 0.35f, Strokes = 0.2f, Wear = 0.15f, Mottle = 0.04f, Seed = seed };

    /// <summary>Type variation for a full-screen backdrop panel: the backdrop colour in the soft paint.</summary>
    public const string BackdropPanel = "BackdropPanel";

    /// <summary>
    /// A plain painted surface behind the widgets, in the soft paint of the text wells: no rim and no wear, so it
    /// reads as a backdrop rather than a plate.
    /// </summary>
    private static void ApplyBackdrop(Theme theme)
    {
        theme.SetTypeVariation(BackdropPanel, "Panel");
        theme.SetStylebox("panel", BackdropPanel, Box(new Plate
        {
            Fill = Backdrop, Rim = 0f, Radius = 0f, Outline = false, Shadow = 0f, Strokes = 0.2f, Wear = 0f, Mottle = 0.04f, Seed = 37,
        }, 0, 0, 0, 0));
    }

    private static void ApplyLineEdit(Theme theme)
    {
        foreach (var (type, side, top) in new[] { ("LineEdit", 9, 5), ("TextEdit", 9, 7) })
        {
            theme.SetStylebox("normal", type, Box(SoftWellPlate(29), side, top, side, top));
            theme.SetStylebox("read_only", type, Box(SoftWellPlate(29) with { Brass = 0.7f }, side, top, side, top));
            theme.SetStylebox("focus", type, Focus());
            theme.SetColor("font_color", type, Text);
            theme.SetColor("font_readonly_color", type, TextDim);
            theme.SetColor("font_uneditable_color", type, TextDim);
            theme.SetColor("font_placeholder_color", type, TextDim with { A = 0.7f });
            theme.SetColor("caret_color", type, TextBright);
            theme.SetColor("selection_color", type, Lift(Slate, 1.8f) with { A = 0.8f });
        }
        theme.SetColor("current_line_color", "TextEdit", new Color(1f, 0.93f, 0.8f, 0.04f));
    }

    // ---- Spin boxes -------------------------------------------------------------------------------

    /// <summary>The spin box's step arrows: small cast-brass chevrons like the dropdown's, beside a brass field.</summary>
    private static void ApplySpinBox(Theme theme)
    {
        const string type = "SpinBox";
        Texture2D Chevron(bool up, Color body, Color lit) => Icon(12, 8, null, (up
            ? Triangle(new(1.5f, 6.5f), new(10.5f, 6.5f), new(6f, 1.2f))
            : Triangle(new(1.5f, 1.5f), new(10.5f, 1.5f), new(6f, 6.8f))) with { Colour = body, Highlight = lit });
        foreach (var up in new[] { true, false })
        {
            var name = up ? "up" : "down";
            theme.SetIcon(name, type, Chevron(up, ChevronBrass, ChevronLight));
            theme.SetIcon(name + "_hover", type, Chevron(up, ChevronBrass.Lightened(0.2f), ChevronLight.Lightened(0.25f)));
            theme.SetIcon(name + "_pressed", type, Chevron(up, ChevronBrass.Darkened(0.15f), ChevronBrass));
            theme.SetIcon(name + "_disabled", type, Chevron(up, TextDim.Darkened(0.3f), TextDim));
            foreach (var state in new[] { "", "_hovered", "_pressed", "_disabled" })
                theme.SetStylebox(name + "_background" + state, type, Empty(0));
        }
        theme.SetStylebox("up_down_buttons_separator", type, Empty(0));
        theme.SetStylebox("field_and_buttons_separator", type, Empty(0));
        theme.SetConstant("field_and_buttons_separation", type, Px(4));
        theme.SetConstant("buttons_vertical_separation", type, Px(2));
    }

    // ---- Building blocks --------------------------------------------------------------------------

    private static void SetLooks(Theme theme, string type, Func<Look, StyleBox> make)
    {
        var pressed = make(Look.Pressed);
        theme.SetStylebox("normal", type, make(Look.Normal));
        theme.SetStylebox("hover", type, make(Look.Hover));
        theme.SetStylebox("pressed", type, pressed);
        theme.SetStylebox("hover_pressed", type, pressed);
        theme.SetStylebox("disabled", type, make(Look.Disabled));
    }

    /// <summary>
    /// A plate as a style box: the painted 9-slice frame tiled to any size, the paint overlay over the fill, and the
    /// given content margins (at least the frame margins on closed sides, so the corners never squash). The drop
    /// shadow is an expand margin, so it does not change the layout. The overlay darkens the paint by
    /// <see cref="ScrapedPaint.MeanShade"/> on average, so the frame paints the fill that much lighter under it.
    /// </summary>
    private static StyleBox Box(Plate plate, int left, int top, int right, int bottom)
    {
        plate = plate with { Scale = _scale };
        var margin = BrassPainter.MarginOf(plate) * _scale;
        var frame = new StyleBoxTexture
        {
            Texture = FrameOf(plate with { FieldLift = 1f / (1f - ScrapedPaint.MeanShade), FieldInset = StrokeInset(plate) }),
            AxisStretchHorizontal = StyleBoxTexture.AxisStretchMode.TileFit,
            AxisStretchVertical = StyleBoxTexture.AxisStretchMode.TileFit,
        };
        frame.SetTextureMarginAll(margin);
        // Open sides carry no shadow: their margin is plain fill, so it stays inside the rect (a tab's foot ends on
        // its rail rather than hanging 6 px below it with no paint texture).
        var shadow = BrassPainter.ShadowMarginOf(plate) * _scale;
        frame.SetExpandMargin(Side.Left, plate.Open.HasFlag(Edges.Left) ? 0 : shadow);
        frame.SetExpandMargin(Side.Top, plate.Open.HasFlag(Edges.Top) ? 0 : shadow);
        frame.SetExpandMargin(Side.Right, plate.Open.HasFlag(Edges.Right) ? 0 : shadow);
        frame.SetExpandMargin(Side.Bottom, plate.Open.HasFlag(Edges.Bottom) ? 0 : shadow);
        var box = new BrushedStyleBox { Frame = frame, Scrapes = ScrapesFor(plate), Inset = StrokeInset(plate) * _scale, Open = plate.Open };
        box.SetContentMargin(Side.Left, Px(left));
        box.SetContentMargin(Side.Top, Px(top));
        box.SetContentMargin(Side.Right, Px(right));
        box.SetContentMargin(Side.Bottom, Px(bottom));
        return box;
    }

    private static ScrapeSet ScrapesFor(Plate plate)
    {
        var key = (plate.Strokes, plate.Wear, plate.Scale);
        lock (ScrapeSets)
        {
            if (ScrapeSets.TryGetValue(key, out var cached)) return cached;
            return ScrapeSets[key] = ScrapedPaint.Paint(ScrapeSets.Count * 7919 + 3, plate.Strokes, plate.Wear, plate.Scale);
        }
    }

    private static Texture2D FrameOf(Plate plate)
    {
        lock (Frames)
        {
            if (Frames.TryGetValue(plate, out var cached)) return cached;
            return Frames[plate] = ImageTexture.CreateFromImage(BrassPainter.PaintTiled(plate));
        }
    }

    /// <summary>Where strokes start on each side: inside the rim and clear of the rounded corners.</summary>
    private static Vector4 StrokeInset(Plate plate)
    {
        var outer = plate.Pad + (plate.Outline ? BrassPainter.OutlineWidth(plate) : 0f);
        float Side(Edges edge, float rim)
        {
            if (plate.Open.HasFlag(edge)) return 0f;
            var corner = Mathf.Max(plate.Radius - (plate.Radius - rim) / Mathf.Sqrt2, (plate.Chamfer + rim * Mathf.Sqrt2) / 2f);
            return Mathf.Ceil(outer + Mathf.Max(rim + 1f, corner + 0.5f));
        }
        return new Vector4(Side(Edges.Left, plate.Rim), Side(Edges.Top, plate.TopRimWidth), Side(Edges.Right, plate.Rim), Side(Edges.Bottom, plate.Rim));
    }

    private static StyleBox Focus()
    {
        var box = new StyleBoxFlat { DrawCenter = false, BorderColor = Text with { A = 0.35f } };
        box.SetBorderWidthAll(Px(1));
        box.SetCornerRadiusAll(Px(5));
        return box;
    }

    private static StyleBoxEmpty Empty(int margin)
    {
        var box = new StyleBoxEmpty();
        box.SetContentMarginAll(Px(margin));
        return box;
    }

    private static Texture2D Icon(int width, int height, Plate? plate, params Glyph[] glyphs) =>
        ImageTexture.CreateFromImage(BrassPainter.PaintIcon(width, height, _scale, plate, glyphs));

    /// <summary>
    /// An icon whose plate carries the same scraped-paint overlay as the style boxes, so it matches the box it
    /// continues (a scroll bar's arrow cells): the plate, then the overlay at the <paramref name="phase"/> given,
    /// then the glyphs on top.
    /// </summary>
    private static Texture2D PaintedIcon(int width, int height, Plate plate, ulong phase, params Glyph[] glyphs)
    {
        plate = plate with { Scale = _scale };
        var inset = StrokeInset(plate);
        var image = BrassPainter.PaintIcon(width, height, _scale, plate with { FieldLift = 1f / (1f - ScrapedPaint.MeanShade), FieldInset = inset });
        BrushedStyleBox.Overlay(image, ScrapesFor(plate), inset * _scale, plate.Open, phase);
        if (glyphs.Length > 0)
        {
            var top = BrassPainter.PaintIcon(width, height, _scale, null, glyphs);
            image.BlendRect(top, new Rect2I(Vector2I.Zero, top.GetSize()), Vector2I.Zero);
        }
        return ImageTexture.CreateFromImage(image);
    }

    /// <summary>A size in units as pixels at the scale being applied.</summary>
    private static int Px(int units) => units * _scale;

    private static Glyph Triangle(Vector2 a, Vector2 b, Vector2 c) => new((x, y) => ConvexDistance(new Vector2(x, y), a, b, c), Text);

    /// <summary>Signed distance (negative inside) to a convex polygon with clockwise or anticlockwise points.</summary>
    private static float ConvexDistance(Vector2 p, params Vector2[] points)
    {
        var area = 0f;
        for (var i = 0; i < points.Length; i++) area += points[i].Cross(points[(i + 1) % points.Length]);
        var sign = area > 0 ? 1f : -1f;
        var d = float.MinValue;
        for (var i = 0; i < points.Length; i++)
        {
            var (a, b) = (points[i], points[(i + 1) % points.Length]);
            var edge = (b - a).Normalized();
            d = Mathf.Max(d, sign * edge.Cross(a - p));
        }
        return d;
    }

    private static float Polyline(float x, float y, params Vector2[] points)
    {
        var p = new Vector2(x, y);
        var d = float.MaxValue;
        for (var i = 0; i + 1 < points.Length; i++)
        {
            var (a, b) = (points[i], points[i + 1]);
            var t = Mathf.Clamp((p - a).Dot(b - a) / (b - a).LengthSquared(), 0f, 1f);
            d = Mathf.Min(d, (p - (a + (b - a) * t)).Length());
        }
        return d;
    }

    private static Color Lift(Color c, float by) => new(c.R * by, c.G * by, c.B * by, c.A);
}
