namespace Ariel.Content.Schema;

/// <summary>A kind of act one player can remember another for. The base game and any mod register kinds and record
/// them through <see cref="Ariel.Game.IRelationsService.Remember"/>; memories fade on the base game's schedule, and
/// whoever turns them into an opinion (the Diplomacy mod's attitude) reads the weights.</summary>
public sealed class MemoryKindDef : ContentDef
{
    /// <summary>The name leader tables (<see cref="LeaderDef.MemoryDecays"/>, <see cref="LeaderDef.MemoryAttitudePercents"/>)
    /// use for this kind. Empty means the id's local part (<c>relations:declared_war</c> is <c>declared_war</c>).</summary>
    public string Key { get; init; } = "";
    /// <summary>Each turn a remembered kind loses one count with a chance of one in this many turns, scaled by the game
    /// speed's pace, unless the remembering leader's <see cref="LeaderDef.MemoryDecays"/> names its own.</summary>
    public int Decay { get; init; } = 60;
    /// <summary>Most counts one player keeps of this kind about another; further acts add nothing until one fades.
    /// Zero means no limit.</summary>
    public int Limit { get; init; }
    /// <summary>Attitude per count, in percent, for a leader whose <see cref="LeaderDef.MemoryAttitudePercents"/> does not
    /// name the kind.</summary>
    public int AttitudePercent { get; init; }

    /// <summary>The leader-table name: <see cref="Key"/>, or the id without its namespace.</summary>
    public string LeaderKey => Key.Length > 0 ? Key : Id[(Id.IndexOf(':') + 1)..];
}
