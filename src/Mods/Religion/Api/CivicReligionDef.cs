using Ariel.Content;
using Ariel.Content.Schema;
using Ariel.Game;

namespace Ariel.Mods.Religion;

/// <summary>What a civic means to religion. Any mod may attach one to a civic, its own or another's. Read with
/// <c>ContentDb.Attachment&lt;CivicReligionDef&gt;(civic)</c>; a civic without a row has none of these effects.</summary>
public sealed class CivicReligionDef : ContentAttachment<CivicDef>, IDescribedContent
{
    /// <summary>The civic lets a player hold a state religion; without such a civic the last choice is suppressed.</summary>
    public bool RequiresStateReligion { get; init; }
    /// <summary>Inquisitors may purge non-state religions, unless another held civic disallows them.</summary>
    public bool AllowInquisitions { get; init; }
    public bool DisallowInquisitions { get; init; }
    /// <summary>Religious buildings need no matching state religion.</summary>
    public bool FreeReligion { get; init; }
    /// <summary>Only the state religion spreads into the player's cities.</summary>
    public bool NoNonStateReligionSpread { get; init; }
    /// <summary>Percent added to natural religion spread in the player's cities.</summary>
    public int ReligionSpreadRate { get; init; }
    /// <summary>Happiness in a city per non-state religion present.</summary>
    public int NonStateReligionHappiness { get; init; }
    /// <summary>Effects in cities that follow the state religion.</summary>
    public int StateReligionHappiness { get; init; }
    public int StateReligionHealth { get; init; }
    public int StateReligionBuildingProductionModifier { get; init; }
    public int StateReligionUnitProductionModifier { get; init; }
    public int StateReligionGreatPeopleRateModifier { get; init; }
    /// <summary>Experience for units trained in a city that follows the state religion.</summary>
    public int StateReligionFreeExperience { get; init; }

    public IEnumerable<string> Describe(ContentDb content)
    {
        if (RequiresStateReligion) yield return "Requires a state religion";
        if (AllowInquisitions) yield return "Allows inquisitions";
        if (DisallowInquisitions) yield return "Forbids inquisitions";
        if (FreeReligion) yield return "Religious buildings need no matching state religion";
        if (NoNonStateReligionSpread) yield return "Only the state religion spreads";
        if (ReligionSpreadRate != 0) yield return $"{ReligionSpreadRate:+#;-#}% religion spread";
        if (NonStateReligionHappiness != 0) yield return $"{NonStateReligionHappiness:+#;-#} happiness per non-state religion";
        if (StateReligionHappiness != 0) yield return $"{StateReligionHappiness:+#;-#} happiness in state religion cities";
        if (StateReligionHealth != 0) yield return $"{StateReligionHealth:+#;-#} health in state religion cities";
        if (StateReligionBuildingProductionModifier != 0)
            yield return $"{StateReligionBuildingProductionModifier:+#;-#}% building production in state religion cities";
        if (StateReligionUnitProductionModifier != 0)
            yield return $"{StateReligionUnitProductionModifier:+#;-#}% unit production in state religion cities";
        if (StateReligionGreatPeopleRateModifier != 0)
            yield return $"{StateReligionGreatPeopleRateModifier:+#;-#}% great people in state religion cities";
        if (StateReligionFreeExperience != 0) yield return $"{StateReligionFreeExperience:+#;-#} experience for units from state religion cities";
    }
}
