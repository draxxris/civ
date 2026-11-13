using Ariel.Content;
using Ariel.Content.Schema;
using Ariel.Game;
using Ariel.Modding;
using Ariel.Rules;

namespace Ariel.TestMods.CivicExtension;

/// <summary>Extends the base civics with data alone: a civic in the existing government option, a whole new option
/// with its own civics, and a modifier key of its own. Tribunes carries a base key (city happiness) the base rules
/// already read and this mod's levy key, which only this mod's levy rule reads.</summary>
public sealed class CivicExtensionMod : IPreparableMod
{
    public const string LevyKey = "civic_extension:levy";
    private static readonly ModJsonFile<ModifierKeyDef[]> Keys = new("data/modifier-key.json");
    private static readonly ModJsonFile<CivicOptionDef[]> Options = new("data/civic-option.json");
    private static readonly ModJsonFile<CivicDef[]> Civics = new("data/civic.json");
    public IReadOnlyList<ModJsonFile> PreparationFiles => [Keys, Options, Civics];

    public void Register(IModContext context)
    {
        ContentRegistration.Load(context, Keys);
        ContentRegistration.Load(context, Options);
        ContentRegistration.Load(context, Civics);
        context.RegisterStateComponent<LevyLog>("levy_log");
        context.TurnSystems.Register(new LevyRule());
    }

    /// <summary>Each turn, pays every player the levy its player-scope modifiers total, whichever content gave it.</summary>
    private sealed class LevyRule : IPlayerTurnSystem
    {
        public string Id => "civic_extension:levy";
        public int Order => TurnPhase.PlayerEnd;

        public void Run(Simulation sim, int playerId)
        {
            var player = sim.State.PlayerById(playerId);
            long levy = ModifierSink.Collect(new(sim, ModifierScope.Player, playerId)).Ledger.Total(LevyKey);
            if (levy == 0) return;
            player.Treasury += levy;
            var log = player.Extensions.Get<LevyLog>(sim.Content) ?? new LevyLog();
            log.Collected += levy;
            player.Extensions.Set(sim.Content, log);
        }
    }
}

/// <summary>Gold the levy has paid a player.</summary>
public sealed class LevyLog
{
    public long Collected { get; set; }
}
