using Ariel.Game;

namespace Ariel.Mods.Events;

/// <summary>Saved game-wide event state. No derived data is stored.</summary>
public sealed class EventState
{
    public int NextEventId { get; set; } = 1;
    public List<PendingEvent> Pending { get; set; } = [];
    /// <summary>Last turn an event happened, keyed "player:trigger".</summary>
    public SortedDictionary<string, int> LastFired { get; set; } = new(StringComparer.Ordinal);
    public List<ActiveEventEffect> Effects { get; set; } = [];
}

/// <summary>A kept happiness, health or plot yield change. ExpiresTurn zero means permanent.</summary>
public sealed class ActiveEventEffect
{
    public int Player { get; set; }
    public int City { get; set; } = -1;
    public int Plot { get; set; } = -1;
    public string Option { get; set; } = "";
    public int ExpiresTurn { get; set; }
    public int Happiness { get; set; }
    public int Health { get; set; }
    public int PlotFood { get; set; }
    public int PlotProduction { get; set; }
    public int PlotCommerce { get; set; }
}
