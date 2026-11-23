using Ariel.Content;
using Ariel.Content.Schema;

namespace Ariel.Mods.Corporations;

/// <summary>Load-time checks for the invariants the schema cannot express. Frozen content reference validation covers
/// unknown identities and resources, so this only checks what it cannot: rules that would fail silently (a branch that
/// pays its spreader, two rules for one identity, a corporation no building founds, a founding building that founds
/// nothing, an executive that spreads a corporation nobody defines). <see cref="Validate"/> runs when the mod
/// registers; <see cref="FoundingBuildings"/> and <see cref="Executives"/> read other modules' rows (Cities' buildings,
/// Military's units) and so run on frozen content (<see cref="Ariel.Game.IContentCheck"/>).</summary>
internal static class CorporationContentValidation
{
    /// <summary>Checks the corporations' own rows.</summary>
    public static void Validate(IReadOnlyList<CorporationDef> definitions)
    {
        var errors = new List<string>();
        foreach (var def in definitions.OrderBy(d => d.Id, StringComparer.Ordinal))
        {
            if (def.Identity.Length == 0)
                errors.Add($"{def.Id}: Identity is empty, so no building, headquarters or executive can reference it");
            if (def.SpreadCost < 0)
                errors.Add($"{def.Id}: negative SpreadCost would pay the spreader");
            if (def.MaintenanceHundredths < 0)
                errors.Add($"{def.Id}: negative MaintenanceHundredths would subsidise the branch");
            if (def.CompetingCorporations.Contains(def.Identity, StringComparer.Ordinal))
                errors.Add($"{def.Id}: competes with itself, so it would evict its own branches");
        }
        foreach (var group in definitions.GroupBy(d => d.Identity, StringComparer.Ordinal).Where(g => g.Count() > 1))
            errors.Add($"{string.Join(", ", group.Select(d => d.Id))}: share identity '{group.Key}', so building, headquarters and executive references are ambiguous");
        if (errors.Count > 0) throw new InvalidDataException("Corporation content validation failed:\n" + string.Join("\n", errors));
    }

    /// <summary>Every corporation has exactly one founding building (a building whose <c>FoundsCorporation</c> names its
    /// identity), which a city can build (its cost is not negative) and which needs a technology, so no corporation can
    /// be founded from the start of the game. Every building that names a corporation identity names one this mod
    /// defines, so a founding building never sits in the build list with nothing behind it.</summary>
    public static IEnumerable<string> FoundingBuildings(ContentDb content)
    {
        var identities = content.Get<CorporationDef>().All.Select(d => d.Identity).ToHashSet(StringComparer.Ordinal);
        var founding = content.Get<BuildingDef>().All.Where(b => b.FoundsCorporation is not null)
            .OrderBy(b => b.Id, StringComparer.Ordinal).ToList();
        foreach (var def in content.Get<CorporationDef>().All.OrderBy(d => d.Id, StringComparer.Ordinal))
        {
            var buildings = founding.Where(b => b.FoundsCorporation == def.Identity).Select(b => b.Id).ToArray();
            if (buildings.Length != 1)
                yield return $"{def.Id}: {buildings.Length} buildings found it ({string.Join(", ", buildings)}); it needs exactly one";
        }
        foreach (var building in founding)
        {
            if (!identities.Contains(building.FoundsCorporation!))
            {
                yield return $"{building.Id}: founds '{building.FoundsCorporation}', which no corporation of this mod has as its identity";
                continue;
            }
            if (building.Cost < 0)
                yield return $"{building.Id}: founds {building.FoundsCorporation} but has a negative cost, so no city can build it";
            if (!ContentReferences.Leaves(building.Requirements ?? new Requirement.All([])).Any(l => l.Kind == "tech"))
                yield return $"{building.Id}: founds {building.FoundsCorporation} but requires no technology, so it could be founded from the start";
        }
    }

    /// <summary>Every executive that spreads one of this mod's corporations can be trained somewhere: its cost is not
    /// negative, and some way through its requirements needs nothing that can never exist, which is a building no city
    /// may construct (a negative cost, or a founding building no loaded founder claims) or a corporation this mod does
    /// not define. An executive is the only way to spread a corporation into a foreign city, so one that can never be
    /// trained silently removes foreign spread. A unit row that spreads an identity no corporation here has would spread
    /// nothing, so it is refused too. Unit rows belong to Military, which may register after this mod, so this runs on
    /// frozen content (<see cref="Ariel.Game.IContentCheck"/>).</summary>
    public static IEnumerable<string> Executives(ContentDb content)
    {
        var identities = content.Get<CorporationDef>().All.Select(d => d.Identity).ToHashSet(StringComparer.Ordinal);
        var founders = content.Services.Get<Ariel.Game.IBuildingFounder>();
        foreach (var row in content.Get<UnitCorporationsDef>().All.OrderBy(r => r.Target, StringComparer.Ordinal))
        {
            foreach (var orphan in row.CorporationSpreads.Keys.Where(k => !identities.Contains(k)).Order(StringComparer.Ordinal))
                yield return $"{row.Target}: spreads '{orphan}', which no corporation of this mod has as its identity";
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

/// <summary>Runs <see cref="CorporationContentValidation.FoundingBuildings"/> once content freezes.</summary>
internal sealed class FoundingBuildingCheck : Ariel.Game.IContentCheck
{
    public IEnumerable<string> Problems(ContentDb content) => CorporationContentValidation.FoundingBuildings(content);
}

/// <summary>Runs <see cref="CorporationContentValidation.Executives"/> once content freezes.</summary>
internal sealed class ExecutiveTrainingCheck : Ariel.Game.IContentCheck
{
    public IEnumerable<string> Problems(ContentDb content) => CorporationContentValidation.Executives(content);
}
