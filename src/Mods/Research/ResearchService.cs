using Civ.Content;
using Civ.Content.Schema;
using Civ.Game;
using TechDef = Civ.Content.Schema.TechDef;
using UnitDef = Civ.Content.Schema.UnitDef;

namespace Civ.Mods.Research;

internal sealed class ResearchService : IResearchService
{
    public ResearchCostResult CalculateCost(Simulation sim, int team, string tech) => ResearchCosts.Calculate(sim, team, tech);
    public int RateModifier(Simulation sim, int team, string tech) => ResearchCosts.RateModifier(sim, sim.State.TeamById(team), tech);
    public RequirementResult Validate(Simulation sim, int player, string tech)
    {
        if (Availability(sim, player, tech) is { } reason) return Reject(reason);
        return sim.Content.Requirements.Evaluate(sim.Content.Get<TechDef>(tech).Requirements, new(sim, player));
    }
    public string? ValidateQueue(Simulation sim, int player, IReadOnlyList<string> queue, bool freeTech = false)
    {
        if (!sim.State.Players.Any(p => p.Id == player)) return "Unknown player";
        if (freeTech && ValidateFreeSelection(sim, player, queue) is { } freeError) return freeError;
        // Future entries may depend on earlier entries. Recheck actual requirements at activation.
        foreach (var id in queue)
        {
            if (Availability(sim, player, id) is { } reason) return reason + ": " + id;
        }
        if (queue.Count == 0) return null;
        var result = Validate(sim, player, queue[0]);
        return result.Success ? null : string.Join("; ", result.Failures.Select(f => f.Explanation));
    }
    private static string? Availability(Simulation sim, int player, string tech)
    {
        if (!sim.Content.TryGet<TechDef>(tech, out var def)) return "Unknown technology";
        if (def.Disable) return "Technology is disabled";
        return sim.KnowsTech(player, tech) && !def.Repeat ? "Technology is already known" : null;
    }
    private static string? ValidateFreeSelection(Simulation sim, int player, IReadOnlyList<string> queue)
    {
        if (queue.Count != 1) return "Choose one free technology";
        return Read(sim, sim.TeamOf(player)).FreeTechs <= 0 ? "No free technologies available" : null;
    }
    public void SetQueue(Simulation sim, int player, IReadOnlyList<string> queue, bool freeTech = false)
    {
        var team = sim.TeamOf(player);
        var state = Edit(sim, team);
        if (freeTech)
        {
            state.FreeTechs--;
            Discover(sim, player, team, queue[0]);
            return;
        }
        state.Queue = queue.ToList();
        team.Researching = queue.FirstOrDefault();
    }
    public void Contribute(Simulation sim, int player, long beakers)
    {
        if (GovernmentStatus.IsAnarchy(sim, player)) return;
        var team = sim.TeamOf(player);
        var state = Edit(sim, team);
        SelectNext(sim, player, team, state);
        if (team.Researching is not { } tech) { state.Overflow += beakers; return; }
        int modifier = RateModifier(sim, team.Id, tech);
        long amount = beakers * modifier / 100 + state.Overflow * modifier / 100;
        state.Overflow = 0;
        long progress = team.Research.GetValueOrDefault(tech) + amount;
        long cost = CalculateCost(sim, team.Id, tech).Cost;
        team.Research[tech] = progress;
        if (progress < cost) return;
        state.Overflow = (progress - cost) * 100 / modifier;
        team.Research[tech] = 0;
        Discover(sim, player, team, tech);
        if (state.Queue.FirstOrDefault() == tech) state.Queue.RemoveAt(0);
        team.Researching = null;
        SelectNext(sim, player, team, state);
        // Like the source player phase, overflow is spent on the next research contribution, not recursively this turn.
    }
    public void AddProgress(Simulation sim, int player, string tech, long beakers)
    {
        if (!Validate(sim, player, tech).Success || beakers <= 0) return;
        var team = sim.TeamOf(player);
        long cost = CalculateCost(sim, team.Id, tech).Cost;
        team.Research[tech] = Math.Min(cost, team.Research.GetValueOrDefault(tech) + beakers);
        if (team.Research[tech] < cost) return;
        team.Research[tech] = 0;
        Discover(sim, player, team, tech);
        var state = Edit(sim, team);
        state.Queue.RemoveAll(t => t == tech);
        if (team.Researching == tech) team.Researching = null;
        SelectNext(sim, player, team, state);
    }
    private void SelectNext(Simulation sim, int player, Team team, ResearchTeamState state)
    {
        if (team.Researching is { } active && Validate(sim, player, active).Success) return;
        team.Researching = null;
        while (state.Queue.Count > 0)
        {
            string id = state.Queue[0];
            if (Validate(sim, player, id).Success) { team.Researching = id; return; }
            if (!team.Techs.Contains(id)) return; // Retain an unmet prerequisite rather than silently dropping it.
            state.Queue.RemoveAt(0);
        }
    }
    private static void Discover(Simulation sim, int player, Team team, string tech)
    {
        var def = sim.Content.Get<TechDef>(tech);
        bool first = !sim.State.Teams.Any(t => t.Techs.Contains(tech));
        var state = Edit(sim, team);
        team.Techs.Add(tech);
        state.Completions[tech] = state.Completions.GetValueOrDefault(tech) + 1;
        if (first) state.FreeTechs += def.FirstFreeTechs;

        sim.Events.Publish(new TechDiscovered(team.Id, tech));
        if (first && def.FirstFreeUnit is { } unit) GrantUnit(sim, player, unit);
    }

    private static void GrantUnit(Simulation sim, int player, string type)
    {
        var city = sim.State.Cities.Where(c => c.Owner == player).OrderBy(c => c.Id).FirstOrDefault();
        var tile = sim.State.PlayerById(player).StartingPlot;
        if (city is null && tile is null) return;
        var def = sim.Content.Get<UnitDef>(type);
        var unit = new Unit { Id = sim.State.NextUnitId++, Owner = player, Type = type,
            Position = city?.Position ?? sim.State.Grid.HexOf(tile!.Value),
            MovesLeft = def.Moves * sim.Content.Get<RuleConstantsDef>().All.Single().Values.MoveDenominator };
        sim.State.AddUnit(unit);
        sim.Events.Publish(new UnitCreated(unit.Id, player, type, unit.Position));
    }
    private static RequirementResult Reject(string reason) => new(false, [new(null, reason)]);
    private static ResearchTeamState Read(Simulation sim, Team team) => team.Extensions.Get<ResearchTeamState>(sim.Content) ?? new();
    private static ResearchTeamState Edit(Simulation sim, Team team)
    {
        var state = Read(sim, team);
        team.Extensions.Set(sim.Content, state);
        return state;
    }
}

internal sealed class ResearchPhase(IResearchService research) : IPlayerTurnSystem
{
    public string Id => "research:progress";
    public int Order => TurnPhase.Research;
    public void Run(Simulation sim, int player)
    {
        if (GovernmentStatus.IsAnarchy(sim, player)) return;
        long commerce = sim.Content.Services.Get<IPlayerResearchSource>().SingleOrDefault()?.ResearchHundredths(sim, player) ?? 0;
        // Players without cities cannot accumulate the otherwise unconditional base research.
        if (!sim.State.Cities.Any(c => c.Owner == player)) return;
        long baseRate = sim.Content.Get<RuleConstantsDef>().All.Single().Values.BaseResearchRate;
        research.Contribute(sim, player, Math.Max(0, baseRate + commerce / 100));
    }
    public void Run(Simulation sim) { foreach (var player in sim.State.Players.OrderBy(p => p.Id)) Run(sim, player.Id); }
}
