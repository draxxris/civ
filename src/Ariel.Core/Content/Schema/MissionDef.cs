namespace Ariel.Content.Schema;

/// <summary>Unit action identity and interface flags. No mission execution is registered yet.</summary>
public sealed class MissionDef : GameContentDef
{
    public int Time { get; init; }
    public bool Target { get; init; }
    public bool Build { get; init; }
    public bool Visible { get; init; }
}
