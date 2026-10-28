using Godot;

namespace Ariel.Client;

/// <summary>The new-game screen's map picture. It shows a rendered silhouette or a one-line note, so nothing here
/// rasterises or blocks: the setup screen renders on a worker thread and hands the pixels over.</summary>
internal sealed partial class MapPreviewPanel : PanelContainer
{
    private readonly TextureRect _picture;
    private readonly Label _note;

    /// <summary>A bordered frame that holds a picture at most <paramref name="box"/> pixels.</summary>
    public MapPreviewPanel(Vector2 box)
    {
        AddThemeStyleboxOverride("panel", Ui.Theme.GetStylebox("panel", "TooltipPanel"));
        CustomMinimumSize = box + new Vector2(20, 20);
        _picture = new TextureRect
        {
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
            SizeFlagsVertical = SizeFlags.ExpandFill,
        };
        _note = Ui.Text("Generating the world…", 13, Ui.Dim);
        _note.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        _note.HorizontalAlignment = HorizontalAlignment.Center;
        _note.VerticalAlignment = VerticalAlignment.Center;
        _note.SizeFlagsVertical = SizeFlags.ExpandFill;
        var stack = Ui.VBox(4);
        stack.AddChild(_picture);
        stack.AddChild(_note);
        AddChild(stack);
    }

    /// <summary>Show pixels from the newest silhouette.</summary>
    public void ShowImage(MapImage image) =>
        Show(ImageTexture.CreateFromImage(Image.CreateFromData(image.Width, image.Height, false, Image.Format.Rgba8, image.Rgba)));

    /// <summary>Replace the picture with one line of explanation.</summary>
    public void ShowNote(string note)
    {
        _picture.Visible = false;
        _note.Text = note;
        _note.Visible = true;
    }

    private void Show(Texture2D texture)
    {
        _picture.Texture = texture;
        _picture.Visible = true;
        _note.Visible = false;
    }
}
