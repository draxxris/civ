using Ariel.Game;

namespace Ariel.Client;

/// <summary>What the setup screen collects. The first player is the human; the rest are opponents.</summary>
public sealed record NewGameOptions(
    string WorldSize, string GameSpeed, string Handicap, string Leader, int Opponents, ulong Seed,
    string MapGenerator = "", string StartingEra = "", IReadOnlyDictionary<string, bool>? GameOptions = null,
    string Civilization = "", MapSpec? MapOverride = null)
{
    /// <summary>Settings for a catalog: the human leader first, then distinct leaders of other civilizations picked from the seed.</summary>
    public SimulationSettings ToSettings(GameCatalog catalog)
    {
        var leaders = catalog.PickLeaders(Leader, Opponents, Seed).ToList();
        // A leader may lead more than one civilization, so the civilization the player picked decides, not the
        // catalog's first entry for that leader.
        if (Civilization.Length > 0)
            leaders[0] = catalog.Leaders.FirstOrDefault(choice => choice.Id == Leader && choice.CivilizationId == Civilization)
                ?? throw new ArgumentException($"Leader '{Leader}' does not lead civilization '{Civilization}'.");
        var gameOptions = new SortedDictionary<string, bool>(StringComparer.Ordinal);
        foreach (var option in catalog.GameOptions) gameOptions[option.Id] = option.Default;
        if (GameOptions is not null)
            foreach (var option in GameOptions) gameOptions[option.Key] = option.Value;
        return new SimulationSettings
        {
            Seed = Seed, WorldSize = WorldSize, MapGenerator = MapGenerator, GameSpeed = GameSpeed, Handicap = Handicap,
            StartingEra = StartingEra, PlayerCount = leaders.Count, Leaders = leaders.Select(l => l.Id).ToArray(),
            Civilizations = leaders.Select(l => l.CivilizationId).ToArray(),
            PlayerAis = leaders.Select((_, i) => i == GameSession.Human ? null : catalog.OpponentAi).ToArray(),
            GameOptions = gameOptions, MapOverride = MapOverride,
        }.Resolve(catalog.Content);
    }
}
