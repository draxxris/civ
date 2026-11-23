using Ariel.Content;
using B = Ariel.Content.Schema.BuildingDef;

namespace Ariel.Game;

/// <summary>Resolves activity within a supplied building set without querying empire grants or modifiers.</summary>
public static class CityBuildingRules
{
    /// <summary>The buildings a city actually runs, as the city rules decide: empire grants, power, replacement and
    /// extension included, in ordinal id order. Every rule that asks which buildings work must ask this, so the mods
    /// asking it declare the city rules as a dependency. <see cref="Active"/> only resolves a set the caller supplies.</summary>
    public static IReadOnlyList<B> Running(Simulation sim, City city) =>
        sim.Rules().CityBuildings.ActiveBuildings(sim, city.Id)
            .Order(StringComparer.Ordinal).Select(sim.Content.Get<B>).ToArray();

    /// <summary>Whether any building the city could run (<see cref="ICityBuildingService.CandidateBuildings"/>) matches.
    /// <see cref="Running"/> only narrows those, so a rule that adds something for matching buildings can pass over a
    /// city where this is false without resolving its running set, the expensive part of visiting every city of an
    /// empire.</summary>
    public static bool MayRun(Simulation sim, City city, Func<B, bool> match)
    {
        foreach (var id in sim.Rules().CityBuildings.CandidateBuildings(sim, city.Id))
            if (match(sim.Content.Get<B>(id))) return true;
        return false;
    }

    public static IReadOnlyList<B> Active(Simulation sim, City city, IEnumerable<string> buildings) =>
        new Selection(sim, city, buildings).Active();

    public static IReadOnlyList<B> Effective(Simulation sim, City city, IEnumerable<string> buildings) =>
        new Selection(sim, city, buildings).Effective();

    public static bool Replaces(B replacer, B building) => replacer.Id != building.Id
        && (replacer.ReplaceBuildings.GetValueOrDefault(building.DefaultBuilding)
            || replacer.ReplaceBuildings.GetValueOrDefault(building.Id));

    /// <summary>A wonder is a building the world, a team or a player may hold only so many of. A building that declares
    /// no limit of its own covers its copies with `ClassLimits.NoLimit` (corporation buildings, the palace) and is not a
    /// wonder however tight its unused limit is, which is also why such a building never uses wonder capacity.</summary>
    public static bool IsWonder(B building) => !building.Limits.NoLimit
        && (building.Limits.World >= 0 || building.Limits.Team >= 0 || building.Limits.Player >= 0);

    private sealed class Selection(Simulation sim, City city, IEnumerable<string> ids)
    {
        private readonly B[] _buildings = ids.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).Select(sim.Content.Get<B>).ToArray();
        // Only a building that names replacements can replace another, so the replacement scan, which every building
        // of the set makes, visits these alone, in the same order.
        private B[]? _replacers;
        private readonly Dictionary<string, bool> _active = new(StringComparer.Ordinal);
        private readonly HashSet<string> _visiting = new(StringComparer.Ordinal);

        public IReadOnlyList<B> Active() => _buildings.Where(IsActive).ToArray();
        public IReadOnlyList<B> Effective() => _buildings.Where(b => HasBase(b) && !HasReplacer(b)).ToArray();

        private bool IsActive(B building)
        {
            if (_active.TryGetValue(building.Id, out bool active)) return active;
            // Invalid cyclic extensions/replacements cannot recurse indefinitely.
            if (!_visiting.Add(building.Id)) return false;
            active = IsBaseActive(building) && HasBase(building) && !HasReplacer(building);
            _visiting.Remove(building.Id);
            _active[building.Id] = active;
            return active;
        }

        private bool IsBaseActive(B building)
        {
            if (building.ObsoleteTech is { } tech && sim.KnowsTech(city.Owner, tech)) return false;
            return !building.RequiresActiveCivics || sim.Content.Requirements.EvaluateKind(building.Requirements,
                new(sim, city.Owner, city, sim.State.Grid.TileAt(city.Position)), "civic").Success;
        }

        private bool HasBase(B building) => building.ExtendsBuildingClass is not { Length: > 0 } category
            || _buildings.Any(b => b.Id != building.Id && (b.DefaultBuilding == category || b.Id == category) && IsActive(b));

        private bool HasReplacer(B building)
        {
            _replacers ??= Array.FindAll(_buildings, b => !b.ReplaceBuildings.IsEmpty);
            foreach (var replacer in _replacers)
                if (Replaces(replacer, building) && IsActive(replacer)) return true;
            return false;
        }
    }
}
