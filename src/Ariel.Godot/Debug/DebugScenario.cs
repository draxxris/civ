using System.Text.Json;
using System.Text.Json.Nodes;
using Ariel.Game;
using Schema = Ariel.Content.Schema;

namespace Ariel.Client;

/// <summary>A hand-made, repeatable scene that overwrites the generated map for captures. JSON:
/// <code>
/// { "fill":  { "terrain": "grass", "kind": "Flat" },
///   "tiles": [ { "at": [5, 5], "kind": "Hills" },
///              { "rect": [3, 3, 7, 5], "terrain": "plains", "feature": "forest" } ],
///   "roads": [ [[2, 2], [3, 2], [4, 3]],
///               { "route": "railroad", "path": [[2, 7], [3, 7]] },
///               { "routes": ["road", "railroad"], "path": [[4, 7], [5, 7]] } ],
///   "focus": [5, 5] }
/// </code>
/// Road entries can be point arrays (default road), or objects with a path and optional route; routes assigns one route name per path point.
/// Route names accept full ids or short names. Tile fields (all optional): terrain, kind (Flat|Hills|Peak), feature, improvement, bonus, route, river (edge bit mask), cliff (true, false or a sea-edge bit mask), lake, owner (null clears it), city.
/// City example: "city": {"owner":0,"name":"Testburg","population":5,"buildings":["walls"]}.
/// Optional "production": {"kind":"mod:kind","id":"mod:item","progress":20} queues one order, and "food" sets the stored food.
/// City requires owner and name; population defaults to 1 and buildings to empty. It clears the plot improvement and sets its owner.
/// Optional "era" (full id or short name such as "modern") sets the owner's current era, which picks the look of the city's
/// buildings. The era belongs to the owner, so cities of different eras need different owners (the last one set wins).
/// The 3D buildings come from <see cref="Ariel.Client.Terrain.CityLayout"/>.
/// Unknown building ids fail with the id in the error. Existing cities on the same tile fail rather than overlap.
/// Empty strings clear feature, improvement, bonus or route. Improvement, bonus and route accept full ids or short names such as "cottage", "copper" and "road". Positions are (column, row).
/// "clearUnits": true removes every generated unit first. "units" places units:
/// <c>[{"at":[5,5],"type":"warrior","owner":1,"health":40,"count":2}]</c>. Type accepts a full id or short name; owner
/// defaults to 0, health to 100 and count to 1. Units are placed after tiles and cities.
/// "revealResources": true gives the local team every resource's reveal technology, so every resource on the map shows.
/// Every tile ends up revealed and visible, so no fog dims the scene. Not part of the game; it only edits the session state.
/// A run without <c>--world</c> sizes the map to the scene (see <see cref="Frame"/>), clears every tile to flat ground
/// first and shifts the scene's coordinates to fit it; a run with <c>--world</c> lays the scene over the generated map.</summary>
internal static class DebugScenario
{
    /// <summary>"cliff": true puts a cliff on every edge of a land tile, false clears them, and a number is an edge bit mask
    /// (Hex.Directions order) of which edges get one, lifted on the named tile. Edges to lakes never get a cliff.</summary>
    private static void SetCliffs(GameSession session, TileId tile, JsonElement cliff)
    {
        var state = session.Sim.State;
        int mask = cliff.ValueKind switch { JsonValueKind.True => 63, JsonValueKind.False => 0, _ => cliff.GetInt32() };
        bool Water(TileId t) => session.Sim.Content.Get<Schema.TerrainDef>(state.Plots[t.Value].Terrain).IsWater;
        if (Water(tile)) return;
        for (int d = 0; d < 6; d++)
            if (state.Grid.Neighbor(tile, d) is { } other && !(Water(other) && state.Plots[other.Value].IsLake))
                state.SetCliff(tile, d, (mask & (1 << d)) != 0);
    }

    /// <summary>A scene laid out in a world of its own: the file with every position shifted so the scene starts
    /// <see cref="SceneMargin"/> tiles from the map edge, and the map size that leaves.</summary>
    internal sealed record SceneWorld(string Json, int Width, int Height, int ShiftCol, int ShiftRow);

    /// <summary>Tiles kept around the scene, so the camera can look along its edge without leaving the map.</summary>
    private const int SceneMargin = 2;

    /// <summary>Ground a scene gets when it names no <c>fill</c> of its own.</summary>
    private const string BareFill = """{"terrain":"world:grass","kind":"Flat","owner":null}""";

