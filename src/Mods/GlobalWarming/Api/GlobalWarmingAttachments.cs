using Ariel.Content;
using Ariel.Content.Schema;

namespace Ariel.Mods.GlobalWarming;

/// <summary>What a map feature means to global warming, kept in this mod's own rows (see "Content attachments" in
/// docs/DESIGN.md). A feature without a row defends nothing: read it as <see cref="None"/>.</summary>
public sealed class FeatureGlobalWarmingDef : ContentAttachment<FeatureDef>
{
    /// <summary>The row of a feature nothing attached to.</summary>
    public static readonly FeatureGlobalWarmingDef None = new();
    /// <summary>The feature's weight in the world's defence against warming; zero or less adds nothing.</summary>
    public int WarmingDefense { get; init; }
}
