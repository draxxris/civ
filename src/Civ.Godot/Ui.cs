using Godot;

namespace Civ.Client;

/// <summary>Small control factories so the screens stay short and look alike.</summary>
internal static class Ui
{
    public static readonly Color Dim = new(0.66f, 0.69f, 0.75f);
    public static readonly Color Bad = new(0.96f, 0.45f, 0.40f);
    public static readonly Color Good = new(0.55f, 0.88f, 0.52f);
    public static readonly Color Accent = new(1f, 0.82f, 0.30f);
    public static readonly Color PanelFill = new(0.08f, 0.09f, 0.12f, 0.94f);

    private static Theme? _theme;

    /// <summary>
    /// The shared painted-brass theme (see <see cref="BrassSkin"/>). The textures are painted on first use, or loaded
    /// from the disk cache, so every caller shares one set.
    /// </summary>
    public static Theme Theme => _theme ??= MakeTheme();

    /// <summary>
    /// Makes the brass theme the look of every control in the game. The window's theme only reaches controls
    /// whose parents are all controls; panels under a CanvasLayer or a 3D node (the game UI, the setup screen)
    /// would fall back to Godot's default theme. So the brass theme is also merged into that default.
    /// </summary>
    public static void InstallTheme(Window window)
    {
        using var trace = Civ.Modding.StartupTrace.Phase("ui theme");
        window.Theme = Theme;
        ThemeDB.GetDefaultTheme().MergeWith(Theme);
        EnableScrollArrows(window.GetTree());
        ListRim.Attach(window.GetTree());
    }

    /// <summary>
    /// Makes the scroll bars' arrow buttons scroll. Godot's own theme draws no arrows, so the scroll bars inside
    /// lists, scroll containers, trees and text areas leave their step at 0 and an arrow click moves nothing.
    /// Every such bar added to <paramref name="tree"/> gets a step of about one list row, in pixels at
    /// <paramref name="scale"/>, or one line in a text area, whose vertical bar counts lines.
    /// </summary>
    public static void EnableScrollArrows(SceneTree tree, int scale = 1)
    {
        foreach (var node in tree.Root.FindChildren("*", nameof(ScrollBar), true, false)) StepArrows(node, scale);
        tree.NodeAdded += node => StepArrows(node, scale);
    }

    private static void StepArrows(Node node, int scale)
    {
        if (node is not ScrollBar bar || bar.Step > 0 || bar.CustomStep >= 0) return;
        bar.CustomStep = bar is VScrollBar && bar.GetParent() is TextEdit ? 1 : 30 * scale;
    }

    /// <summary>A brass theme painted at <paramref name="scale"/> pixels per unit; the game uses <see cref="Theme"/>.</summary>
    public static Theme MakeTheme(int scale = 1)
    {
        var theme = new Theme();
        BrassSkin.Apply(theme, scale);
        return theme;
    }

    public static StyleBoxFlat Box(Color fill, int margin = 8, int radius = 4)
    {
        var s = new StyleBoxFlat { BgColor = fill };
        s.SetCornerRadiusAll(radius);
        s.SetContentMarginAll(margin);
        return s;
    }

    public static PanelContainer Panel(Color? fill = null, int margin = 8)
    {
        var p = new PanelContainer();
        p.AddThemeStyleboxOverride("panel", Box(fill ?? PanelFill, margin));
        return p;
    }

    public static void Clear(Node node)
    {
        foreach (var child in node.GetChildren()) { node.RemoveChild(child); child.QueueFree(); }
    }

    public static Label Text(string text, int size = 14, Color? color = null, bool wrap = true)
    {
        var l = new Label { Text = text, SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        if (wrap) l.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        l.AddThemeFontSizeOverride("font_size", size);
        if (color is { } c) l.AddThemeColorOverride("font_color", c);
        return l;
    }

    public static Label Heading(string text) => Text(text, 17, Accent);

    public static Button Button(string text, Action onPress, string? tooltip = null, bool enabled = true, int size = 14)
    {
        var b = new Button { Text = text, Disabled = !enabled, TooltipText = tooltip ?? "", FocusMode = Control.FocusModeEnum.None, Theme = Ui.Theme };
        b.AddThemeFontSizeOverride("font_size", size);
        b.Pressed += onPress;
        return b;
    }

    public static VBoxContainer VBox(int separation = 4)
    {
        // Wrapped labels have almost no minimum width. Nested text columns must
        // share the available row width rather than collapse to that minimum.
        var v = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        v.AddThemeConstantOverride("separation", separation);
        return v;
    }

    public static HBoxContainer HBox(int separation = 6)
    {
        var h = new HBoxContainer();
        h.AddThemeConstantOverride("separation", separation);
        return h;
    }

    public static ScrollContainer Scroll(Control content, int minHeight = 0)
    {
        var s = new ScrollContainer { HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled, SizeFlagsVertical = Control.SizeFlags.ExpandFill };
        if (minHeight > 0) s.CustomMinimumSize = new Vector2(0, minHeight);
        content.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        s.AddChild(content);
        return s;
    }

    public static Color ToColor(Rgb c, float a = 1f) => new(c.R, c.G, c.B, a);

    public static TextureRect Icon(Texture2D? texture, float size = 28)
    {
        return new TextureRect
        {
            Texture = texture, CustomMinimumSize = new Vector2(size, size), ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered, MouseFilter = Control.MouseFilterEnum.Ignore,
        };
    }

    /// <summary>
    /// A progress bar with the text at its right end, as in the brass skin's reference. <paramref name="fill"/>
    /// tints the fill to give it a meaning (food, damage); null keeps the skin's slate blue.
    /// </summary>
    public static ProgressBar Bar(double value, double max, Color? fill, string text = "")
    {
        var bar = new ProgressBar { MaxValue = Math.Max(1, max), Value = value, CustomMinimumSize = new Vector2(0, 20), ShowPercentage = false, TooltipText = text };
        if (fill is { } tint) bar.AddThemeStyleboxOverride("fill", BrassSkin.BarFill(tint));
        if (text.Length > 0)
        {
            var label = Text(text, 12, BrassSkin.Text, wrap: false);
            label.AddThemeColorOverride("font_outline_color", new Color(0f, 0f, 0f, 0.6f));
            label.AddThemeConstantOverride("outline_size", 3);
            label.HorizontalAlignment = HorizontalAlignment.Right;
            label.VerticalAlignment = VerticalAlignment.Center;
            label.SetAnchorsPreset(Control.LayoutPreset.FullRect);
            label.OffsetRight = -11;
            bar.AddChild(label);
        }
        return bar;
    }
}

/// <summary>Loads mod PNGs once and shares the textures between the UI and the map atlas.</summary>
internal sealed class IconCache(GameCatalog catalog)
{
    private readonly Dictionary<string, Image?> _images = new();
    private readonly Dictionary<string, ImageTexture?> _textures = new();

    public GameCatalog Catalog => catalog;

    public Image? ImageOf(string? id)
    {
        var path = catalog.IconPath(id);
        if (path == null) return null;
        if (_images.TryGetValue(path, out var cached)) return cached;
        var image = Image.LoadFromFile(path);
        if (image != null && image.GetFormat() != Image.Format.Rgba8) image.Convert(Image.Format.Rgba8);
        return _images[path] = image;
    }

    public Texture2D? TextureOf(string? id)
    {
        var path = catalog.IconPath(id);
        if (path == null) return null;
        if (_textures.TryGetValue(path, out var cached)) return cached;
        var image = ImageOf(id);
        return _textures[path] = image == null ? null : ImageTexture.CreateFromImage(image);
    }
}
