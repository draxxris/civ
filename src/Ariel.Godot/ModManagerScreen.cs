using Godot;

namespace Ariel.Client;

/// <summary>Manifest-only view. No catalog or executable mod instances are needed.</summary>
internal sealed partial class ModManagerScreen : Control
{
    private readonly IReadOnlyList<InstalledMod> _installed;
    private readonly HashSet<string> _enabled;
    private readonly Dictionary<string, CheckBox> _checks = new(StringComparer.Ordinal);
    private readonly Dictionary<string, Label> _reasons = new(StringComparer.Ordinal);
    private readonly Label _messages = Ui.Text("", 14, Ui.Bad);
    private readonly Label _failure = Ui.Text("", 14, Ui.Bad);
    private readonly Button _apply;
    private readonly ScrollContainer _errorScroll;
    private bool _profileInvalid;

    public event Action<IReadOnlyList<string>>? ApplyRequested;

    public ModManagerScreen(ModDiscoveryResult discovery, IEnumerable<string> enabled, bool restartRequired, string? profileError, string? notice, Action? cancel)
    {
        _installed = discovery.Mods;
        _enabled = new(enabled, StringComparer.Ordinal);
        _profileInvalid = profileError != null;
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        var panel = Ui.Panel(margin: 24);
        panel.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(panel);
        var root = Ui.VBox(10);
        panel.AddChild(root);
        root.AddChild(Ui.Text("Mods", 30, Ui.Accent));
        root.AddChild(Ui.Text(restartRequired
            ? "Mod assemblies cannot be unloaded. Save your selection, quit, and start the game again."
            : "Choose mods before the game loads their assemblies. Required mods are included automatically."));
        root.AddChild(Ui.Text("Mod code runs with your account permissions. Enable only mods that you trust. Game options are separate.", 14, Ui.Dim));
        root.AddChild(Ui.Text("Saved profiles keep new mods disabled. Disable missing saved IDs to remove them from the profile.", 14, Ui.Dim));
        root.AddChild(Ui.Text("Source priority: ARIEL_MODS > user-installed > bundled. Each winning copy replaces the entire mod.", 14, Ui.Dim));
        var actions = Ui.HBox(8);
        actions.AddChild(Ui.Button("Enable all / reset profile", () => Reset(_installed.Select(m => m.Manifest.Id))));
        actions.AddChild(Ui.Button("Disable all / reset profile", () => Reset([])));
        root.AddChild(actions);
        root.AddChild(BuildModRows());
        if (discovery.Errors.Count > 0)
        {
            var diagnostics = Ui.Scroll(Ui.Text(string.Join("\n", discovery.Errors), 14, Ui.Bad), 60);
            diagnostics.SizeFlagsVertical = SizeFlags.Fill;
            root.AddChild(diagnostics);
        }
        _errorScroll = Ui.Scroll(_messages, 70);
        _errorScroll.SizeFlagsVertical = SizeFlags.Fill;
        root.AddChild(_errorScroll);
        root.AddChild(_failure);
        _failure.Text = profileError ?? "";
        root.AddChild(Ui.Text(notice ?? "", 14, Ui.Warn));
        _apply = Ui.Button(restartRequired ? "Save profile and quit" : "Continue", () => ApplyRequested?.Invoke(_enabled.Order(StringComparer.Ordinal).ToArray()), size: 20);
        root.AddChild(_apply);
        if (cancel != null) root.AddChild(Ui.Button("Back to game setup", cancel));
        RefreshSelection();
    }

    private Control BuildModRows()
    {
        var rows = Ui.VBox(8);
        foreach (var group in _installed.GroupBy(m => m.Manifest.Id).OrderBy(g => g.Key, StringComparer.Ordinal))
        {
            string title = string.Join(" / ", group.Select(m => $"{m.Manifest.Name} {m.Manifest.Version}"));
            var sources = string.Join("\n", group.Select(SourceDescription));
            AddRow(rows, group.Key, $"{title} ({group.Key})", sources, sources);
        }
        foreach (var id in _enabled.Except(_checks.Keys).Order(StringComparer.Ordinal))
            AddRow(rows, id, $"{id} — not installed", "Disable this saved ID to remove it from the profile.");
        return Ui.Scroll(rows);
    }

    private static string SourceDescription(InstalledMod mod)
    {
        string status = mod.Error == null ? "Selected source" : "Blocked source (invalid manifest)";
        string source = $"{status}: {mod.Source} — {mod.Root}";
        if (mod.Overridden.Count == 0) return source;
        return source + "\nOverrides (not loaded):\n" + string.Join("\n", mod.Overridden.Select(m => $"  {m.Source} — {m.Root}"));
    }

    private void AddRow(VBoxContainer rows, string id, string title, string tooltip, string? source = null)
    {
        var row = Ui.VBox(0);
        var check = new CheckBox { Text = title, TooltipText = tooltip };
        var reason = Ui.Text("", 13, Ui.Dim);
        check.Toggled += value =>
        {
            if (value) _enabled.Add(id); else _enabled.Remove(id);
            RefreshSelection();
        };
        _checks.Add(id, check);
        _reasons.Add(id, reason);
        row.AddChild(check);
        if (source != null)
        {
            var label = Ui.Text(source, 13, Ui.Dim);
            label.AutowrapMode = TextServer.AutowrapMode.WordSmart;
            row.AddChild(label);
        }
        row.AddChild(reason);
        rows.AddChild(row);
    }

    private void Reset(IEnumerable<string> ids)
    {
        _enabled.Clear();
        _enabled.UnionWith(ids);
        _profileInvalid = false;
        _failure.Text = "";
        RefreshSelection();
    }

    private void RefreshSelection()
    {
        var selection = ModSelection.Resolve(_installed, _enabled);
        foreach (var (id, check) in _checks)
        {
            bool required = selection.RequiredBy.TryGetValue(id, out var parents);
            bool explicitChoice = _enabled.Contains(id);
            check.SetPressedNoSignal(explicitChoice || required);
            check.Disabled = required && !explicitChoice;
            _reasons[id].Text = required ? $"Required by: {string.Join(", ", parents!)}" : explicitChoice ? "Enabled by your selection" : "Disabled";
        }
        _messages.Text = string.Join("\n", selection.Errors);
        _errorScroll.Visible = selection.Errors.Count > 0;
        _apply.Disabled = _profileInvalid || !selection.IsValid;
    }

    public void ShowError(string error) => _failure.Text = error;
}
