using Civ.Game;

namespace Civ.Client;

public sealed partial class GameSession
{
    /// <summary>The whole game as save JSON.</summary>
    public string SaveToJson() => Game.SaveGame.Save(Sim);

    /// <summary>Write the game to a file, creating its folder. A failure becomes <see cref="Message"/>.</summary>
    public bool SaveGame(string path)
    {
        try
        {
            if (Path.GetDirectoryName(path) is { Length: > 0 } dir) Directory.CreateDirectory(dir);
            File.WriteAllText(path, SaveToJson());
            SetMessage("Game saved");
            return true;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            SetMessage($"Can't save: {e.Message}");
            return false;
        }
    }

    /// <summary>Replace the simulation with a saved one from the same ruleset. A bad save leaves the current game untouched.</summary>
    public bool LoadFromJson(string json)
    {
        Simulation loaded;
        try { loaded = Game.SaveGame.Load(Sim.Content, json); }
        catch (Exception e) when (e is InvalidDataException or System.Text.Json.JsonException or KeyNotFoundException or InvalidOperationException)
        {
            SetMessage($"Can't load: {e.Message}");
            return false;
        }
        Sim = loaded;
        _gotos.Clear();
        _choices.Clear();
        _choicesKey = (-1, -1);
        ResetNotifications();
        SelectedCityId = null;
        SelectedUnitId = null;
        SelectNextUnit();
        Message = "Game loaded";
        Revision++;
        WorldRevision++;
        return true;
    }

    public bool LoadGame(string path)
    {
        string json;
        try { json = File.ReadAllText(path); }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            SetMessage($"Can't load: {e.Message}");
            return false;
        }
        return LoadFromJson(json);
    }
}
