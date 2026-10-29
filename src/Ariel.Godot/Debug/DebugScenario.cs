using System.Text.Json;
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
/// Every tile ends up revealed and visible, so no fog dims the scene. Not part of the game; it only edits the session state.</summary>
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

    public static Hex? Apply(GameSession session, string file)
    {
        using var doc = JsonDocument.Parse(System.IO.File.ReadAllText(file), new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip });
        var state = session.Sim.State;
        var root = doc.RootElement;
        if (root.TryGetProperty("fill", out var fill))
            foreach (var tile in state.Grid.Tiles)
            {
                var plot = state.Plots[tile.Value];
                plot.Feature = null; plot.Route = null; plot.Improvement = null; plot.Bonus = null; plot.RiverEdges = 0; plot.IsLake = false;
                Edit(session, tile, fill);
            }
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
