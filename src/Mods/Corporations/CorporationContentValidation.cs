using Ariel.Content;
using Ariel.Content.Schema;

namespace Ariel.Mods.Corporations;

/// <summary>Load-time checks for the invariants the schema cannot express. Frozen content reference validation covers
/// unknown corporations and resources, so this only checks what it cannot: rules that would fail silently (a branch
/// that pays its spreader, a corporation no building founds, a founding building that founds nothing, an executive that
/// spreads a corporation nobody defines). All of them run on frozen content (<see cref="Ariel.Game.IContentCheck"/>):
/// <see cref="Rows"/> checks every corporation any mod defines, and <see cref="FoundingBuildings"/> and
/// <see cref="Executives"/> read every mod's buildings and units, and the founders registered for them.</summary>
internal static class CorporationContentValidation
{
    /// <summary>Checks the corporations' own rows.</summary>
    public static IEnumerable<string> Rows(IEnumerable<CorporationDef> definitions)
    {
        foreach (var def in definitions.OrderBy(d => d.Id, StringComparer.Ordinal))
        {
            if (def.SpreadCost < 0)
                yield return $"{def.Id}: negative SpreadCost would pay the spreader";
            if (def.MaintenanceHundredths < 0)
                yield return $"{def.Id}: negative MaintenanceHundredths would subsidise the branch";
            if (def.CompetingCorporations.Contains(def.Id, StringComparer.Ordinal))
                yield return $"{def.Id}: competes with itself, so it would evict its own branches";
            if (def.NaturalSpreadPercent <= 0)
                yield return $"{def.Id}: NaturalSpreadPercent {def.NaturalSpreadPercent} would never spread with Realistic Guilds/Corporations";
            if (def.PrereqBonuses.Length == 0 && def.FoundingBuildingClasses.Count == 0)
                yield return $"{def.Id}: consumes no resource and has no FoundingBuildingClasses, so Realistic Guilds/Corporations would found it with its technology alone";
            if (def.PrereqBonuses.Length > 0 && def.FoundingBuildingClasses.Count > 0)
                yield return $"{def.Id}: consumes a resource, which gates its founding; FoundingBuildingClasses is only for a corporation that consumes none";
            foreach (var (building, count) in def.FoundingBuildingClasses.Where(p => p.Value < 1))
                yield return $"{def.Id}: FoundingBuildingClasses needs {count} of {building}; a count must be at least 1";
        }
    }

    /// <summary>Every corporation has exactly one founding building (a building whose <c>Founds</c> names it), which a
    /// city can build (its cost is not negative) and which needs a technology, so no corporation can be founded from the
    /// start of the game. Every building that founds a corporation names one this mod defines, so a founding building
    /// never sits in the build list with nothing behind it.</summary>
    public static IEnumerable<string> FoundingBuildings(ContentDb content)
    {
        var founding = content.Get<BuildingDef>().All.Where(b => b.Founds?.Kind == CorporationFounder.Kind)
            .OrderBy(b => b.Id, StringComparer.Ordinal).ToList();
        foreach (var def in content.Get<CorporationDef>().All.OrderBy(d => d.Id, StringComparer.Ordinal))
        {
            var buildings = founding.Where(b => b.Founds!.Id == def.Id).Select(b => b.Id).ToArray();
            if (buildings.Length != 1)
                yield return $"{def.Id}: {buildings.Length} buildings found it ({string.Join(", ", buildings)}); it needs exactly one";
        }
        foreach (var building in founding)
        {
            string founds = building.Founds!.Id;
            if (!content.TryGet<CorporationDef>(founds, out _))
            {
                yield return $"{building.Id}: founds '{founds}', which is no corporation of this mod";
                continue;
            }
            if (building.Cost < 0)
                yield return $"{building.Id}: founds {founds} but has a negative cost, so no city can build it";
            if (!ContentReferences.Leaves(building.Requirements ?? new Requirement.All([])).Any(l => l.Kind == "tech"))
                yield return $"{building.Id}: founds {founds} but requires no technology, so it could be founded from the start";
        }
    }

    /// <summary>Every executive that spreads one of this mod's corporations can be trained somewhere: its cost is not
    /// negative, and some way through its requirements needs nothing that can never exist, which is a building no city
    /// may construct (a negative cost, or a founding building no loaded founder claims) or a corporation this mod does
    /// not define. An executive is the only way to spread a corporation into a foreign city, so one that can never be
    /// trained silently removes foreign spread. A unit row that spreads a corporation this mod does not define would
    /// spread nothing, so it is refused too. Unit rows belong to Military, which may register after this mod, so this runs on
    /// frozen content (<see cref="Ariel.Game.IContentCheck"/>).</summary>
    public static IEnumerable<string> Executives(ContentDb content)
    {
        var corporations = content.Get<CorporationDef>().All.Select(d => d.Id).ToHashSet(StringComparer.Ordinal);
        var founders = content.Services.Get<Ariel.Game.IBuildingFounder>();
        foreach (var row in content.Get<UnitCorporationsDef>().All.OrderBy(r => r.Target, StringComparer.Ordinal))
        {
            foreach (var orphan in row.CorporationSpreads.Keys.Where(k => !corporations.Contains(k)).Order(StringComparer.Ordinal))
                yield return $"{row.Target}: spreads '{orphan}', which is no corporation of this mod";
            var spreads = row.CorporationSpreads.Where(p => p.Value > 0 && corporations.Contains(p.Key)).Select(p => p.Key).Order(StringComparer.Ordinal).ToArray();
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
                && (building.Founds is null || founders.Any(f => f.Claims(content, building))),
            "corporation" => corporations.Contains(leaf.Id),
            _ => true,
        };
    }
}

/// <summary>Runs <see cref="CorporationContentValidation.Rows"/> once content freezes, so a corporation another mod
/// defines is held to the same rules as this mod's own.</summary>
internal sealed class CorporationRowCheck : Ariel.Game.IContentCheck
{
    public IEnumerable<string> Problems(ContentDb content) => CorporationContentValidation.Rows(content.Get<CorporationDef>().All);
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

/// <summary>The source's rule constants the Realistic Guilds/Corporations spread reads are present and usable: a spread
/// range and distance divisor above zero, a resource influence not below zero, and a free-trade change no lower than
/// -100%. Foundation owns the rule constants, so this runs on frozen content.</summary>
internal sealed class SourceConstantCheck : Ariel.Game.IContentCheck
{
    public IEnumerable<string> Problems(ContentDb content)
    {
        var raw = content.Get<RuleConstantsDef>().All.Single().Raw;
        foreach (var key in NaturalCorporations.SourceConstants)
        {
            if (!int.TryParse(raw.GetValueOrDefault(key), System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out int value))
            {
                yield return $"rule constant {key} is missing or not a whole number; the realistic corporation spread reads it";
                continue;
            }
            int minimum = key switch
            {
                "CORPORATION_SPREAD_RAND" or "CORPORATION_SPREAD_DISTANCE_DIVISOR" => 1,
                "FREE_TRADE_CORPORATION_SPREAD_MOD" => -100,
                _ => 0,
            };
            if (value < minimum) yield return $"rule constant {key} is {value}; the realistic corporation spread needs at least {minimum}";
        }
    }
}
