using Ariel.Game;
using Schema = Ariel.Content.Schema;
using Godot;

namespace Ariel.Client;

/// <summary>Rendered capture tour. Exercises real controls and commands, then checks the visible intelligence lifecycle.</summary>
internal sealed class EspionageCapture
{
    private static readonly string[] Stages = ["locked", "unlocked", "city", "mission", "protected", "turn", "revoked", "restored", "city-removed", "eliminated"];
    private readonly GameSession _session;
    private readonly City _target;
    private readonly EspionageTeamState _points = new();
    private readonly string _rivalName;
    private int _step;
    public string Stage => Stages[_step];

    public EspionageCapture(GameSession session)
    {
        _session = session;
        var sim = session.Sim;
        if (!sim.State.Players.Any(p => p.Id == 1)) throw new ArgumentException("The espionage capture needs at least one opponent.");
        foreach (var unit in sim.State.Units.ToArray()) sim.State.RemoveUnit(unit.Id);
        foreach (var city in sim.State.Cities.ToArray()) sim.State.RemoveCity(city.Id);
        AddCity(0, "Home", 14);
        _target = AddCity(1, "Watchtower", 57);
        AddCity(1, "Outpost", 80);
        var production = session.ProductionChoices(_target).Where(p => p.Available && !p.IsContinuous).Take(2).ToArray();
        _target.ProductionQueue.Add(new(production[0].Kind, production[0].Id, 23));
        _target.ProductionQueue.Add(new(production[1].Kind, production[1].Id, 7));
        sim.State.PlayerById(1).Treasury = 123;
        int member = sim.State.Players.Max(p => p.Id) + 1;
        sim.State.Players.Add(new Player { Id = member, Name = "Teammate", TeamId = sim.TeamOf(1).Id, Treasury = 345 });
        sim.TeamOf(1).Members.Add(member);
        sim.State.RebuildIndexes();
        var rival = sim.TeamOf(1);
        var technologies = sim.Content.Get<Schema.TechDef>().All.OrderBy(t => t.Id, StringComparer.Ordinal).Take(2).Select(t => t.Id).ToList();
        rival.Researching = technologies[0];
        rival.ResearchProgress = 17;
        rival.Extensions.Set(sim.Content, new ResearchTeamState { Queue = technologies });
        sim.Content.Services.Get<IDiplomacyService>().Single().EstablishContact(sim, session.MyTeam.Id, rival.Id);
        _points.Spent[rival.Id] = 100000;
        session.MyTeam.Extensions.Set(sim.Content, _points);
        var spy = new Unit { Id = sim.State.NextUnitId++, Owner = 0, Type = sim.Content.Get<Schema.UnitDef>().All.First(u => u.Spy).Id, Position = _target.Position, MovesLeft = 60 };
        sim.State.AddUnit(spy);
        session.SelectUnit(spy.Id);
        _rivalName = session.DescribeRivalIntelligence(rival.Id)!.Name;
    }

    private City AddCity(int owner, string name, int tile)
    {
        var state = _session.Sim.State;
        var city = new City { Id = state.NextCityId++, Owner = owner, Name = name, Position = state.Grid.HexOf(new TileId(tile)), Population = 3 };
        state.AddCity(city);
        state.PlotAt(city.Position).Owner = owner;
        return city;
    }

    public void Open(GameUi ui)
    {
        ui.ToggleEspionage();
        ui.Refresh();
        Press(ui, _rivalName);
        ui.Refresh();
    }

    public bool Advance(GameUi ui)
    {
        if (++_step == Stages.Length) return false;
        UpdateIntelligence(ui);
        UpdateCities(ui);
        ui.Refresh();
        foreach (var scroll in Descendants(ui).OfType<ScrollContainer>()
            .Where(s => Descendants(s).OfType<Label>().Any(l => l.Text == "Demographics and treasury")))
            scroll.SetDeferred(ScrollContainer.PropertyName.ScrollVertical, Stage == "city" ? 10000 : 0);
        return true;
    }

    private void UpdateIntelligence(GameUi ui)
    {
        var sim = _session.Sim;
        int rival = sim.TeamOf(1).Id;
        switch (Stage)
        {
            case "unlocked": SetPoints(100); break;
            case "mission": Press(ui, "Counter-espionage"); break;
            case "protected":
                sim.TeamOf(1).Extensions.Set(sim.Content, new EspionageTeamState
                    { CounterEspionage = new() { [0] = new(sim.State.Turn + 1, 200) } });
                _session.SetEspionageWeight(rival, 1);
                break;
            case "turn":
                foreach (var player in sim.State.Players.Where(p => p.Id != 0)) player.EndedTurn = true;
                _session.EndTurn();
                break;
            case "revoked": SetPoints(0); break;
            case "restored": SetPoints(100000); break;
        }
    }

