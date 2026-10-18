using Civ.Content;
using Civ.Game;
using Schema = Civ.Content.Schema;

namespace Civ.Client;

/// <summary>How a tile relates to the open city screen. Locked: inside the radius but unavailable (another city or foreign).</summary>
public enum TileMark { None, Worked, Workable, Selected, Locked }

public sealed partial class GameSession
{
    public string CitizenSpecialist => Service<ICityWorkService>().DefaultSpecialist(Sim);

    public IReadOnlyList<Hex> WorkArea(City city) =>
        Service<ICityWorkService>().WorkArea(Sim, city.Id).Select(Sim.State.Grid.HexOf).ToList();

    /// <summary>Tiles a citizen could be put on right now (ignores whether a free citizen exists).</summary>
    public IReadOnlyList<Hex> WorkableTiles(City city)
    {
        var work = Service<ICityWorkService>();
        var none = new Dictionary<string, int>();
        return WorkArea(city).Where(h => h != city.Position && !city.Worked.Contains(h) && work.Validate(Sim, city.Id, [h], none) == null)
            .Concat(city.Worked).Distinct().ToList();
    }

    /// <summary>Marks for every tile of the selected city's work area, computed once so the renderer does not repeat the validation per tile.</summary>
    public IReadOnlyDictionary<Hex, TileMark> TileMarks()
    {
        var marks = new Dictionary<Hex, TileMark>();
        if (City is not { } city) return marks;
        var workable = WorkableTiles(city).ToHashSet();
        foreach (var hex in WorkArea(city).Where(h => SightOf(h) == Sight.Visible))
            marks[hex] = hex == city.Position ? TileMark.Selected : city.Worked.Contains(hex) ? TileMark.Worked
                : workable.Contains(hex) ? TileMark.Workable : TileMark.Locked;
        return marks;
    }

    public TileMark MarkOf(Hex hex)
    {
        if (City != null && Sim.State.Grid.TryTile(hex, out _)) return TileMarks().GetValueOrDefault(Sim.State.Grid.Normalize(hex));
        return Unit is { } unit && unit.Position == hex ? TileMark.Selected : TileMark.None;
    }

    /// <summary>Current yields only in sight; fog cannot query live irrigation or adjacent modifiers.</summary>
    public YieldVector YieldsOf(Hex hex) => SightOf(hex) == Sight.Visible
        ? Service<IPlotYieldService>().Calculate(Sim, Human, Sim.State.Grid.TileAt(hex)).Yields : default;

    private long TileScore(Hex hex)
    {
        var y = YieldsOf(hex);
        return y.Food * 3 + y.Production * 2 + y.Commerce;
    }



    private bool Assign(City city, List<Hex> tiles, IDictionary<string, int> specialists, bool automatic = false)
    {
        var spec = new SortedDictionary<string, int>(specialists.Where(s => s.Value > 0).ToDictionary(s => s.Key, s => s.Value), StringComparer.Ordinal);
        var state = city.Extensions.Get<CityRuleState>(Sim.Content);
        return Submit(new SetCityAssignmentsCommand
        {
            Player = Human, CityId = city.Id, Tiles = tiles.ToArray(), Specialists = spec, Automatic = automatic,
            AvoidGrowth = state?.AvoidGrowth ?? false,
        });
    }

    /// <summary>Take a citizen off a worked tile (it becomes a plain citizen) or put one on a free tile, taking it from the plain pool, then any specialist, then the worst worked tile.</summary>
    public void ToggleTile(City city, Hex hex)
    {
        hex = Sim.State.Grid.Normalize(hex);
        var tiles = city.Worked.ToList();
        var spec = new Dictionary<string, int>(city.Specialists);
        if (tiles.Remove(hex)) spec[CitizenSpecialist] = spec.GetValueOrDefault(CitizenSpecialist) + 1;
        else if (!TakeCitizen(city, tiles, spec, hex)) { SetMessage("No citizen to assign"); return; }
        Assign(city, tiles, spec);
    }

    /// <summary>Free one citizen for <paramref name="hex"/> and add the tile; false when the city has nobody to move.</summary>
    private bool TakeCitizen(City city, List<Hex> tiles, Dictionary<string, int> spec, Hex hex)
    {
        string? from = spec.GetValueOrDefault(CitizenSpecialist) > 0 ? CitizenSpecialist
            : spec.Where(kv => kv.Value > 0).Select(kv => kv.Key).FirstOrDefault();
        if (tiles.Count + spec.Values.Sum() < WorkingPopulation(city)) { tiles.Add(hex); return true; }
        if (from != null) spec[from]--;
        else if (tiles.Count > 0) tiles.Remove(tiles.OrderBy(TileScore).ThenBy(t => t.Q).ThenBy(t => t.R).First());
        else return false;
        tiles.Add(hex);
        return true;
    }

