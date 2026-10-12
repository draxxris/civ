using Civ.BaseGame;
using Civ.Content;
using Civ.Game;
using Prowl.PaperUI;
using Prowl.PaperUI.LayoutEngine;
using Prowl.Runtime.Resources;
using Prowl.Scribe;
using TextAlignment = Prowl.PaperUI.TextAlignment;
using Prowl.Vector;

namespace Civ.Client;

/// <summary>Immediate-mode Paper UI: top bar plus a left panel with Selection / Research / Civics tabs.</summary>
public sealed class GameUi
{
    private enum Tab { Selection, Research, Civics }

    private const float PanelWidth = 340f;
    private const float TopBarHeight = 36f;

    private static readonly Color Ink = new(0.93f, 0.94f, 0.96f, 1f);
    private static readonly Color Dim = new(0.65f, 0.68f, 0.74f, 1f);
    private static readonly Color Panel = new(0.09f, 0.10f, 0.13f, 0.92f);
    private static readonly Color Button = new(0.20f, 0.23f, 0.30f, 1f);
    private static readonly Color ButtonHot = new(0.30f, 0.36f, 0.50f, 1f);
    private static readonly Color Active = new(0.78f, 0.62f, 0.18f, 1f);
    private static readonly Color Bad = new(0.95f, 0.45f, 0.40f, 1f);

    private readonly CivGame _game;
    private readonly FontFile _font = FontAsset.LoadDefault().FontFile;
    private Paper _p = null!;
    private Tab _tab = Tab.Selection;

    public GameUi(CivGame game) { _game = game; }

    private Simulation Sim => _game.Sim;
    private Player Me => Sim.State.PlayerById(CivGame.Human);

    public bool PointerOverUi(Paper paper) =>
        paper.IsPointerOverRect(0, 0, PanelWidth, 4000) || paper.IsPointerOverRect(0, 0, 4000, TopBarHeight);

    public void Build(Paper paper)
    {
        _p = paper;
        TopBar();
        using (_p.Column("panel").PositionType(PositionType.SelfDirected)
            .Left(0).Top(TopBarHeight).Width(PanelWidth).Height(UnitValue.Stretch())
            .BackgroundColor(Panel).Padding(8).ColBetween(4).Clip().Enter())
        {
            using (_p.Row("tabs").Height(26).RowBetween(4).Enter())
            {
                TabButton("Selection", Tab.Selection);
                TabButton("Research", Tab.Research);
                TabButton("Civics", Tab.Civics);
            }
            switch (_tab)
            {
                case Tab.Selection: SelectionTab(); break;
                case Tab.Research: ResearchTab(); break;
                case Tab.Civics: CivicsTab(); break;
            }
            Label("hint", "WASD pan, wheel zoom, LMB select, RMB move/attack, Enter end turn", 11, Dim);
        }
    }

    // --- widgets --------------------------------------------------------------------------

    private void Label(string id, string text, float size = 13, Color? color = null, float height = 18) =>
        _p.Box(id).Height(height).Text(text, _font).FontSize(size).TextColor(color ?? Ink).Alignment(TextAlignment.MiddleLeft);

    private void Btn(string id, string text, Action onClick, bool enabled = true, bool active = false, UnitValue? width = null)
    {
        var b = _p.Box(id).Height(24).Width(width ?? UnitValue.Stretch()).Rounded(4)
            .BackgroundColor(active ? Active : Button);
        if (enabled) b.Hovered.BackgroundColor(active ? Active : ButtonHot).End();
        b.Text(text, _font).FontSize(12).TextColor(enabled ? Ink : Dim).Alignment(TextAlignment.MiddleCenter);
        if (enabled) b.OnClick(onClick, (cb, _) => cb());
    }

    private void TabButton(string label, Tab tab) => Btn($"tab_{tab}", label, () => _tab = tab, true, _tab == tab);

