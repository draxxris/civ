using Ariel.Content;
using Ariel.Content.Schema;

namespace Ariel.Mods.Corporations;

/// <summary>Registration-time checks for the invariants the schema cannot express. Frozen content reference
/// validation covers unknown identities and technologies, so this only checks what it cannot: rules that would fail
/// silently (an unfoundable corporation, a free branch, two rules for one identity, a headquarters that founds no
/// corporation or a corporation with no headquarters). Identity existence cannot be checked here because the
/// Corporations mod registers before Foundation loads the identity records; content reference validation does it after
/// the freeze.</summary>
internal static class CorporationContentValidation
{
    /// <summary>Checks the corporations and their founding headquarters, and returns which corporation each
    /// headquarters founds, by building id.</summary>
    public static IReadOnlyDictionary<string, string> Validate(IReadOnlyList<CorporationDef> definitions, IReadOnlyList<BuildingDef> headquarters)
    {
        var errors = new List<string>();
        foreach (var def in definitions.OrderBy(d => d.Id, StringComparer.Ordinal))
        {
            if (def.Identity.Length == 0)
                errors.Add($"{def.Id}: Identity is empty, so no building, headquarters or executive can reference it");
            if (def.PrereqBonuses.Length == 0)
                errors.Add($"{def.Id}: PrereqBonuses is empty, so ConsumedBonusCount is always zero and founding always fails");
            if (def.SpreadCost < 0)
                errors.Add($"{def.Id}: negative SpreadCost would pay the spreader");
            if (def.MaintenanceHundredths < 0)
                errors.Add($"{def.Id}: negative MaintenanceHundredths would subsidise the branch");
            int founding = headquarters.Count(b => b.FoundsCorporation == def.Identity);
            if (founding != 1)
                errors.Add($"{def.Id}: {founding} founding headquarters name its identity; it needs exactly one");
        }
        foreach (var group in definitions.GroupBy(d => d.Identity, StringComparer.Ordinal).Where(g => g.Count() > 1))
            errors.Add($"{string.Join(", ", group.Select(d => d.Id))}: share identity '{group.Key}', so building, headquarters and executive references are ambiguous");
        var founds = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var building in headquarters.OrderBy(b => b.Id, StringComparer.Ordinal))
        {
            var corporation = definitions.FirstOrDefault(d => d.Identity == building.FoundsCorporation);
            if (corporation is null) { errors.Add($"{building.Id}: founds no corporation of this mod ('{building.FoundsCorporation}')"); continue; }
            if (corporation.FoundingTech is { } tech && !ContentReferences.Leaves(building.Requirements ?? new Requirement.All([])).Contains(new Requirement.Leaf("tech", tech)))
                errors.Add($"{building.Id}: does not require {tech}, the technology that makes {corporation.Id} available");
            founds[building.Id] = corporation.Id;
        }
        if (errors.Count > 0) throw new InvalidDataException("Corporation content validation failed:\n" + string.Join("\n", errors));
        return founds;
    }
}
