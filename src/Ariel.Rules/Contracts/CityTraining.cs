namespace Ariel.Game;

/// <summary>Whose unit classes a city trains. A mod that keeps class overrides of its own units for a civilization
/// resolves the civilization here, as the city rules do for the civilization's own overrides.</summary>
public static class CityTraining
{
    /// <summary>The owner's civilization or, with Assimilation, the civilization that founded the city.</summary>
    public static string Civilization(Simulation sim, City city)
    {
        string civilization = sim.State.PlayerById(city.Owner).Civilization;
        if (sim.IsOptionEnabled("foundation:gameoption_assimilation")
            && city.Extensions.Get<CityRuleState>(sim.Content)?.OriginalCivilization is { Length: > 0 } original) return original;
        return civilization;
    }
}