    /// <summary>The world a scene file asks for, or null when it names no position. Every pair of numbers in the file counts
    /// as a column and row — tile <c>at</c> lists, <c>focus</c>, unit positions and road points — and none of them are
    /// shifted by the frame itself, so the JSON still reads the way it was written. Rects are left alone: a scene uses them
    /// to clear a whole map, not to say where it is, so a 200 × 200 rect must not stretch the world to 200 tiles.</summary>
    public static SceneWorld? Frame(string file)
    {
        var root = JsonNode.Parse(System.IO.File.ReadAllText(file), documentOptions: new JsonDocumentOptions
            { CommentHandling = JsonCommentHandling.Skip });
        if (root is null) return null;
        int col0 = int.MaxValue, row0 = int.MaxValue, col1 = int.MinValue, row1 = int.MinValue;
        Walk(root, (c, r) =>
        {
            col0 = Math.Min(col0, c); row0 = Math.Min(row0, r);
            col1 = Math.Max(col1, c); row1 = Math.Max(row1, r);
        });
        if (col0 > col1) return null;
        int dc = SceneMargin - col0, dr = SceneMargin - row0;
        Shift(root, dc, dr);
        int width = Math.Max(8, col1 - col0 + 1 + 2 * SceneMargin), height = Math.Max(6, row1 - row0 + 1 + 2 * SceneMargin);
        return new(root.ToJsonString(), width, height, dc, dr);
    }

    /// <summary>Call <paramref name="visit"/> with the column and row of every two-number array in the document.</summary>
    private static void Walk(JsonNode node, Action<int, int> visit)
    {
        if (Point(node) is var (c, r) && c is not null) { visit(c.Value, r!.Value); return; }
        if (node is JsonArray array)
        {
            foreach (var child in array)
                if (child is not null) Walk(child, visit);
        }
        else if (node is JsonObject obj)
        {
            foreach (var pair in obj)
                if (pair.Value is not null) Walk(pair.Value, visit);
        }
    }

    /// <summary>Shift every two-number array in the document by the given column and row offset.</summary>
    private static void Shift(JsonNode node, int dc, int dr)
    {
        if (Point(node) is var (c, r) && c is not null)
        {
            node.AsArray()[0] = c.Value + dc;
            node.AsArray()[1] = r!.Value + dr;
            return;
        }
        if (node is JsonArray array)
        {
            foreach (var child in array)
                if (child is not null) Shift(child, dc, dr);
        }
        else if (node is JsonObject obj)
        {
            foreach (var pair in obj)
                if (pair.Value is not null) Shift(pair.Value, dc, dr);
        }
    }

    /// <summary>The column and row an array holds when it is exactly two whole numbers, else (null, null).</summary>
    private static (int? Col, int? Row) Point(JsonNode node) =>
        node is JsonArray { Count: 2 } array && array[0] is JsonValue c && array[1] is JsonValue r
            && c.TryGetValue<int>(out int col) && r.TryGetValue<int>(out int row)
            ? (col, row) : (null, null);

    /// <summary>Lay the scene on the session. With <paramref name="sceneWorld"/> the map was sized to the scene, so the
    /// generated world is cleared first and the scene's own coordinates are shifted to match.</summary>
    public static Hex? Apply(GameSession session, string file, bool sceneWorld = false)
    {
        var text = System.IO.File.ReadAllText(file);
        if (sceneWorld) text = Frame(file)?.Json ?? text;
        using var doc = JsonDocument.Parse(text, new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip });
        using var bare = sceneWorld ? JsonDocument.Parse(BareFill) : null;
        var state = session.Sim.State;
        var root = doc.RootElement;
        JsonElement? ground = root.TryGetProperty("fill", out var declared) ? declared : sceneWorld ? bare!.RootElement : null;
        if (ground is { } fill)
            foreach (var tile in state.Grid.Tiles)
            {
                var plot = state.Plots[tile.Value];
                plot.Feature = null; plot.Route = null; plot.Improvement = null; plot.Bonus = null; plot.RiverEdges = 0; plot.IsLake = false;
                if (sceneWorld)
                {
                    // The map is the scene, so nothing the generator made may show through it.
                    plot.CliffEdges = 0; plot.CliffLiftEdges = 0; plot.Owner = null;
                }
                Edit(session, tile, fill);
            }
        if (sceneWorld)
            foreach (var unit in state.Units.ToList()) state.RemoveUnit(unit.Id);
        if (root.TryGetProperty("tiles", out var tiles))
        {
            foreach (var entry in tiles.EnumerateArray())
                foreach (var tile in Targets(session, entry)) Edit(session, tile, entry);
            // Cliffs need the final terrain on both sides of an edge, so they go on after every tile is set.
            foreach (var entry in tiles.EnumerateArray())
                if (entry.TryGetProperty("cliff", out var cliff))
                    foreach (var tile in Targets(session, entry)) SetCliffs(session, tile, cliff);
        }
        if (root.TryGetProperty("roads", out var roads))
        {
            foreach (var entry in roads.EnumerateArray())
            {
                var path = entry.ValueKind == JsonValueKind.Array ? entry : entry.GetProperty("path");
                JsonElement routeList = default;
                var perPointRoutes = entry.ValueKind == JsonValueKind.Object && entry.TryGetProperty("routes", out routeList);
                if (perPointRoutes && entry.TryGetProperty("route", out _))
                    throw new ArgumentException("A roads entry cannot contain both 'route' and 'routes'.");
                if (perPointRoutes && routeList.GetArrayLength() != path.GetArrayLength())
                    throw new ArgumentException("A roads entry's 'routes' length must match its 'path' length.");

                var defaultRoute = entry.ValueKind == JsonValueKind.Object && entry.TryGetProperty("route", out var route)
                    ? RouteId(session, route.GetString()!)
                    : RouteId(session, "road");
                int index = 0;
                foreach (var point in path.EnumerateArray())
                {
                    var routeId = perPointRoutes ? RouteId(session, routeList[index++].GetString()!) : defaultRoute;
                    state.Plots[state.Grid.TileAt(point[0].GetInt32(), point[1].GetInt32()).Value].Route = routeId;
                }
            }
        }
        if (root.TryGetProperty("clearUnits", out var clear) && clear.GetBoolean())
        {
            foreach (var unit in state.Units.ToList()) state.RemoveUnit(unit.Id);
            session.ClearSelection();
        }
        if (root.TryGetProperty("units", out var units))
            foreach (var entry in units.EnumerateArray()) AddUnits(session, entry);
        if (root.TryGetProperty("revealResources", out var reveal) && reveal.GetBoolean())
            foreach (var bonus in session.Sim.Content.Get<Schema.BonusDef>().All)
                if (bonus.RevealTech is { } tech) session.MyTeam.Techs.Add(tech);
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

