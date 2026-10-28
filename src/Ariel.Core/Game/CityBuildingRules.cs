using Ariel.Content;
using B = Ariel.Content.Schema.BuildingDef;

namespace Ariel.Game;

/// <summary>Resolves activity within a supplied building set without querying empire grants or modifiers.</summary>
public static class CityBuildingRules
{
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

        private bool HasReplacer(B building) => _buildings.Any(b => Replaces(b, building) && IsActive(b));
    }
}
