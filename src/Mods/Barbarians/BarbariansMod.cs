using System.Text.Json;
using System.Text.Json.Nodes;
using Ariel.Game;
using Ariel.Modding;

namespace Ariel.Mods.Barbarians;

/// <summary>The barbarians: the hostile player, created when the first barbarian appears; land units spawned in
/// unclaimed land and ships off unclaimed coasts; and how every barbarian unit moves and attacks. The base game keeps
/// the hostile-faction flag (<see cref="MilitaryGameState.BarbarianPlayer"/>) and every rule that asks it, and reaches
/// this mod through <see cref="IHostileFaction"/> for hostile villagers and rebels; movement asks the base game's
/// <see cref="IMovementBarrier"/>s, not this mod, what bars a barbarian.</summary>
public sealed class BarbariansMod : IPreparableMod
{
    private static readonly ModJsonFile<BarbarianRulesDef> Rules = new("data/barbarian-rules.json");
    /// <summary>What handicaps, game speeds and eras mean to barbarian spawning, attached to the base rows.</summary>
    private static readonly ModJsonFile<JsonArray> Handicaps = new("data/handicap-barbarians.json");
    private static readonly ModJsonFile<GameSpeedBarbariansDef[]> Speeds = new("data/game-speed-barbarians.json");
    private static readonly ModJsonFile<EraBarbariansDef[]> Eras = new("data/era-barbarians.json");
    public IReadOnlyList<ModJsonFile> PreparationFiles => [Rules, Handicaps, Speeds, Eras];

    public void Register(IModContext context)
    {
        var rules = Rules.Read(context.Data);
        if (rules.UnownedTilesMultiplier < 1) throw new InvalidDataException("Invalid hex barbarian tuning");
        context.Content<BarbarianRulesDef>().Register(rules);
        LoadHexHandicaps(context, rules);
        Ariel.Rules.ContentRegistration.Load(context, Speeds);
        Ariel.Rules.ContentRegistration.Load(context, Eras);
        context.RegisterService<IHostileFaction>("faction", new BarbarianFaction());
        context.TurnSystems.Register(new BarbarianSpawns());
        context.TurnSystems.Register(new BarbarianBehavior());
    }

    /// <summary>Adapts the loaded rows, not the imported sidecar: spawning reads the effective hex value.</summary>
    private static void LoadHexHandicaps(IModContext context, BarbarianRulesDef rules)
    {
        foreach (var row in Handicaps.Read(context.Data))
        {
            if (row!["unownedTilesPerBarbarianUnit"] is { } tiles)
                row["unownedTilesPerBarbarianUnit"] = checked(tiles.GetValue<int>() * rules.UnownedTilesMultiplier);
            context.Content<HandicapBarbariansDef>().Register(row.Deserialize<HandicapBarbariansDef>(ModLoader.Json)!);
        }
    }
}