    private static void Edit(GameSession session, TileId tile, JsonElement e)
    {
        var plot = session.Sim.State.Plots[tile.Value];
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
        if (e.TryGetProperty("owner", out var owner)) plot.Owner = owner.ValueKind == JsonValueKind.Null ? null : owner.GetInt32();
        if (e.TryGetProperty("city", out var city)) AddCity(session, tile, city);
    }

    private static void AddCity(GameSession session, TileId tile, JsonElement e)
    {
        var state = session.Sim.State;
        int owner = e.GetProperty("owner").GetInt32();
        state.PlayerById(owner);
        var buildings = e.TryGetProperty("buildings", out var list)
            ? list.EnumerateArray().Select(b => ContentId<Schema.BuildingDef>(session, b.GetString(), "building")
                ?? throw new ArgumentException("City building id must not be empty.")).ToList()
            : new List<string>();
        var queue = new List<ProductionOrder>();
        if (e.TryGetProperty("production", out var order))
            queue.Add(new ProductionOrder(order.GetProperty("kind").GetString()!, order.GetProperty("id").GetString()!,
                order.TryGetProperty("progress", out var progress) ? progress.GetInt64() : 0));
        state.AddCity(new City
        {
            ProductionQueue = queue, FoodStored = e.TryGetProperty("food", out var food) ? food.GetInt64() : 0,
            Id = state.NextCityId++, Owner = owner, Name = e.GetProperty("name").GetString()!,
            Position = state.Grid.HexOf(tile),
            Population = e.TryGetProperty("population", out var population) ? population.GetInt32() : 1,
            Buildings = buildings,
        });
        state.Plots[tile.Value].Improvement = null;
        state.Plots[tile.Value].Owner = owner;
        if (e.TryGetProperty("era", out var era)) SetEra(session, owner, era.GetString());
    }

    private static void AddUnits(GameSession session, JsonElement e)
    {
        var state = session.Sim.State;
        var at = e.GetProperty("at");
        var position = state.Grid.HexOf(state.Grid.TileAt(at[0].GetInt32(), at[1].GetInt32()));
        var type = ContentId<Schema.UnitDef>(session, e.GetProperty("type").GetString(), "unit")
            ?? throw new ArgumentException("Unit type must not be empty.");
        int owner = e.TryGetProperty("owner", out var o) ? o.GetInt32() : 0;
        state.PlayerById(owner);
        int health = e.TryGetProperty("health", out var h) ? h.GetInt32() : 100;
        int count = e.TryGetProperty("count", out var c) ? c.GetInt32() : 1;
        for (int i = 0; i < count; i++)
            state.AddUnit(new Unit { Id = state.NextUnitId++, Owner = owner, Type = type, Position = position, Health = health });
    }

    /// <summary>Set the owner's current era, which picks the look of all its cities.</summary>
    private static void SetEra(GameSession session, int owner, string? name)
    {
        var content = session.Sim.Content;
        var id = ContentId<Schema.EraDef>(session, name, "era") ?? throw new ArgumentException("City era must not be empty.");
        var player = session.Sim.State.PlayerById(owner);
        var current = player.Extensions.Get<CityPlayerState>(content) ?? new CityPlayerState();
        current.CurrentEra = id;
        player.Extensions.Set(content, current);
    }

    private static string? Empty(string? s) => string.IsNullOrEmpty(s) ? null : s;
    private static string? ContentId<T>(GameSession session, string? name, string kind) where T : Ariel.Content.ContentDef
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

    private static string Unique<T>(T[] matches, string kind, string name) where T : Ariel.Content.ContentDef
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
