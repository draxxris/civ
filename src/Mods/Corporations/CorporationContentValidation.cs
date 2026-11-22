using Ariel.Content;
using Ariel.Content.Schema;

namespace Ariel.Mods.Corporations;

/// <summary>Registration-time checks for the invariants the schema cannot express. Frozen content reference
/// validation covers unknown identities and technologies, so this only checks what it cannot: rules that would fail
/// silently (an unfoundable corporation, a free branch, two rules for one identity, a headquarters that founds no
/// corporation or a corporation with no headquarters). Identity existence cannot be checked here because the
/// Corporations mod registers before Foundation loads the identity records; content reference validation does it after
/// the freeze. <see cref="Executives"/> reads other mods' rows and so runs after the freeze too.</summary>
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

    /// <summary>Every executive that spreads one of this mod's corporations can be trained somewhere: its cost is not
    /// negative, and some way through its requirements needs nothing that can never exist, which is a building no city
    /// may construct (a negative cost, or a founding building no loaded founder claims) or a corporation this mod does
    /// not define. An executive is the only way to spread a corporation into a foreign city, so one that can never be
    /// trained silently removes foreign spread. Executives of identities no corporation here has spread nothing and are
    /// not checked. Unit rows belong to Military, which may register after this mod, so this runs on frozen content
    /// (<see cref="IContentCheck"/>).</summary>
    public static IEnumerable<string> Executives(ContentDb content)
    {
        var identities = content.Get<CorporationDef>().All.Select(d => d.Identity).ToHashSet(StringComparer.Ordinal);
        var founders = content.Services.Get<Ariel.Game.IBuildingFounder>();
        foreach (var row in content.Get<UnitCorporationsDef>().All.OrderBy(r => r.Target, StringComparer.Ordinal))
        {
            var spreads = row.CorporationSpreads.Where(p => p.Value > 0 && identities.Contains(p.Key)).Select(p => p.Key).Order(StringComparer.Ordinal).ToArray();
            if (spreads.Length == 0 || !content.TryGet<UnitDef>(row.Target, out var unit)) continue;
            string spread = string.Join(", ", spreads);
            if (unit.Cost < 0) yield return $"{unit.Id}: spreads {spread} but has a negative cost, so no city can train it";
            else if (unit.Requirements is { } requirements && Never(requirements) is { } blocker)
                yield return $"{unit.Id}: spreads {spread} but can never be trained: it needs {blocker.Kind} '{blocker.Id}', which can never exist";
        }

        // The leaf that blocks every way through the requirement, or null when some way can be met.
        Requirement.Leaf? Never(Requirement requirement) => requirement switch
        {
            Requirement.All all => all.Items.Select(Never).FirstOrDefault(blocker => blocker is not null),
            Requirement.Any any => any.Items.Select(Never).ToArray() is var blockers && blockers.Length > 0 && blockers.All(b => b is not null) ? blockers[0] : null,
            Requirement.Leaf leaf => Exists(leaf) ? null : leaf,
            // A negated requirement is met by the absence of something, which can always happen.
            _ => null,
        };
        bool Exists(Requirement.Leaf leaf) => leaf.Kind switch
        {
            "building" => content.TryGet<BuildingDef>(leaf.Id, out var building) && building.Cost >= 0
                && (building.FoundsCorporation is null && building.FoundsReligion is null || founders.Any(f => f.Claims(content, building))),
            "corporation" => identities.Contains(leaf.Id),
            _ => true,
        };
    }
}

/// <summary>Runs <see cref="CorporationContentValidation.Executives"/> once content freezes.</summary>
internal sealed class ExecutiveTrainingCheck : Ariel.Game.IContentCheck
{
    public IEnumerable<string> Problems(ContentDb content) => CorporationContentValidation.Executives(content);
}
