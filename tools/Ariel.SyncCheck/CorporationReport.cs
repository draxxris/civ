using System.Globalization;
using Ariel.Content;
using Ariel.Content.Schema;
using Ariel.Game;
using Ariel.Mods.Corporations;
using Ariel.Mods.Tests;

namespace Ariel.SyncCheck;

/// <summary>
/// Plays an all-AI game (or continues a save with <c>--load</c>) and reports whether the computer players ever found a
/// corporation or guild, and when, and why not. It prints the same options as <c>hash-trace</c> (<c>--seed</c>,
/// <c>--players</c>, <c>--turns</c>, <c>--world</c>, <c>--era</c>, <c>--optional</c>, <c>--load</c>, <c>--save</c>, and <c>--game-option ID=true|false</c> to set a game option) and
/// writes one block per game: every founding with its turn and founder, then for each AI player the corporation
/// technologies it knows, the civics that ban corporations, and for each founding building whose technology the player
/// has: in how many of its cities production could build it (and the first refusal when in none), and in how many it
/// is queued. Everything comes from the public rules and the game state, so it needs no hook in the AI.
/// </summary>
internal static class CorporationReport
{
    public static int Run(string[] args, ContentDb content)
    {
        ulong seed = ulong.Parse(HashTrace.Option(args, "--seed") ?? "1", CultureInfo.InvariantCulture);
        int players = int.Parse(HashTrace.Option(args, "--players") ?? "5", CultureInfo.InvariantCulture);
        int turns = int.Parse(HashTrace.Option(args, "--turns") ?? "100", CultureInfo.InvariantCulture);
        string world = HashTrace.Option(args, "--world") ?? "foundation:worldsize_small";
        string era = HashTrace.Option(args, "--era") ?? "foundation:era_ancient";
        string? save = HashTrace.Option(args, "--save"), load = HashTrace.Option(args, "--load");
        var options = new SortedDictionary<string, bool>(StringComparer.Ordinal);
        for (int i = 0; i < args.Length - 1; i++)
            if (args[i] == "--game-option" && args[i + 1].Split('=') is [var id, var value]) options[id] = bool.Parse(value);
        var sim = load is null ? GameFactory.CreateAi(content, seed, players, world, TurnMode.Simultaneous, era, options) : SaveGame.Load(content, File.ReadAllText(load), force: args.Contains("--force"));
        var buildings = content.Get<CorporationDef>().All.OrderBy(c => c.Id, StringComparer.Ordinal)
            .Select(c => (Corporation: c, Building: content.Get<BuildingDef>().All.FirstOrDefault(b => b.Founds is { Kind: "corporation" } f && f.Id == c.Id)))
            .Where(x => x.Building is not null).Select(x => (x.Corporation, Building: x.Building!)).ToArray();
        int startTurn = sim.State.Turn;
        Console.WriteLine(load is null ? $"game seed={seed} players={players} era={era} world={world} turns={turns}"
            : $"game load={load} from turn {startTurn} turns={turns}");
        Console.WriteLine($"  corporations in content: {buildings.Count(b => b.Corporation.Seat == "headquarters")} corporations, {buildings.Count(b => b.Corporation.Seat != "headquarters")} guilds");
        Console.WriteLine($"  at turn {startTurn}: founded {Founded(sim).Count}; techs for founding known by AI players: {Summary(sim, buildings, AiPlayers(sim))}");
        var firstSeen = new Dictionary<string, int>(StringComparer.Ordinal);
        var firstTech = new Dictionary<string, int>(StringComparer.Ordinal);
        for (int i = 0; i < turns; i++)
        {
            sim.AdvanceTurn();
            foreach (var (id, record) in Founded(sim)) firstSeen.TryAdd(id, record.Turn);
            foreach (var (corporation, building) in buildings)
                if (!firstTech.ContainsKey(corporation.Id) && AiPlayers(sim).Any(p => TechsKnown(sim, p, building))) firstTech[corporation.Id] = sim.State.Turn;
        }
        Console.WriteLine($"  final turn {sim.State.Turn}; first turn an AI player knew the technology of: "
            + (firstTech.Count == 0 ? "none" : string.Join(", ", firstTech.OrderBy(t => t.Value).Select(t => $"{content.Get<CorporationDef>(t.Key).Name} {t.Value}"))));
        var all = Founded(sim);
        Console.WriteLine($"  founded: {all.Count} of {buildings.Length} ({all.Count(f => IsGuild(content, f.Key))} guilds, {all.Count(f => !IsGuild(content, f.Key))} corporations)");
        foreach (var (id, record) in all.OrderBy(f => f.Value.Turn))
        {
            var city = sim.State.FindCity(record.Headquarters);
            Console.WriteLine($"    turn {record.Turn,4}  {content.Get<CorporationDef>(id).Name} by player {city?.Owner.ToString() ?? "?"} (city {record.Headquarters})");
        }
        Console.WriteLine("  " + Branches(sim));
        Console.WriteLine("  " + Bans(sim));
        foreach (int player in AiPlayers(sim)) Console.WriteLine(PlayerBlock(sim, player, buildings));
        if (save is not null) File.WriteAllText(save, SaveGame.Save(sim));
        return 0;
    }

