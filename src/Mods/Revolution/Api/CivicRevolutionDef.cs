using Ariel.Content;
using Ariel.Content.Schema;

namespace Ariel.Mods.Revolution;

/// <summary>What a civic means to revolutions: elections, free speech and a communist state. Any mod may attach one to
/// a civic, its own or another's. Read with <c>ContentDb.Attachment&lt;CivicRevolutionDef&gt;(civic)</c>. Kept for the
/// election and ideology rules this mod does not implement yet; nothing reads them. A civic's instability is
/// <c>RevolutionRulesDef.CivicFactors</c>, hand-tuned beside the other factors.</summary>
public sealed class CivicRevolutionDef : ContentAttachment<CivicDef>
{
    public bool CanDoElection { get; init; }
    public bool FreeSpeech { get; init; }
    public bool Communism { get; init; }
}
