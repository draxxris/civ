using System.Text.Json;
using Ariel.Game;
using Schema = Ariel.Content.Schema;

namespace Ariel.Client;

/// <summary>One capture in a <c>--shots</c> file. Every field except <see cref="Name"/> is optional; a missing camera field keeps
/// the previous shot's value, so a file can sweep one value.</summary>
internal sealed record ShotSpec
{
    public string Name { get; init; } = "shot";
    public string? Focus { get; init; }
    public int[]? FocusTile { get; init; }
    public float? Pitch { get; init; }
    public float? Yaw { get; init; }
    public float? Distance { get; init; }
    public string? TerrainDebug { get; init; }
    public bool? Wireframe { get; init; }

    public static List<ShotSpec> Load(string file) => JsonSerializer.Deserialize<List<ShotSpec>>(System.IO.File.ReadAllText(file),
        new JsonSerializerOptions { PropertyNameCaseInsensitive = true, ReadCommentHandling = JsonCommentHandling.Skip })
        ?? throw new ArgumentException($"{file} holds no shots.");
}

/// <summary>Finds the best patch of a kind of terrain to point the camera at: the matching tile with the most matching tiles
/// within two steps. Ties go to the lowest tile id, so the choice is stable for a seed.</summary>
internal static class DebugFocusFinder
{
    public static Hex? Find(GameSession session, string kind)
    {
        var state = session.Sim.State;
        switch (kind.ToLowerInvariant())
        {
            case "peak": return TerrainShowcase.Focus(session);
            case "river": return TerrainShowcase.RiverFocus(session);
            case "city":
                return state.Cities.Where(c => c.Owner == GameSession.Human).Select(c => (Hex?)c.Position).FirstOrDefault()
                    ?? session.Unit?.Position;
        }
        Func<Plot, bool> match = kind.ToLowerInvariant() switch
        {
            "hills" => p => p.Kind == PlotKind.Hills && p.Feature == null && !Water(session, p),
            "forest" => p => p.Feature?.EndsWith("forest", StringComparison.Ordinal) == true,
            "jungle" => p => p.Feature?.EndsWith("jungle", StringComparison.Ordinal) == true,
            "road" => p => p.Route != null,
            _ => throw new ArgumentException($"Unknown focus '{kind}'. Use hills, forest, jungle, road, peak, river or city."),
        };
        int best = -1, bestScore = 0;
        foreach (var tile in state.Grid.Tiles)
        {
            if (!match(state.Plots[tile.Value])) continue;
            int score = state.Grid.Range(tile, 2).Count(n => match(state.Plots[n.Value]));
            if (score > bestScore) { best = tile.Value; bestScore = score; }
        }
        return best < 0 ? null : state.Grid.HexOf(new TileId(best));
    }

    private static bool Water(GameSession session, Plot plot) => plot.IsLake || session.Sim.Content.Get<Schema.TerrainDef>(plot.Terrain).IsWater;
}
