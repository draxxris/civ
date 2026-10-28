using Ariel.Client;
using Godot;

namespace Ariel.Client;

/// <summary>Builds the rich tooltip of a research choice: header with icon, cost line, description, then what it unlocks.</summary>
internal static class TechTip
{
    private static readonly Dictionary<string, Color> CategoryColors = new(StringComparer.Ordinal)
    {
        ["Buildings"] = new(0.98f, 0.82f, 0.45f),
        ["Units"] = new(0.96f, 0.55f, 0.50f),
        ["Improvements"] = new(0.65f, 0.90f, 0.55f),
        ["Projects"] = new(0.78f, 0.70f, 0.98f),
        ["Civics"] = new(0.55f, 0.82f, 0.98f),
        ["Religions"] = new(0.98f, 0.72f, 0.92f),
        ["Corporations"] = new(0.90f, 0.90f, 0.60f),
        ["Resources"] = new(0.60f, 0.95f, 0.85f),
        ["Technologies"] = new(0.45f, 0.85f, 1.0f),
        ["Worker actions"] = new(0.88f, 0.78f, 0.58f),
        ["Processes"] = new(0.95f, 0.75f, 0.35f),
        ["Promotions"] = new(0.85f, 0.55f, 0.85f),
    };

    public static Color ColorOf(string category) => CategoryColors.GetValueOrDefault(category, BrassSkin.Text);

    public static Control Build(TechChoice tech, IReadOnlyList<TechUnlock> unlocks, IconCache icons)
    {
        var panel = new MarginContainer { CustomMinimumSize = new Vector2(420, 0) };
        foreach (var side in new[] { "left", "right", "top", "bottom" }) panel.AddThemeConstantOverride("margin_" + side, 4);

        var box = Ui.VBox(6);
        var head = Ui.HBox(8);
        var techTexture = icons.TextureOf(tech.Id);
        if (techTexture != null) head.AddChild(Ui.Icon(techTexture, 36));

        var titles = Ui.VBox(2);
        titles.AddChild(Ui.Heading(tech.Name));
        titles.AddChild(Ui.Text(tech.Era + " · " + tech.Cost + " beakers" + (tech.Turns is int t ? $" · {t} turns" : ""), 13, BrassSkin.TextBright, wrap: false));
        head.AddChild(titles);
        box.AddChild(head);

        string help = GameUi.StripMarkup(tech.Help).Trim();
        if (help.Length > 0) box.AddChild(Ui.Text(help, 13, BrassSkin.Text));

        if (unlocks.Count == 0)
        {
            box.AddChild(Ui.Text("Unlocks nothing directly.", 13, Ui.Dim));
        }
        else
        {
            var line = new HSeparator();
            line.AddThemeColorOverride("separator_color", new Color(0.55f, 0.48f, 0.32f, 0.5f));
            box.AddChild(line);
            box.AddChild(Ui.Text("Unlocks:", 14, Ui.Accent));

            var groups = unlocks.GroupBy(u => u.Category);
            foreach (var group in groups)
            {
                var catHeader = Ui.Text(group.Key, 13, Ui.Dim, wrap: false);
                box.AddChild(catHeader);

                var grid = new GridContainer { Columns = 2 };
                grid.AddThemeConstantOverride("h_separation", 16);
                grid.AddThemeConstantOverride("v_separation", 4);
                foreach (var unlock in group)
                {
                    var itemBox = new HBoxContainer();
                    itemBox.AddThemeConstantOverride("separation", 6);
                    var itemTexture = icons.TextureOf(unlock.Id);
                    if (itemTexture != null) itemBox.AddChild(Ui.Icon(itemTexture, 18));
                    var lbl = new Label { Text = unlock.Name };
                    lbl.AddThemeFontSizeOverride("font_size", 13);
                    lbl.AddThemeColorOverride("font_color", ColorOf(unlock.Category));
                    itemBox.AddChild(lbl);
                    grid.AddChild(itemBox);
                }
                box.AddChild(grid);
            }
        }
        panel.AddChild(box);
        return panel;
    }
}