    /// <summary>Move one citizen into (+1) or out of (-1) a specialist role; out goes back to the plain citizen pool.</summary>
    public void ChangeSpecialist(City city, string id, int delta)
    {
        var tiles = city.Worked.ToList();
        var spec = new Dictionary<string, int>(city.Specialists);
        if (delta > 0 && !FreeCitizen(city, tiles, spec, id)) { SetMessage("No citizen available to reassign"); return; }
        if (delta < 0)
        {
            if (spec.GetValueOrDefault(id) <= 0) return;
            spec[id]--;
            spec[CitizenSpecialist] = spec.GetValueOrDefault(CitizenSpecialist) + 1;
        }
        else spec[id] = spec.GetValueOrDefault(id) + 1;
        Assign(city, tiles, spec);
    }

    private bool FreeCitizen(City city, List<Hex> tiles, Dictionary<string, int> spec, string target)
    {
        if (tiles.Count + spec.Values.Sum() < WorkingPopulation(city)) return true;
        if (target != CitizenSpecialist && spec.GetValueOrDefault(CitizenSpecialist) > 0) { spec[CitizenSpecialist]--; return true; }
        if (tiles.Count == 0) return false;
        tiles.Remove(tiles.OrderBy(TileScore).ThenBy(t => t.Q).ThenBy(t => t.R).First());
        return true;
    }

    private int WorkingPopulation(City city) => city.Population - Service<ICityWellbeingService>().Calculate(Sim, city.Id).Angry;

    /// <summary>Hand the citizens back to the governor.</summary>
    public void AutoAssign(City city) => Assign(city, [], new Dictionary<string, int>(), automatic: true);

    public void ToggleAvoidGrowth(City city)
    {
        var state = city.Extensions.Get<CityRuleState>(Sim.Content);
        Submit(new SetCityAssignmentsCommand
        {
            Player = Human, CityId = city.Id, Tiles = city.Worked.ToArray(), Specialists = new(city.Specialists, StringComparer.Ordinal),
            Automatic = !(state?.ManualWork ?? false), AvoidGrowth = !(state?.AvoidGrowth ?? false),
        });
    }

    // --- production -------------------------------------------------------------------------

    public bool Enqueue(City city, string kind, string id, bool append) =>
        Submit(new SetCityProductionCommand { Player = Human, CityId = city.Id, Kind = kind, ContentId = id, Append = append });

    public bool RemoveFromQueue(City city, int index) =>
        Submit(new SetCityProductionCommand { Player = Human, CityId = city.Id, RemoveIndex = index });

    /// <summary>Swap a queue entry with its neighbour (-1 earlier, +1 later) by rebuilding the queue; stored progress is kept.</summary>
    public bool MoveInQueue(City city, int index, int delta)
    {
        int to = index + delta;
        var order = city.ProductionQueue.ToList();
        if (index < 0 || index >= order.Count || to < 0 || to >= order.Count) return false;
        (order[index], order[to]) = (order[to], order[index]);
        // Validate before clearing anything: obsolete entries can stay queued, but cannot be re-added through the command service.
        foreach (var item in order)
        {
            var error = new SetCityProductionCommand { Player = Human, CityId = city.Id, Kind = item.Kind, ContentId = item.ContentId }.Validate(Sim);
            if (error != null) { SetMessage($"Can't reorder: {error}"); return false; }
        }
        for (int i = 0; i < order.Count; i++) Enqueue(city, order[i].Kind, order[i].ContentId, append: i > 0);
        return true;
    }

    public bool Hurry(City city, string hurryId) => Submit(new HurryCityCommand { Player = Human, CityId = city.Id, HurryId = hurryId });

    public IReadOnlyList<PendingCityConquest> DescribePendingConquests()
    {
        var commands = Service<ICityCommandService>();
        return Sim.State.Cities
            .Where(city => city.Owner == Human)
            .Select(city => (City: city, Rules: city.Extensions.Get<CityRuleState>(Sim.Content)))
            .Where(entry => entry.Rules?.PendingConquestChoice == true)
            .OrderBy(entry => entry.City.Id)
            .Select(entry => new PendingCityConquest(
                entry.City.Id, entry.City.Name, entry.City.Population, entry.Rules!.OccupationTurns,
                commands.ValidateConquestChoice(Sim, Human, entry.City.Id, CityConquestOption.Raze) is null))
            .ToList();
    }

    public bool ResolveCityConquest(int cityId, CityConquestOption option) =>
        Submit(new ResolveCityConquestCommand { Player = Human, CityId = cityId, Option = option });

    public bool AnnexCity(City city) => ResolveCityConquest(city.Id, CityConquestOption.Annex);

    public bool Conscript(City city) => Submit(new ConscriptCityCommand { Player = Human, CityId = city.Id });
}
