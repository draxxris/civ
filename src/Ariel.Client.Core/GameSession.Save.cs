using Ariel.Game;

namespace Ariel.Client;

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
            // Write beside the slot and move over it, so a crash, full disk or failed write mid-save leaves the
            // previous save whole instead of a truncated file.
            string temporary = path + ".tmp";
            try
            {
                using (var stream = new FileStream(temporary, FileMode.Create, FileAccess.Write, FileShare.None))
                {
                    using (var writer = new StreamWriter(stream, leaveOpen: true)) writer.Write(SaveToJson());
                    stream.Flush(flushToDisk: true);
                }
                File.Move(temporary, path, overwrite: true);
            }
            finally
            {
                if (File.Exists(temporary)) File.Delete(temporary);
            }
            SetMessage("Game saved");
            return true;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            SetMessage($"Can't save: {e.Message}");
            return false;
        }
    }

    /// <summary>Replace the simulation with a saved one from the same ruleset. A bad save leaves the current game
    /// untouched. A networked game refuses: every peer must load the same save, which the lobby arranges.</summary>
    public bool LoadFromJson(string json)
    {
        if (!_channel.PlaysAbsentHumans)
        {
            SetMessage("Load a multiplayer game from the multiplayer lobby.");
            return false;
        }
        Simulation loaded;
        try { loaded = Game.SaveGame.Load(Sim.Content, json); }
        catch (InvalidDataException e)
        {
            SetMessage($"Can't load: {e.Message}");
            return false;
        }
        ReplaceSimulation(loaded);
        Message = "Game loaded";
        return true;
    }

    /// <summary>Continue from another simulation of the same game, such as a loaded save or the host's snapshot after a
    /// desync: selections, standing orders and notifications start afresh.</summary>
    public void ReplaceSimulation(Simulation sim)
    {
        AbandonTurnWork();
        Sim = sim;
        Sim.DeferTurnWork = BackgroundTurns;
        _ordersTurn = sim.State.Turn;
        _gotos.Clear();
        _choices.Clear();
        _choicesKey = (-1, -1);
        ResetNotifications();
        SelectedCityId = null;
        SelectedUnitId = null;
        SelectNextUnit();
        Revision++;
        WorldRevision++;
        StartTurnWork(); // A save made while other players moved holds that work; it carries on from here.
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