    /// <summary>Every branch at the end, in all, and per corporation that has one.</summary>
    private static string Branches(Simulation sim)
    {
        var counts = new SortedDictionary<string, int>(StringComparer.Ordinal);
        foreach (var city in sim.State.Cities)
            foreach (var id in city.Extensions.Get<CorporationCityState>(sim.Content)?.Corporations ?? [])
                counts[id] = counts.GetValueOrDefault(id) + 1;
        return $"branches: {counts.Values.Sum()}" + (counts.Count == 0 ? ""
            : " (" + string.Join(", ", counts.Select(c => $"{sim.Content.Get<CorporationDef>(c.Key).Name} {c.Value}")) + ")");
    }

    /// <summary>How many AI players with cities run a civic that bans all corporations, or foreign ones.</summary>
    private static string Bans(Simulation sim)
    {
        var players = AiPlayers(sim).Where(p => sim.State.Cities.Any(c => c.Owner == p)).ToArray();
        bool Has(int p, Func<CivicCorporationsDef, bool> ban) => sim.State.PlayerById(p).Civics.Values
            .Any(c => sim.Content.Attachment<CivicCorporationsDef>(c) is { } d && ban(d));
        return $"AI players with cities: {players.Length}; banning all corporations: {players.Count(p => Has(p, d => d.NoCorporations))}; "
            + $"banning foreign: {players.Count(p => Has(p, d => d.NoForeignCorporations))}";
    }

    private static bool IsGuild(ContentDb content, string id) => content.Get<CorporationDef>(id).Seat != "headquarters";

    private static IReadOnlyDictionary<string, CorporationFounding> Founded(Simulation sim) =>
        sim.State.Extensions.Get<CorporationGameState>(sim.Content)?.Founded ?? new SortedDictionary<string, CorporationFounding>();

    private static IEnumerable<int> AiPlayers(Simulation sim) => sim.State.Players.Where(p => p.Ai is not null && PlayerRules.IsNation(sim, p.Id)).Select(p => p.Id);

    /// <summary>The technologies a founding building's requirements name.</summary>
    private static string[] TechLeaves(BuildingDef building) => building.Requirements is null ? []
        : ContentReferences.Leaves(building.Requirements).Where(l => l.Kind == "tech").Select(l => l.Id).Distinct(StringComparer.Ordinal).ToArray();

    private static bool TechsKnown(Simulation sim, int player, BuildingDef building) => TechLeaves(building).All(t => sim.KnowsTech(player, t));

    private static string Summary(Simulation sim, (CorporationDef Corporation, BuildingDef Building)[] buildings, IEnumerable<int> players)
    {
        var parts = players.Select(p => $"p{p}={buildings.Count(b => TechsKnown(sim, p, b.Building))}").ToArray();
        return parts.Length == 0 ? "no AI player" : string.Join(' ', parts) + " (corporations whose technology the player knows)";
    }

    private static string PlayerBlock(Simulation sim, int player, (CorporationDef Corporation, BuildingDef Building)[] buildings)
    {
        var content = sim.Content;
        var production = sim.Service<ICityProductionService>()!;
        var cities = sim.State.Cities.Where(c => c.Owner == player).ToList();
        var lines = new List<string>();
        var banned = sim.State.PlayerById(player).Civics.Values.Where(c => content.Attachment<CivicCorporationsDef>(c) is { } d && (d.NoCorporations || d.NoForeignCorporations))
            .Select(c => $"{c}{(content.Attachment<CivicCorporationsDef>(c)!.NoCorporations ? "(bans all)" : "(bans foreign)")}").ToArray();
        lines.Add($"  player {player}: {cities.Count} cities, civics banning corporations: {(banned.Length == 0 ? "none" : string.Join(", ", banned))}");
        var missing = buildings.Where(b => !TechsKnown(sim, player, b.Building)).ToArray();
        if (missing.Length > 0)
        {
            var techs = missing.SelectMany(b => TechLeaves(b.Building)).Where(t => !sim.KnowsTech(player, t)).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();
            lines.Add($"    technology unknown for {missing.Length} of {buildings.Length} corporations and guilds; {techs.Length} technologies missing: {string.Join(", ", techs.Take(12))}{(techs.Length > 12 ? ", ..." : "")}");
        }
        foreach (var (corporation, building) in buildings)
        {
            if (!TechsKnown(sim, player, building)) continue;
            int available = 0, queued = 0;
            var refusals = new SortedDictionary<string, int>(StringComparer.Ordinal);
            foreach (var city in cities)
            {
                var result = production.Validate(sim, city.Id, "cities:building", building.Id);
                if (result.Success) available++;
                else foreach (var failure in result.Failures) refusals[failure.Explanation] = refusals.GetValueOrDefault(failure.Explanation) + 1;
                if (city.ProductionQueue.Any(o => o.Kind == "cities:building" && o.ContentId == building.Id)) queued++;
            }
            string classes = string.Join(", ", ContentReferences.Leaves(building.Requirements ?? new Requirement.All([])).Where(l => l.Kind == "building-class")
                .Select(l => $"{l.Count ?? 1} {l.Id} needed, {cities.Sum(c => c.Buildings.Count(b => content.Get<BuildingDef>(b).DefaultBuilding == l.Id))} held"));
            string founded = Founded(sim).TryGetValue(corporation.Id, out var f) ? $", founded turn {f.Turn}" : "";
            lines.Add($"    {corporation.Name} ({corporation.Seat}, cost {building.Cost}): buildable in {available}/{cities.Count} cities, queued in {queued}"
                + (refusals.Count > 0 ? $", refused: {string.Join("; ", refusals.Select(r => $"{r.Key} x{r.Value}"))}" : "") + (classes.Length > 0 ? $" [{classes}]" : "") + founded);
        }
        return string.Join('\n', lines);
    }
}