    private void UpdateCities(GameUi ui)
    {
        var sim = _session.Sim;
        int rival = sim.TeamOf(1).Id;
        switch (Stage)
        {
            case "city": SelectTargetCity(ui); break;
            case "city-removed":
                SelectTargetCity(ui);
                ui.Refresh();
                sim.State.RemoveCity(_target.Id);
                _session.SetEspionageWeight(rival, 1);
                break;
            case "eliminated":
                foreach (var city in sim.State.Cities.Where(c => sim.TeamOf(c.Owner).Id == rival).ToArray()) sim.State.RemoveCity(city.Id);
                foreach (var unit in sim.State.Units.Where(u => sim.TeamOf(u.Owner).Id == rival).ToArray()) sim.State.RemoveUnit(unit.Id);
                _session.SetEspionageWeight(rival, 1);
                break;
        }
    }

    private void SelectTargetCity(GameUi ui)
    {
        var picker = Descendants(ui).OfType<OptionButton>().Single();
        int targetIndex = picker.GetItemIndex(_target.Id);
        picker.Select(targetIndex);
        picker.EmitSignal(OptionButton.SignalName.ItemSelected, (long)targetIndex);
    }

    private void SetPoints(long balance)
    {
        _session.MyTeam.Extensions.Get<EspionageTeamState>(_session.Sim.Content)!.Points[_session.Sim.TeamOf(1).Id] = balance;
        _session.SetEspionageWeight(_session.Sim.TeamOf(1).Id, 1);
    }

    public void Check(GameUi ui)
    {
        string text = string.Join("\n", Descendants(ModalContent(ui)).OfType<Label>().Where(l => l.IsVisibleInTree()).Select(l => l.Text));
        CheckLockedSections(text);
        CheckUnlockedSections(text);
        CheckProtection(text);
        CheckSelections(ui, text);
        var bounds = ((Control)ModalContent(ui).GetParent()).GetGlobalRect();
        var viewport = ui.GetViewport().GetVisibleRect();
        Require(viewport.Encloses(bounds), $"Panel exceeds viewport: {bounds}");
        GD.Print($"espionage UI check: {Stage} passed; panel {bounds}");
    }

    private void CheckLockedSections(string text)
    {
        switch (Stage)
        {
            case "locked": Require(text.Contains("Locked — unlock Demographics") && !text.Contains("123 gold reserves"), "Locked treasury leaked"); break;
            case "mission": Require(!text.Contains("Production queue · read-only") && !text.Contains("17/"), "Mission spending did not revoke details"); break;
            case "revoked": Require(!text.Contains("Watchtower") && !text.Contains("123 gold reserves"), "Revoked city or treasury remained"); break;
        }
    }

    private void CheckUnlockedSections(string text)
    {
        switch (Stage)
        {
            case "unlocked": Require(text.Contains("123 gold reserves") && text.Contains("345 gold reserves") && text.Contains("17/"), "Unlocked intelligence missing"); break;
            case "city":
                Require(text.Contains("Production queue · read-only") && text.Contains("23/"), "Read-only queue missing");
                Require(_session.SelectedCityId is null, "Rival picker opened city controls");
                break;
        }
    }

    private void CheckProtection(string text)
    {
        switch (Stage)
        {
            case "mission": Require(text.Contains("8 turns remaining") && text.Contains("Their missions against us cost 200% of normal"), "Mission status missing"); break;
            case "protected": Require(text.Contains("Our missions against them cost 200% of normal"), "Protection direction missing"); break;
            case "turn": Require(text.Contains("7 turns remaining") && text.Contains("Rival protection against us: inactive"), "Turn refresh or expiry missing"); break;
        }
    }

    private void CheckSelections(GameUi ui, string text)
    {
        switch (Stage)
        {
            case "restored": Require(Descendants(ui).OfType<OptionButton>().Single().Selected == 0, "Revoked city selection survived"); break;
            case "city-removed": Require(Descendants(ui).OfType<OptionButton>().Single().Selected == 0 && !text.Contains("Watchtower"), "Removed city remained selected"); break;
            case "eliminated": Require(text.Contains("Select a rival to inspect intelligence."), "Eliminated rival remained selected"); break;
        }
    }

    private static void Press(GameUi ui, string label)
    {
        var button = Descendants(ModalContent(ui)).OfType<Button>().Single(b => b.Text == label && b.IsVisibleInTree() && !b.Disabled);
        button.EmitSignal(BaseButton.SignalName.Pressed);
    }

    private static Node ModalContent(GameUi ui) => Descendants(ui).OfType<Label>()
        .Single(l => l.Text == "Espionage").GetParent().GetParent();

    private static IEnumerable<Node> Descendants(Node node)
    {
        foreach (var child in node.GetChildren())
        {
            yield return child;
            foreach (var descendant in Descendants(child)) yield return descendant;
        }
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException("Espionage capture: " + message);
    }
}
