using Godot;

namespace Civ.Client;

public partial class Main
{
    private ModDiscoveryResult _discovery = new([], []);
    private IReadOnlyList<string> _enabledMods = [];
    private ModManagerScreen? _modManager;
    private bool _loadAttempted;
    private string? _profileError;
    private string ProfilePath => System.IO.Path.Combine(OS.GetUserDataDir(), "mod-profile.json");
    private bool AutomaticStartup => DisplayServer.GetName() == "headless" || _debug.ScreenshotPath != null || _debug.FpsProbe
        || OS.GetCmdlineUserArgs().Contains("--skip-mod-manager");

    private void BeginModStartup()
    {
        try
        {
            _discovery = ModDiscovery.DiscoverMerged(BundledModsRoots(), System.IO.Path.Combine(OS.GetUserDataDir(), "mods"),
                System.Environment.GetEnvironmentVariable(ModDiscovery.ModsEnvironmentVariable));
            foreach (var error in _discovery.Errors) GD.PrintErr(error);
            if (_discovery.Mods.Count == 0)
            {
                ModLoadFailed("No usable mod IDs found. Build with 'dotnet build src/Civ.Godot' or install mods in CIV_MODS or user://mods.");
                return;
            }
            ReadModProfile();
            if (!AutomaticStartup) { ShowModManager(); return; }
            if (_profileError != null) { ModLoadFailed(_profileError); return; }
            var selection = ModSelection.Resolve(_discovery.Mods, _enabledMods);
            if (!selection.IsValid) { ModLoadFailed(string.Join("\n", selection.Errors)); return; }
            BeginCatalogLoad();
        }
        catch (Exception error) when (error is System.IO.IOException or UnauthorizedAccessException)
        {
            ModLoadFailed("Could not discover mods: " + error.Message);
        }
    }

    private void ReadModProfile()
    {
        try { _enabledMods = ModProfile.InitialSelection(_discovery.Mods, ModProfile.Load(ProfilePath)); }
        catch (Exception error) when (error is System.IO.IOException or System.IO.InvalidDataException or UnauthorizedAccessException or System.Text.Json.JsonException)
        {
            // A damaged profile never grants permission to load all installed code.
            _enabledMods = [];
            _profileError = "Could not read the mod profile: " + error.Message;
        }
    }

    private void ShowModManager(string? failure = null)
    {
        _setup?.Hide();
        _status.Visible = false;
        _modManager?.QueueFree();
        _modManager = new ModManagerScreen(_discovery, _enabledMods, _loadAttempted, _profileError,
            _setup == null ? null : CloseModManager);
        _modManager.ApplyRequested += ApplyModSelection;
        AddChild(_modManager);
        if (failure != null) _modManager.ShowError(failure);
    }

    private void CloseModManager()
    {
        _modManager?.QueueFree();
        _modManager = null;
        _setup?.Show();
    }

    private void ApplyModSelection(IReadOnlyList<string> enabled)
    {
        try { ModProfile.Save(ProfilePath, enabled); }
        catch (Exception error) when (error is System.IO.IOException or UnauthorizedAccessException)
        {
            _modManager?.ShowError("Could not save the mod profile: " + error.Message);
            return;
        }
        _profileError = null;
        _enabledMods = enabled;
        if (_loadAttempted) { GetTree().Quit(); return; }
        CloseModManager();
        BeginCatalogLoad();
    }

    private void BeginCatalogLoad()
    {
        _loadAttempted = true;
        _status.Text = "Loading selected mods…";
        _status.Visible = true;
        var enabled = _enabledMods.ToArray();
        _loading = Task.Run(() => GameCatalog.Load(_discovery.Mods, enabled));
        // Frame-limited smoke runs must wait for content. PollLoading handles errors on the main thread.
        if (DisplayServer.GetName() == "headless")
        {
            try { _loading.Wait(); }
            catch (AggregateException) { }
        }
    }

    private void ModLoadFailed(string error)
    {
        GD.PrintErr(error);
        if (AutomaticStartup)
        {
            _status.Text = error;
            GetTree().Quit(1);
            return;
        }
        ShowModManager(error);
    }

    private static string? SetupError(GameCatalog catalog)
    {
        if (catalog.WorldSizes.Count == 0 || catalog.GameSpeeds.Count == 0 || catalog.Handicaps.Count == 0 ||
            catalog.MapGenerators.Count == 0 || catalog.Leaders.Count == 0)
            return "The selected mods do not supply a playable setup. Enable mods with worlds, maps, speeds, handicaps, and leaders.";
        return null;
    }
}
