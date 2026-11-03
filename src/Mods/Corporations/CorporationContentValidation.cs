using Ariel.Content.Schema;

namespace Ariel.Mods.Corporations;

/// <summary>Registration-time checks for the invariants the schema cannot express. Frozen content reference
/// validation covers unknown identities, technologies and great-person classes, so this only checks what it cannot:
/// rules that would fail silently (an unfoundable corporation, a free branch, two rules for one identity).
/// Identity existence cannot be checked here because the Corporations mod registers before Foundation loads the
/// identity records; content reference validation does it after the freeze.</summary>
internal static class CorporationContentValidation
{
    public static void Validate(IReadOnlyList<CorporationDef> definitions)
    {
        var errors = new List<string>();
        foreach (var def in definitions.OrderBy(d => d.Id, StringComparer.Ordinal))
        {
            if (def.Identity.Length == 0)
                errors.Add($"{def.Id}: Identity is empty, so no building, headquarters or executive can reference it");
            if (def.RequiredGreatPersonClass.Length == 0)
                errors.Add($"{def.Id}: RequiredGreatPersonClass is empty, so no unit can found it");
            if (def.PrereqBonuses.Length == 0)
                errors.Add($"{def.Id}: PrereqBonuses is empty, so ConsumedBonusCount is always zero and ValidateFound always rejects");
            if (def.SpreadCost < 0)
                errors.Add($"{def.Id}: negative SpreadCost would pay the spreader");
            if (def.MaintenanceHundredths < 0)
                errors.Add($"{def.Id}: negative MaintenanceHundredths would subsidise the branch");
        }
        foreach (var group in definitions.GroupBy(d => d.Identity, StringComparer.Ordinal).Where(g => g.Count() > 1))
            errors.Add($"{string.Join(", ", group.Select(d => d.Id))}: share identity '{group.Key}', so building, headquarters and executive references are ambiguous");
        if (errors.Count > 0) throw new InvalidDataException("Corporation content validation failed:\n" + string.Join("\n", errors));
    }
}