    private void TopBar()
    {
        using (_p.Row("topbar").PositionType(PositionType.SelfDirected).Left(0).Top(0).Width(UnitValue.Stretch()).Height(TopBarHeight)
            .BackgroundColor(Panel).Padding(6).RowBetween(10).Enter())
        {
            var me = Me;
            var civ = Sim.CivilizationOf(me.Id);
            Label("tb_turn", $"Turn {Sim.State.Turn}", 14, Ink, 24);
            Label("tb_civ", $"{civ?.Name ?? me.Name}  culture {me.Culture}" +
                (Sim.Content.CultureLevelFor(me.Culture) is { } lvl ? $" ({lvl.Name})" : ""), 13, Ink, 24);
            Label("tb_res", me.Researching == null
                ? "Research: none (pick one in the Research tab)"
                : $"Research: {Sim.Content.Techs.Get(me.Researching).Name} {me.ResearchProgress}/{Sim.Content.Techs.Get(me.Researching).Cost}", 13,
                me.Researching == null ? Bad : Ink, 24);
            if (_game.Message.Length > 0) Label("tb_msg", _game.Message, 13, Bad, 24);
            Btn("tb_end", "End turn", _game.EndTurn, true, false, 90);
        }
    }

    // --- tabs -----------------------------------------------------------------------------

    private void SelectionTab()
    {
        if (_game.City is { } city) CityPanel(city);
        else if (_game.Unit is { } unit) UnitPanel(unit);
        else Label("none", "Select a unit or city.", 13, Dim);
    }

    private void ResearchTab()
    {
        var me = Me;
        Label("r_head", "Available technologies", 14);
        var available = Sim.Content.Techs.All
            .Where(t => !me.Techs.Contains(t.Id) && t.Prerequisites.All(me.Techs.Contains)).ToList();
        if (available.Count == 0) Label("r_none", "Nothing left to research.", 13, Dim);
        foreach (var t in available)
        {
            var tech = t;
            Btn($"r_{tech.Id}", $"{tech.Name}  ({tech.Cost})", () => _game.Submit(new SetResearchCommand { Player = CivGame.Human, Tech = tech.Id }),
                true, me.Researching == tech.Id);
        }
        Label("r_known_h", "Known", 14);
        Label("r_known", me.Techs.Count == 0 ? "none" : string.Join(", ", me.Techs.Select(id => Sim.Content.Techs.Get(id).Name)), 12, Dim, 40);
    }

