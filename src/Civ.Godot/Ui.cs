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

    // Declared after the colors: MakeTheme reads Accent, and static initializers run in source order.
    private static readonly Theme ButtonTheme = MakeTheme();

    /// <summary>Steel-lapis buttons (see <see cref="ButtonSkin"/>) with hover, pressed and disabled looks.</summary>
    public static Theme MakeTheme()
    {
        var theme = new Theme();
        var pressed = ButtonSkin.Make(ButtonSkin.State.Pressed);
        theme.SetStylebox("normal", "Button", ButtonSkin.Make(ButtonSkin.State.Normal));
        theme.SetStylebox("hover", "Button", ButtonSkin.Make(ButtonSkin.State.Hover));
        theme.SetStylebox("pressed", "Button", pressed);
        theme.SetStylebox("hover_pressed", "Button", pressed);
        theme.SetStylebox("disabled", "Button", ButtonSkin.Make(ButtonSkin.State.Disabled));
        theme.SetColor("font_color", "Button", new Color(0.91f, 0.93f, 0.96f));
        theme.SetColor("font_hover_color", "Button", Colors.White);
        theme.SetColor("font_pressed_color", "Button", Accent);
        theme.SetColor("font_hover_pressed_color", "Button", Accent);
        theme.SetColor("font_disabled_color", "Button", new Color(0.66f, 0.67f, 0.68f));
        // A soft dark outline stands in for the text shadow in the mockup.
        theme.SetColor("font_outline_color", "Button", new Color(0f, 0f, 0f, 0.55f));
        theme.SetConstant("outline_size", "Button", 3);
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
        var b = new Button { Text = text, Disabled = !enabled, TooltipText = tooltip ?? "", FocusMode = Control.FocusModeEnum.None, Theme = ButtonTheme };
        b.AddThemeFontSizeOverride("font_size", size);
        b.Pressed += onPress;
        return b;
    }

    public static VBoxContainer VBox(int separation = 4)
    {
        var v = new VBoxContainer();
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

    public static ProgressBar Bar(double value, double max, Color fill, string text = "")
    {
        var bar = new ProgressBar { MaxValue = Math.Max(1, max), Value = value, CustomMinimumSize = new Vector2(0, 18), ShowPercentage = false, TooltipText = text };
        var f = Box(fill, 0, 3);
        bar.AddThemeStyleboxOverride("fill", f);
        bar.AddThemeStyleboxOverride("background", Box(new Color(0.18f, 0.19f, 0.23f), 0, 3));
        if (text.Length > 0)
        {
            var label = Text(text, 12, Colors.White, wrap: false);
            label.HorizontalAlignment = HorizontalAlignment.Center;
            label.SetAnchorsPreset(Control.LayoutPreset.FullRect);
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
