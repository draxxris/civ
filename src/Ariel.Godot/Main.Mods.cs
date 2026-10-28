using Godot;

namespace Ariel.Client;

public partial class Main
{
    private ModDiscoveryResult _discovery = new([], []);
    private IReadOnlyList<string> _enabledMods = [];
    private ModManagerScreen? _modManager;
    private bool _loadAttempted;
    private string? _profileError;
    private string? _migrationNotice;
    private string ProfilePath => System.IO.Path.Combine(OS.GetUserDataDir(), "mod-profile.json");
    private bool AutomaticStartup => DisplayServer.GetName() == "headless" || _debug.ScreenshotPath != null || _debug.FpsProbe
        || OS.GetCmdlineUserArgs().Contains("--skip-mod-manager");

    private void BeginModStartup()
    {
        MigrateOldUserData();
        try
        {
            _discovery = ModDiscovery.DiscoverMerged(BundledModsRoots(), System.IO.Path.Combine(OS.GetUserDataDir(), "mods"),
                System.Environment.GetEnvironmentVariable(ModDiscovery.ModsEnvironmentVariable));
            foreach (var error in _discovery.Errors) GD.PrintErr(error);
            if (_discovery.Mods.Count == 0)
            {
                ModLoadFailed("No usable mod IDs found. Build with 'dotnet build src/Ariel.Godot' or install mods in ARIEL_MODS or user://mods.");
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

    /// <summary>Copy the previous application name's saves and mods once, before discovery reads the mod folder.</summary>
    private void MigrateOldUserData()
    {
        var userData = OS.GetUserDataDir();
        try
        {
            var report = UserDataMigration.Migrate(UserDataMigration.LegacyRootFor(userData), userData);
            if (report.Conflicts.Count == 0) return;
            _migrationNotice = "The old data folder holds a different copy of these files. Both copies were kept; "
                + "this run uses the new folder:\n" + string.Join("\n", report.Conflicts);
            GD.PrintErr(_migrationNotice);
        }
        catch (Exception error) when (error is System.IO.IOException or UnauthorizedAccessException)
        {
            // Starting with the new folder is always better than refusing to start.
            _migrationNotice = "Could not copy saves and mods from the old data folder: " + error.Message;
            GD.PrintErr(_migrationNotice);
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
        _modManager = new ModManagerScreen(_discovery, _enabledMods, _loadAttempted, _profileError, _migrationNotice,
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