    private void CivicsTab()
    {
        var me = Me;
        foreach (var group in Sim.Content.Civics.All.GroupBy(c => c.Category).OrderBy(g => g.Key, StringComparer.Ordinal))
        {
            Label($"cv_{group.Key}", char.ToUpper(group.Key[0]) + group.Key[1..], 14);
            foreach (var civic in group)
            {
                var c = civic;
                bool known = c.RequiresTech == null || me.Techs.Contains(c.RequiresTech);
                string extra = known ? "" : $"  (needs {Sim.Content.Techs.Get(c.RequiresTech!).Name})";
                Btn($"cv_{c.Id}", c.Name + extra, () => _game.Submit(new SetCivicCommand { Player = CivGame.Human, Civic = c.Id }),
                    known, me.Civics.GetValueOrDefault(c.Category) == c.Id);
                var effects = CivicEffects(c);
                if (effects.Length > 0) Label($"cve_{c.Id}", effects, 11, Dim, 14);
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

    private void UnitPanel(Unit unit)
    {
        var def = Sim.Content.Units.Get(unit.Type);
        Label("u_name", def.Name, 16);
        Label("u_stats", $"HP {unit.Health}  Strength {def.Strength}  Moves {unit.MovesLeft}/{UnitRules.MaxMoves(Sim, unit)}", 13);
        int level = UnitRules.Level(Sim, unit);
        Label("u_xp", $"Experience {unit.Experience}  Level {level}", 13);

        if (unit.Promotions.Count > 0)
            Label("u_promos", "Promotions: " + string.Join(", ", UnitRules.PromotionsOf(Sim, unit).Select(p => p.Name)), 12, Dim, 36);

        if (def.CanFoundCity)
        {
            var pos = unit.Position;
            var id = unit.Id;
            Btn("u_found", "Found city here", () =>
            {
                _game.Submit(new FoundCityCommand { Player = CivGame.Human, Position = pos, UnitId = id });
                _game.SelectedUnit = null;
            });
        }

        int unspent = UnitRules.UnspentPromotions(Sim, unit);
        if (unspent > 0)
        {
            Label("u_pick", $"Choose a promotion ({unspent} available)", 14, Active);
            foreach (var promo in UnitRules.AvailablePromotions(Sim, unit))
            {
                var p = promo;
                var uid = unit.Id;
                Btn($"u_p_{p.Id}", p.Name, () => _game.Submit(new PromoteUnitCommand { Player = CivGame.Human, UnitId = uid, Promotion = p.Id }));
                if (p.Description.Length > 0) Label($"u_pd_{p.Id}", p.Description, 11, Dim, 14);
            }
        }
        Label("u_help", "Right-click a tile to move or attack.", 11, Dim);
    }

    // --- city -----------------------------------------------------------------------------

    private void CityPanel(City city)
    {
        var y = Yields.OfCity(Sim, city);
        Label("c_name", $"{city.Name}  (population {city.Population})", 16);
        int surplus = y.Food - CityRules.FoodPerCitizen * city.Population;
        Label("c_food", $"Food {y.Food} ({surplus:+#;-#;0}/turn)  box {city.FoodStored}/{CityRules.FoodToGrow(city.Population)}", 13);
        Label("c_yield", $"Production {y.Production}   Commerce {y.Commerce}   Culture {y.Culture}", 13);
        Label("c_tiles", "Click highlighted tiles on the map to put citizens to work or unassign them.", 11, Dim, 28);

        Label("c_spec_h", "Specialists", 14);
        foreach (var spec in Sim.Content.Specialists.All)
        {
            var s = spec;
            int count = city.Specialists.GetValueOrDefault(s.Id);
            int slots = CityRules.Slots(Sim, city, s.Id);
            if (!s.Unlimited && slots == 0 && count == 0) continue;
            string slotText = s.Unlimited ? "" : slots == int.MaxValue ? "  (unlimited)" : $"  (slots {slots})";
            using (_p.Row($"c_s_{s.Id}").Height(24).RowBetween(4).Enter())
            {
                _p.Box($"c_sl_{s.Id}").Height(24).Width(UnitValue.Stretch())
                    .Text($"{s.Name}  x{count}{slotText}  {SpecialistYield(s)}", _font).FontSize(12).TextColor(Ink).Alignment(TextAlignment.MiddleLeft);
                if (!s.Unlimited)
                {
                    Btn($"c_sm_{s.Id}", "-", () => _game.ChangeSpecialist(city, s.Id, -1), count > 0, false, 24);
                    Btn($"c_sp_{s.Id}", "+", () => _game.ChangeSpecialist(city, s.Id, +1), slots > count, false, 24);
                }
            }
        }

        var me = Me;
        Label("c_prod_h", "Production", 14);
        string current = city.Building != null ? $"{Sim.Content.Buildings.Get(city.Building).Name} {city.BuildProgress}/{Sim.Content.Buildings.Get(city.Building).Cost}"
            : city.Training != null ? $"{Sim.Content.Units.Get(city.Training).Name} {city.TrainProgress}/{Sim.Content.Units.Get(city.Training).Cost}"
            : "nothing";
        Label("c_cur", "Building: " + current, 13);
        if (city.Buildings.Count > 0)
            Label("c_has", "Has: " + string.Join(", ", city.Buildings.Select(b => Sim.Content.Buildings.Get(b).Name)), 12, Dim, 30);

        foreach (var building in Sim.Content.Buildings.All.Where(b => !city.Buildings.Contains(b.Id) && (b.RequiresTech == null || me.Techs.Contains(b.RequiresTech))))
        {
            var b = building;
            Btn($"c_b_{b.Id}", $"{b.Name} ({b.Cost})", () => _game.Submit(new StartBuildingCommand { Player = CivGame.Human, CityId = city.Id, Building = b.Id }),
                true, city.Building == b.Id);
        }
        foreach (var unit in Sim.Content.Units.All.Where(u => u.RequiresTech == null || me.Techs.Contains(u.RequiresTech)))
        {
            var u = unit;
            Btn($"c_u_{u.Id}", $"Train {u.Name} ({u.Cost})", () => _game.Submit(new StartTrainingCommand { Player = CivGame.Human, CityId = city.Id, Unit = u.Id }),
                true, city.Training == u.Id);
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
