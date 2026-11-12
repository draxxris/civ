namespace Ariel.Rules.Cities;

/// <summary>Ledger keys for city health. The key a contribution goes under is its origin, chosen once by the
/// mod that emits it (mods that cannot see this class write the same string). Every reader decides from the
/// key alone: a building-only-healthy city drops the penalties under <see cref="Building"/>, and pollution
/// counts the penalties under <see cref="Building"/> and <see cref="Bonus"/>.</summary>
internal static class HealthKeys
{
    /// <summary>Health from anything that is neither a building nor a bonus: civics, traits, religion, events.</summary>
    public const string General = "cities:health";
    /// <summary>Health a building gives, wherever its effect reaches, and a civic's change to a kind of building.</summary>
    public const string Building = "cities:building_health";
    /// <summary>Health that depends on a bonus, including a building's change for having the bonus.</summary>
    public const string Bonus = "cities:bonus_health";
}
