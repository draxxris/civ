using Civ.BaseGame;
using Civ.Content;
using Civ.Game;
using Godot;

namespace Civ.Client;

/// <summary>Top bar plus a left panel with Selection / Research / Civics tabs. Rebuilt only when game or selection state changes.</summary>
public partial class GameUi : CanvasLayer
{
    private enum Tab { Selection, Research, Civics }

    private const float PanelWidth = 340f;
    private const float TopBarHeight = 36f;

    internal static readonly Color Dim = new(0.65f, 0.68f, 0.74f);
    internal static readonly Color Bad = new(0.95f, 0.45f, 0.40f);
    internal static readonly Color Accent = new(1f, 0.82f, 0.30f);

    private readonly GameSession _session;
    private readonly PanelContainer _top = new();
    private readonly PanelContainer _left = new();
    private readonly CityUi _city;
    private Tab _tab = Tab.Selection;
    private (ulong, Tab, CityUi.ProdTab, bool) _shown;
    private bool _built;

    private Simulation Sim => _session.Sim;
    private Player Me => _session.Me;

    public GameUi(GameSession session)
    {
        _session = session;
        var panel = Box(new Color(0.09f, 0.10f, 0.13f, 0.95f), 8);
        _top.AddThemeStyleboxOverride("panel", panel);
        _left.AddThemeStyleboxOverride("panel", panel);
        _top.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.TopWide);
        _top.OffsetBottom = TopBarHeight;
        _left.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.LeftWide);
        _left.OffsetTop = TopBarHeight;
        _left.OffsetRight = PanelWidth;
        AddChild(_top);
        AddChild(_left);
        _city = new CityUi(session);
        AddChild(_city);
    }

    public void Refresh()
    {
        var city = _session.City;
        var key = (_session.Version, _tab, _city.Tab, city != null);
        if (_built && key == _shown) return;
        _shown = key;
        _built = true;
        _top.Visible = _left.Visible = city == null;
        _city.Visible = city != null;
        if (city != null) { _city.Build(city); return; }
        BuildTop();
        BuildLeft();
    }

    // --- widgets --------------------------------------------------------------------------

    internal static StyleBoxFlat Box(Color fill, int margin = 4)
    {
        var s = new StyleBoxFlat { BgColor = fill, CornerRadiusTopLeft = 4, CornerRadiusTopRight = 4, CornerRadiusBottomLeft = 4, CornerRadiusBottomRight = 4 };
        s.SetContentMarginAll(margin);
        return s;
    }

    internal static void Clear(Node n)
    {
        foreach (var child in n.GetChildren()) { n.RemoveChild(child); child.QueueFree(); }
    }

    internal static Label Text(string text, int size = 13, Color? color = null)
    {
        var l = new Label { Text = text, AutowrapMode = TextServer.AutowrapMode.WordSmart, SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        l.AddThemeFontSizeOverride("font_size", size);
        if (color is { } c) l.AddThemeColorOverride("font_color", c);
        return l;
    }

    internal static Button Btn(string text, Action onClick, bool enabled = true, bool active = false, float minWidth = 0)
    {
        var b = new Button { Text = text, Disabled = !enabled, SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, CustomMinimumSize = new Vector2(minWidth, 26) };
        b.AddThemeFontSizeOverride("font_size", 12);
        var fill = active ? new Color(0.78f, 0.62f, 0.18f) : new Color(0.20f, 0.23f, 0.30f);
        b.AddThemeStyleboxOverride("normal", Box(fill));
        b.AddThemeStyleboxOverride("hover", Box(fill.Lightened(0.18f)));
        b.AddThemeStyleboxOverride("pressed", Box(fill.Lightened(0.3f)));
        b.AddThemeStyleboxOverride("disabled", Box(fill.Darkened(0.35f)));
        b.Pressed += onClick;
        return b;
    }

    private void BuildTop()
    {
        Clear(_top);
        var row = new HBoxContainer();
        _top.AddChild(row);
        var me = Me;
        var civ = Sim.CivilizationOf(me.Id);
        row.AddChild(Text($"Turn {Sim.State.Turn}", 14));
        row.AddChild(Text($"{civ?.Name ?? me.Name}  culture {me.Culture}" + (Sim.Content.CultureLevelFor(me.Culture) is { } lvl ? $" ({lvl.Name})" : ""), 13));
        row.AddChild(me.Researching == null
            ? Text("Research: none (pick one in the Research tab)", 13, Bad)
            : Text($"Research: {Sim.Content.Techs.Get(me.Researching).Name} {me.ResearchProgress}/{Sim.Content.Techs.Get(me.Researching).Cost}", 13));
        foreach (var r in ResourceView.Player(Sim, me.Id))
            row.AddChild(Text($"{r.Name} {ResourceView.Amount(r)}", 13, Color.FromHtml(r.Color)));
        if (_session.Message.Length > 0) row.AddChild(Text(_session.Message, 13, Bad));
        var end = Btn("End turn", _session.EndTurn, minWidth: 90);
        end.SizeFlagsHorizontal = Control.SizeFlags.ShrinkEnd;
        row.AddChild(end);
    }

    private void BuildLeft()
    {
        Clear(_left);
        var scroll = new ScrollContainer { HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
        _left.AddChild(scroll);
        var col = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        scroll.AddChild(col);

        var tabs = new HBoxContainer();
        foreach (var t in Enum.GetValues<Tab>())
        {
            var tab = t;
            tabs.AddChild(Btn(tab.ToString(), () => _tab = tab, true, _tab == tab));
        }
        col.AddChild(tabs);

        switch (_tab)
        {
            case Tab.Selection: SelectionTab(col); break;
            case Tab.Research: ResearchTab(col); break;
            case Tab.Civics: CivicsTab(col); break;
        }
        col.AddChild(Text("WASD pan, wheel zoom, LMB select, RMB move/attack, Enter end turn", 11, Dim));
    }

    // --- tabs -----------------------------------------------------------------------------

    private void SelectionTab(Container col)
    {
        if (_session.City is { } city) CityPanel(col, city);
        else if (_session.Unit is { } unit) UnitPanel(col, unit);
        else col.AddChild(Text("Select a unit or city.", 13, Dim));
    }

    private void ResearchTab(Container col)
    {
        var me = Me;
        col.AddChild(Text("Available technologies", 14));
        var available = Sim.Content.Techs.All.Where(t => !me.Techs.Contains(t.Id) && t.Prerequisites.All(me.Techs.Contains)).ToList();
        if (available.Count == 0) col.AddChild(Text("Nothing left to research.", 13, Dim));
        foreach (var t in available)
        {
            var tech = t;
            col.AddChild(Btn($"{tech.Name}  ({tech.Cost})", () => _session.Submit(new SetResearchCommand { Player = GameSession.Human, Tech = tech.Id }), true, me.Researching == tech.Id));
        }
        col.AddChild(Text("Known", 14));
        col.AddChild(Text(me.Techs.Count == 0 ? "none" : string.Join(", ", me.Techs.Select(id => Sim.Content.Techs.Get(id).Name)), 12, Dim));
    }

    private void CivicsTab(Container col)
    {
        var me = Me;
        foreach (var group in Sim.Content.Civics.All.GroupBy(c => c.Category).OrderBy(g => g.Key, StringComparer.Ordinal))
        {
            col.AddChild(Text(char.ToUpper(group.Key[0]) + group.Key[1..], 14));
            foreach (var civic in group)
            {
                var c = civic;
                bool known = c.RequiresTech == null || me.Techs.Contains(c.RequiresTech);
                col.AddChild(Btn(c.Name + (known ? "" : $"  (needs {Sim.Content.Techs.Get(c.RequiresTech!).Name})"),
                    () => _session.Submit(new SetCivicCommand { Player = GameSession.Human, Civic = c.Id }), known, me.Civics.GetValueOrDefault(c.Category) == c.Id));
                var effects = CivicEffects(c);
                if (effects.Length > 0) col.AddChild(Text(effects, 11, Dim));
            }
        }
    }

    private static string CivicEffects(CivicDef c)
    {
        var parts = new List<string>();
        if (c.ResearchBonus != 0) parts.Add($"{c.ResearchBonus:+#;-#}% research");
        if (c.ProductionBonus != 0) parts.Add($"{c.ProductionBonus:+#;-#}% production");
        if (c.CultureBonus != 0) parts.Add($"{c.CultureBonus:+#;-#}% culture");
        foreach (var (id, n) in c.SpecialistSlots) parts.Add(n < 0 ? $"unlimited {Short(id)}s" : $"{n} {Short(id)} slot{(n == 1 ? "" : "s")}");
        return string.Join(", ", parts);
    }

    private static string Short(string id) => id[(id.IndexOf(':') + 1)..].Replace('_', ' ');

    // --- unit -----------------------------------------------------------------------------

    private void UnitPanel(Container col, Unit unit)
    {
        var def = Sim.Content.Units.Get(unit.Type);
        col.AddChild(Text(def.Name, 16));
        col.AddChild(Text($"HP {unit.Health}  Strength {def.Strength}  Moves {unit.MovesLeft}/{UnitRules.MaxMoves(Sim, unit)}"));
        col.AddChild(Text($"Experience {unit.Experience}  Level {UnitRules.Level(Sim, unit)}"));
        if (unit.Promotions.Count > 0)
            col.AddChild(Text("Promotions: " + string.Join(", ", UnitRules.PromotionsOf(Sim, unit).Select(p => p.Name)), 12, Dim));

        if (def.CanFoundCity)
        {
            var pos = unit.Position;
            var id = unit.Id;
            col.AddChild(Btn("Found city here", () => _session.Submit(new FoundCityCommand { Player = GameSession.Human, Position = pos, UnitId = id })));
        }

        foreach (var action in ResourceView.Spreadable(Sim, GameSession.Human, unit))
        {
            var a = action;
            var b = Btn($"Spread {a.Name}" + (a.Cost.Length > 0 ? $" ({a.Cost})" : ""), () => _session.SpreadInstitution(unit, a.Id), a.Blocked == null);
            b.TooltipText = a.Blocked ?? "";
            col.AddChild(b);
        }

        int unspent = UnitRules.UnspentPromotions(Sim, unit);
        if (unspent > 0)
        {
            col.AddChild(Text($"Choose a promotion ({unspent} available)", 14, Accent));
            foreach (var promo in UnitRules.AvailablePromotions(Sim, unit))
            {
                var p = promo;
                var uid = unit.Id;
                col.AddChild(Btn(p.Name, () => _session.Submit(new PromoteUnitCommand { Player = GameSession.Human, UnitId = uid, Promotion = p.Id })));
                if (p.Description.Length > 0) col.AddChild(Text(p.Description, 11, Dim));
            }
        }
        col.AddChild(Text("Right-click a tile to move or attack.", 11, Dim));
    }

    // --- city -----------------------------------------------------------------------------

    private void CityPanel(Container col, City city)
    {
        var y = Yields.OfCity(Sim, city);
        col.AddChild(Text($"{city.Name}  (population {city.Population})", 16));
        int surplus = y.Food - CityRules.FoodPerCitizen * city.Population;
        col.AddChild(Text($"Food {y.Food} ({surplus:+#;-#;0}/turn)  box {city.FoodStored}/{CityRules.FoodToGrow(city.Population)}"));
        col.AddChild(Text($"Production {y.Production}   Commerce {y.Commerce}   Culture {y.Culture}"));
        col.AddChild(Text("Click highlighted tiles on the map to put citizens to work or unassign them.", 11, Dim));

        col.AddChild(Text("Specialists", 14));
        foreach (var spec in Sim.Content.Specialists.All)
        {
            var s = spec;
            int count = city.Specialists.GetValueOrDefault(s.Id);
            int slots = CityRules.Slots(Sim, city, s.Id);
            if (!s.Unlimited && slots == 0 && count == 0) continue;
            string slotText = s.Unlimited ? "" : slots == int.MaxValue ? "  (unlimited)" : $"  (slots {slots})";
            var row = new HBoxContainer();
            row.AddChild(Text($"{s.Name}  x{count}{slotText}  {SpecialistYield(s)}", 12));
            if (!s.Unlimited)
            {
                row.AddChild(Btn("-", () => _session.ChangeSpecialist(city, s.Id, -1), count > 0, false, 28));
                row.AddChild(Btn("+", () => _session.ChangeSpecialist(city, s.Id, +1), slots > count, false, 28));
            }
            col.AddChild(row);
        }

        var me = Me;
        col.AddChild(Text("Production", 14));
        string current = city.Building != null ? $"{Sim.Content.Buildings.Get(city.Building).Name} {city.BuildProgress}/{Sim.Content.Buildings.Get(city.Building).Cost}"
            : city.Training != null ? $"{Sim.Content.Units.Get(city.Training).Name} {city.TrainProgress}/{Sim.Content.Units.Get(city.Training).Cost}"
            : "nothing";
        col.AddChild(Text("Building: " + current));
        if (city.Buildings.Count > 0)
            col.AddChild(Text("Has: " + string.Join(", ", city.Buildings.Select(b => Sim.Content.Buildings.Get(b).Name)), 12, Dim));

        foreach (var building in Sim.Content.Buildings.All.Where(b => !city.Buildings.Contains(b.Id) && (b.RequiresTech == null || me.Techs.Contains(b.RequiresTech))))
        {
            var b = building;
            col.AddChild(Btn($"{b.Name} ({b.Cost})", () => _session.Submit(new StartBuildingCommand { Player = GameSession.Human, CityId = city.Id, Building = b.Id }), true, city.Building == b.Id));
        }
        foreach (var unit in Sim.Content.Units.All.Where(u => u.RequiresTech == null || me.Techs.Contains(u.RequiresTech)))
        {
            var u = unit;
            col.AddChild(Btn($"Train {u.Name} ({u.Cost})", () => _session.Submit(new StartTrainingCommand { Player = GameSession.Human, CityId = city.Id, Unit = u.Id }), true, city.Training == u.Id));
        }
    }

    private static string SpecialistYield(SpecialistDef s)
    {
        var parts = new List<string>();
        if (s.Food != 0) parts.Add($"+{s.Food} food");
        if (s.Production != 0) parts.Add($"+{s.Production} prod");
        if (s.Commerce != 0) parts.Add($"+{s.Commerce} commerce");
        if (s.Culture != 0) parts.Add($"+{s.Culture} culture");
        return parts.Count == 0 ? "" : "[" + string.Join(" ", parts) + "]";
    }
}
