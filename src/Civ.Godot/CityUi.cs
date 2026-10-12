using Civ.BaseGame;
using Civ.Game;
using Godot;

namespace Civ.Client;

/// <summary>
/// The full-screen city view: header, resource bar, citizens panel on the left, production panel on the right, with the
/// map (yield badges on tiles) showing through the middle. Everything shown comes from <see cref="CityScreen"/>.
/// Mod resources (faith, renown, capital, ...) join the resource bar and institutions get their own list with found buttons.
/// What the simulation does not have yet (gold, purchasing, forced labor, projects, wonders) is shown disabled or left out.
/// </summary>
public partial class CityUi : Control
{
    public enum ProdTab { Buildings, Units }

    private static readonly Color Parchment = new(0.93f, 0.89f, 0.80f, 0.96f);
    private static readonly Color Ink = new(0.18f, 0.13f, 0.08f);
    private static readonly Color InkDim = new(0.40f, 0.33f, 0.24f);
    private static readonly Color Slate = new(0.12f, 0.14f, 0.17f, 0.95f);
    private static readonly Color Green = new(0.45f, 0.78f, 0.25f);
    private static readonly Color Orange = new(0.85f, 0.55f, 0.20f);
    private static readonly Color Gold = new(0.95f, 0.78f, 0.25f);
    private static readonly Color Purple = new(0.72f, 0.45f, 0.95f);

    private readonly GameSession _session;
    private readonly PanelContainer _header = new();
    private readonly PanelContainer _resources = new();
    private readonly PanelContainer _citizens = new();
    private readonly PanelContainer _production = new();
    private readonly Button _close;

    public ProdTab Tab { get; private set; } = ProdTab.Buildings;

    public CityUi(GameSession session)
    {
        _session = session;
        MouseFilter = MouseFilterEnum.Ignore;
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);

        Place(_header, 8, 8, 540, 104, Parchment);
        Place(_resources, 8, 118, 600, 62, Slate);
        _citizens.AnchorBottom = 1;
        Place(_citizens, 8, 190, 330, 0, Parchment);
        _citizens.OffsetBottom = -8;
        _production.AnchorLeft = _production.AnchorRight = 1;
        _production.AnchorBottom = 1;
        Place(_production, -400, 56, 392, 0, Parchment);
        _production.OffsetBottom = -8;

