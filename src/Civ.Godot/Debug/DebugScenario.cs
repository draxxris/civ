using System.Text.Json;
using Civ.Game;
using Schema = Civ.Content.Schema;

namespace Civ.Client;

/// <summary>A hand-made, repeatable scene that overwrites the generated map for captures. JSON:
/// <code>
/// { "fill":  { "terrain": "grass", "kind": "Flat" },
///   "tiles": [ { "at": [5, 5], "kind": "Hills" },
///              { "rect": [3, 3, 7, 5], "terrain": "plains", "feature": "forest" } ],
///   "roads": [ [[2, 2], [3, 2], [4, 3]] ],
///   "focus": [5, 5] }
/// </code>
/// Tile fields (all optional): terrain, kind (Flat|Hills|Peak), feature, improvement, bonus, route, river (edge bit mask), lake, owner.
/// Empty strings clear feature, improvement, bonus or route. Improvement, bonus and route accept full ids or short names such as "cottage", "copper" and "road". Positions are (column, row).
/// Every tile ends up revealed and visible, so no fog dims the scene. Not part of the game; it only edits the session state.</summary>
internal static class DebugScenario
{
    public static Hex? Apply(GameSession session, string file)
    {
        using var doc = JsonDocument.Parse(System.IO.File.ReadAllText(file), new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip });
        var state = session.Sim.State;
        var root = doc.RootElement;
        if (root.TryGetProperty("fill", out var fill))
            foreach (var plot in state.Plots)
            {
                plot.Feature = null; plot.Route = null; plot.Improvement = null; plot.Bonus = null; plot.RiverEdges = 0; plot.IsLake = false;
                Edit(session, plot, fill);
            }
        if (root.TryGetProperty("tiles", out var tiles))
            foreach (var entry in tiles.EnumerateArray())
                foreach (var tile in Targets(session, entry)) Edit(session, state.Plots[tile.Value], entry);
        if (root.TryGetProperty("roads", out var roads))
        {
            string route = RouteId(session, "road");
            foreach (var path in roads.EnumerateArray())
                foreach (var point in path.EnumerateArray())
                    state.Plots[state.Grid.TileAt(point[0].GetInt32(), point[1].GetInt32()).Value].Route = route;
        }
        foreach (var plot in state.Plots) { plot.Revealed.Add(session.Me.TeamId); plot.Visible.Add(session.Me.TeamId); }
        return root.TryGetProperty("focus", out var f) ? state.Grid.HexOf(state.Grid.TileAt(f[0].GetInt32(), f[1].GetInt32())) : null;
    }

    private static IEnumerable<TileId> Targets(GameSession session, JsonElement entry)
    {
        var grid = session.Sim.State.Grid;
        if (entry.TryGetProperty("at", out var at)) yield return grid.TileAt(at[0].GetInt32(), at[1].GetInt32());
        if (!entry.TryGetProperty("rect", out var r)) yield break;
        for (int row = r[1].GetInt32(); row <= r[3].GetInt32(); row++)
            for (int col = r[0].GetInt32(); col <= r[2].GetInt32(); col++)
                if (grid.TryTile(col, row, out var id)) yield return id;
    }

    private static void Edit(GameSession session, Plot plot, JsonElement e)
    {
        if (e.TryGetProperty("terrain", out var terrain))
        {
            // Fail now, with the id, rather than later in the renderer.
            session.Sim.Content.Get<Schema.TerrainDef>(terrain.GetString()!);
            plot.Terrain = terrain.GetString()!;
        }
        if (e.TryGetProperty("kind", out var kind)) plot.Kind = Enum.Parse<PlotKind>(kind.GetString()!, ignoreCase: true);
        if (e.TryGetProperty("feature", out var feature)) plot.Feature = Empty(feature.GetString());
        if (e.TryGetProperty("improvement", out var improvement)) plot.Improvement = ContentId<Schema.ImprovementDef>(session, Empty(improvement.GetString()), "improvement");
        if (e.TryGetProperty("bonus", out var bonus)) plot.Bonus = ContentId<Schema.BonusDef>(session, Empty(bonus.GetString()), "bonus");
        if (e.TryGetProperty("route", out var route)) plot.Route = Empty(route.GetString()) is { } id ? RouteId(session, id) : null;
        if (e.TryGetProperty("river", out var river)) plot.RiverEdges = (byte)river.GetInt32();
        if (e.TryGetProperty("lake", out var lake)) plot.IsLake = lake.GetBoolean();
        if (e.TryGetProperty("owner", out var owner)) plot.Owner = owner.GetInt32();
    }
    private static string? Empty(string? s) => string.IsNullOrEmpty(s) ? null : s;

    private static string? ContentId<T>(GameSession session, string? name, string kind) where T : Civ.Content.ContentDef
    {
        if (name is null) return null;
        if (name.Contains(':')) return session.Sim.Content.Get<T>(name).Id;
        var definitions = session.Sim.Content.Get<T>().All;
        var exact = definitions.Where(def =>
        {
            var idName = def.Id[(def.Id.LastIndexOf(':') + 1)..];
            while (idName.StartsWith(kind + "_", StringComparison.Ordinal))
                idName = idName[(kind.Length + 1)..];
            return idName == name;
        }).ToArray();
        if (exact.Length > 0) return Unique(exact, kind, name);
        var suffix = definitions.Where(def => def.Id.EndsWith("_" + name, StringComparison.Ordinal)
                || def.Id.EndsWith(":" + name, StringComparison.Ordinal)).ToArray();
        return suffix.Length > 0 ? Unique(suffix, kind, name)
            : throw new ArgumentException($"No {kind} match for name '{name}'.");
    }

    private static string Unique<T>(T[] matches, string kind, string name) where T : Civ.Content.ContentDef
    {
        if (matches.Length == 1) return matches[0].Id;
        var candidates = matches.Take(10).Select(def => def.Id);
        var more = matches.Length > 10 ? ", ..." : "";
        throw new ArgumentException($"Ambiguous {kind} name '{name}'. Candidates: {string.Join(", ", candidates)}{more}.");
    }

    private static string RouteId(GameSession session, string name)
    {
        var routes = session.Sim.Content.Get<Schema.RouteDef>().All.Where(r => !r.SeaTunnel).ToArray();
        var match = routes.FirstOrDefault(r => r.Id == name)
            ?? routes.OrderBy(r => r.Value).FirstOrDefault(r => r.Id.EndsWith("_" + name, StringComparison.Ordinal) || r.Id.EndsWith(":" + name, StringComparison.Ordinal))
            ?? throw new ArgumentException($"No route '{name}'. Have: {string.Join(", ", routes.Select(r => r.Id))}.");
        return match.Id;
    }
}