        _close = GameUi.Btn("X", _session.ClearSelection, minWidth: 40);
        _close.AnchorLeft = _close.AnchorRight = 1;
        _close.OffsetLeft = -48; _close.OffsetRight = -8; _close.OffsetTop = 8; _close.OffsetBottom = 44;
        AddChild(_close);
    }

    private void Place(PanelContainer p, float x, float y, float w, float h, Color fill)
    {
        var box = GameUi.Box(fill, 10);
        box.BorderColor = new Color(0.55f, 0.43f, 0.25f);
        box.SetBorderWidthAll(2);
        p.AddThemeStyleboxOverride("panel", box);
        p.OffsetLeft = x; p.OffsetTop = y; p.OffsetRight = x + w; p.OffsetBottom = y + h;
        AddChild(p);
    }

    public void Build(City city)
    {
        var s = CityScreen.Of(_session.Sim, city);
        BuildHeader(s, city);
        BuildResources(s);
        BuildCitizens(s, city);
        BuildProduction(s, city);
    }

    // --- header and resources ---------------------------------------------------------------

    private void BuildHeader(CityScreen s, City city)
    {
        GameUi.Clear(_header);
        var row = new HBoxContainer();
        _header.AddChild(row);
        var civColor = Color.FromHtml(_session.Sim.CivilizationOf(city.Owner)?.Color ?? "#4060c0");
        var banner = new ColorRect { Color = civColor, CustomMinimumSize = new Vector2(48, 80) };
        row.AddChild(banner);
        var col = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        row.AddChild(col);
        col.AddChild(Label(s.Name, 28, Ink, false));
        col.AddChild(Label($"Population  {s.Population}", 16, Ink));
        string growth = s.Starving ? $"Starving ({s.FoodSurplus} food)"
            : s.TurnsToGrow is int t ? $"Growing in {t} turn{(t == 1 ? "" : "s")} ({s.FoodSurplus:+#;-#} food)"
            : $"Not growing ({s.FoodSurplus:+#;-#;0} food)";
        col.AddChild(Label(growth, 14, s.Starving ? GameUi.Bad : InkDim));
    }

    private void BuildResources(CityScreen s)
    {
        GameUi.Clear(_resources);
        var row = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
        row.AddThemeConstantOverride("separation", 28);
        _resources.AddChild(row);
        Stat(row, s.Food - 0, "Food", Green, $"{s.FoodSurplus:+#;-#;0} growth");
        Stat(row, s.Production, "Production", Orange);
        Stat(row, s.Commerce, "Commerce", Gold, "to research");
        Stat(row, s.Culture, "Culture", Purple);
        foreach (var r in s.Resources)
            Stat(row, (int)r.PerTurn, r.Name, Color.FromHtml(r.Color), r.Stored is long stored ? $"{stored} stored" : r.CityScoped ? null : "civ-wide");
    }

    private static void Stat(Container row, int value, string name, Color color, string? sub = null)
    {
        var col = new VBoxContainer();
        var top = Label($"{value:+#;-#;0}", 20, color, false);
        top.HorizontalAlignment = HorizontalAlignment.Center;
        col.AddChild(top);
        var bottom = Label(sub == null ? name : $"{name} ({sub})", 11, new Color(0.85f, 0.85f, 0.88f), false);
        bottom.HorizontalAlignment = HorizontalAlignment.Center;
        col.AddChild(bottom);
        row.AddChild(col);
    }

    // --- citizens ---------------------------------------------------------------------------

    private void BuildCitizens(CityScreen s, City city)
    {
        GameUi.Clear(_citizens);
        var scroll = new ScrollContainer { HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
        _citizens.AddChild(scroll);
        var col = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        scroll.AddChild(col);

        var title = new HBoxContainer();
        title.AddChild(Label("Citizens", 22, Ink));
        var total = Label($"{s.Population} total", 14, InkDim);
        total.HorizontalAlignment = HorizontalAlignment.Right;
        title.AddChild(total);
        col.AddChild(title);

        int free = CityRules.WorkableTiles(_session.Sim, city).Count();
        col.AddChild(Row("Workers", s.Workers.ToString(), "Work tiles for yields",
            () => _session.ChangeWorkers(city, -1), s.Workers > 0,
            () => _session.ChangeWorkers(city, +1), free > s.Workers));

        col.AddChild(Row("Specialists", s.SpecialistTotal.ToString(), "Provide specialist yields", null, false, null, false));
        foreach (var spec in s.Specialists)
        {
            var sp = spec;
            string name = sp.Unlimited ? "Citizen (no job)" : sp.Name;
            string detail = sp.Effects.Length == 0 ? "No yields" : sp.Effects;
            if (!sp.Unlimited) detail += sp.Slots == int.MaxValue ? "" : $"  ({sp.Count}/{sp.Slots} slots)";
            col.AddChild(Row(name, sp.Count.ToString(), detail,
                sp.Unlimited ? null : () => _session.ChangeSpecialist(city, sp.Id, -1), sp.Count > 0,
                sp.Unlimited ? null : () => _session.ChangeSpecialist(city, sp.Id, +1), sp.Slots > sp.Count,
                indent: true));
        }
        col.AddChild(Label("Click highlighted tiles on the map to move a citizen onto or off a tile.", 11, InkDim));
        if (_session.Message.Length > 0) col.AddChild(Label(_session.Message, 12, GameUi.Bad));

        if (s.Institutions.Count > 0 || s.Foundable.Count > 0) col.AddChild(Label("Institutions", 22, Ink));
        foreach (var i in s.Institutions)
            col.AddChild(Label($"{i.Name} ({i.Category}){(i.IsHome ? ", founded here" : "")}", 14, Color.FromHtml(i.Color)));
        foreach (var f in s.Foundable)
        {
            var id = f.Id;
            var b = GameUi.Btn($"Found {f.Name}" + (f.Cost.Length > 0 ? $" ({f.Cost})" : ""), () => _session.FoundInstitution(city, id), f.Blocked == null);
            b.TooltipText = f.Blocked ?? "";
            col.AddChild(b);
        }
    }

    private static Control Row(string name, string count, string detail, Action? minus, bool minusOn, Action? plus, bool plusOn, bool indent = false)
    {
        var panel = new PanelContainer();
        panel.AddThemeStyleboxOverride("panel", GameUi.Box(new Color(0.85f, 0.80f, 0.68f, indent ? 0.5f : 0.8f), 6));
        var row = new HBoxContainer();
        panel.AddChild(row);
        var text = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        text.AddChild(Label(name, 15, Ink));
        text.AddChild(Label(detail, 11, InkDim));
        row.AddChild(text);
        var n = Label(count, 18, Ink);
        n.SizeFlagsHorizontal = SizeFlags.ShrinkEnd;
        n.CustomMinimumSize = new Vector2(28, 0);
        row.AddChild(n);
        if (minus != null) row.AddChild(Small("-", minus, minusOn));
        if (plus != null) row.AddChild(Small("+", plus, plusOn));
        return panel;
    }

    // --- production -------------------------------------------------------------------------

    private void BuildProduction(CityScreen s, City city)
    {
        GameUi.Clear(_production);
        var col = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _production.AddChild(col);
        col.AddChild(Label("Production", 22, Ink));

        if (s.CurrentName is { } name)
        {
            col.AddChild(Label($"{name}   {(s.CurrentTurns is int t ? $"{t} turns" : "stalled")}", 16, Ink));
            var bar = new ProgressBar { MinValue = 0, MaxValue = s.CurrentCost, Value = s.CurrentProgress, ShowPercentage = false, CustomMinimumSize = new Vector2(0, 14) };
            col.AddChild(bar);
            col.AddChild(Label($"{s.CurrentProgress} / {s.CurrentCost}", 12, InkDim));
        }
        else col.AddChild(Label("Nothing in production. Pick something below.", 13, GameUi.Bad));

        var actions = new HBoxContainer();
        var purchase = GameUi.Btn("Purchase", () => { }, false);
        purchase.TooltipText = "There is no gold in the simulation yet.";
        var forced = GameUi.Btn("Forced Labor", () => { }, false);
        forced.TooltipText = "Needs a civic that allows it; not implemented yet.";
        actions.AddChild(purchase);
        actions.AddChild(forced);
        col.AddChild(actions);

        var tabs = new HBoxContainer();
        tabs.AddChild(GameUi.Btn("Buildings", () => Tab = ProdTab.Buildings, true, Tab == ProdTab.Buildings));
        tabs.AddChild(GameUi.Btn("Units", () => Tab = ProdTab.Units, true, Tab == ProdTab.Units));
        tabs.AddChild(GameUi.Btn("Projects", () => { }, false));
        tabs.AddChild(GameUi.Btn("Wonders", () => { }, false));
        col.AddChild(tabs);

        var scroll = new ScrollContainer { HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled, SizeFlagsVertical = SizeFlags.ExpandFill };
        col.AddChild(scroll);
        var list = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        scroll.AddChild(list);
        var items = Tab == ProdTab.Buildings ? s.Buildings : s.Units;
        if (items.Count == 0) list.AddChild(Label("Nothing available. Research more technologies.", 12, InkDim));
        foreach (var item in items)
        {
            var it = item;
            string turns = it.Turns is int n ? $"{n} turns" : "never";
            string text = $"{it.Name}    {it.Cost} prod, {turns}" + (it.Effects.Length > 0 ? $"\n{it.Effects}" : "");
            Action pick = it.IsUnit
                ? () => _session.Submit(new StartTrainingCommand { Player = GameSession.Human, CityId = city.Id, Unit = it.Id })
                : () => _session.Submit(new StartBuildingCommand { Player = GameSession.Human, CityId = city.Id, Building = it.Id });
            var b = GameUi.Btn(text, pick, true, it.Current);
            b.Alignment = HorizontalAlignment.Left;
            b.CustomMinimumSize = new Vector2(0, 48);
            list.AddChild(b);
        }
    }

    private static Label Label(string text, int size, Color color, bool wrap = true)
    {
        var l = GameUi.Text(text, size, color);
        if (!wrap) { l.AutowrapMode = TextServer.AutowrapMode.Off; l.SizeFlagsHorizontal = SizeFlags.ShrinkBegin; }
        return l;
    }

    private static Button Small(string text, Action onClick, bool enabled)
    {
        var b = GameUi.Btn(text, onClick, enabled, false, 32);
        b.SizeFlagsHorizontal = SizeFlags.ShrinkEnd;
        return b;
    }
}
