using ContentDb = Ariel.Content.ContentDb;
using Ariel.Content.Schema;
using Ariel.Game;

namespace Ariel.Mods.Foundation;

/// <summary>Feature ruleset setup, using CvPlayer::initFreeUnits class grants and role selection.</summary>
internal sealed class NewGameSetup : INewGameSetup
{
    public MapSpec ResolveMap(ContentDb content, SimulationSettings settings)
    {
        var world = content.Get<WorldSizeDef>(settings.WorldSize!);
        var scale = content.Get<HexOverridesDef>("foundation:hex_overrides").SourceGridScale;
        _ = content.Get<GameSpeedDef>(settings.GameSpeed);
        _ = content.Get<HandicapDef>(settings.Handicap);
        _ = content.Get<EraDef>(settings.StartingEra);
        return new(checked(world.GridWidth * scale), checked(world.GridHeight * scale), true);
    }

    public void CreatePlayers(Simulation sim)
    {
        var settings = sim.Settings;
        if (settings.Leaders.Length != settings.PlayerCount) throw new ArgumentException("Select a leader for each player");
        for (int id = 0; id < settings.PlayerCount; id++) CreatePlayer(sim, id);
    }

    private static void CreatePlayer(Simulation sim, int id)
    {
        var settings = sim.Settings;
        var leader = sim.Content.Get<LeaderDef>(settings.Leaders[id]);
        var civ = Civilization(sim.Content, settings, id, leader.Id);
        int teamId = id < settings.PlayerTeams.Length ? settings.PlayerTeams[id] : id;
        if (teamId < 0) throw new ArgumentException("Team ids must be nonnegative");
        var team = sim.State.Teams.FirstOrDefault(t => t.Id == teamId);
        if (team is null) { team = new() { Id = teamId }; sim.State.Teams.Add(team); }
        team.Members.Add(id);
        AddTechs(team, civ.StartingTechs);
        var handicap = sim.Content.Get<HandicapDef>(settings.Handicap);
        AddTechs(team, handicap.StartingTechs);
        string? ai = id < settings.PlayerAis.Length ? settings.PlayerAis[id] : null;
        if (ai is not null) { _ = sim.Content.PlayerAIs.Get(ai); AddTechs(team, handicap.AiStartingTechs); }
        var player = new Player
        {
            Id = id, TeamId = teamId, Civilization = civ.Id, Leader = leader.Id, Name = civ.Name, Ai = ai,
            Treasury = handicap.Gold + sim.Content.Get<EraDef>(settings.StartingEra).StartingGold,
        };
        foreach (var option in sim.Content.Get<CivicOptionDef>().All)
        {
            var defaultCivic = option.DefaultCivic ?? sim.Content.Get<CivicDef>().All.FirstOrDefault(c => c.Option == option.Id && c.Requirements is null)?.Id;
            if (defaultCivic is not null) player.Civics[option.Id] = defaultCivic;
        }
        foreach (var civic in civ.StartingCivics)
            if (sim.Content.Get<CivicDef>(civic).Option is { } optionId) player.Civics[optionId] = civic;
        sim.State.Players.Add(player);
    }

    private static CivilizationDef Civilization(ContentDb db, SimulationSettings settings, int id, string leader)
    {
        var choices = db.Get<CivilizationDef>().All.Where(c => c.Playable && c.Leaders.GetValueOrDefault(leader)).ToArray();
        if (id < settings.Civilizations.Length)
            return choices.SingleOrDefault(c => c.Id == settings.Civilizations[id])
                ?? throw new ArgumentException("Leader does not belong to the selected playable civilization");
        return choices.Length == 1 ? choices[0] : throw new ArgumentException("Select a civilization for a leader with multiple civilizations");
    }

    private static void AddTechs(Team team, IReadOnlyDictionary<string, bool> grants) =>
        team.Techs.UnionWith(grants.Where(p => p.Value).Select(p => p.Key).Order(StringComparer.Ordinal));

    public void Complete(Simulation sim)
    {
        var starts = sim.Content.Services.Get<IStartingPositionService>().Single();
        var positions = starts.Select(sim, sim.State.Players.Count);
        starts.Normalize(sim, positions);
        foreach (var player in sim.State.Players.OrderBy(p => p.Id)) GrantUnits(sim, player, positions[player.Id]);
        if (sim.Content.Get<EraDef>(sim.Settings.StartingEra).NoGoodies)
            foreach (var plot in sim.State.Plots)
                if (plot.Improvement is { } id && sim.Content.Get<ImprovementDef>(id).Goody) plot.Improvement = null;
        sim.Content.Services.Get<IVisibilityService>().Single().Recompute(sim);
    }

    private static void GrantUnits(Simulation sim, Player player, TileId tile)
    {
        player.StartingPlot = tile;
        var civ = sim.Content.Get<CivilizationDef>(player.Civilization);
        var era = sim.Content.Get<EraDef>(sim.Settings.StartingEra);
        var handicap = sim.Content.Get<HandicapDef>(sim.Settings.Handicap);
        int multiplier = era.StartingUnitMultiplier + (player.Ai is null ? 0 : handicap.AiStartingUnitMultiplier);
        foreach (var grant in civ.StartingUnits.OrderBy(p => p.Key, StringComparer.Ordinal))
        {
            string? type = civ.UnitOverrides.GetValueOrDefault(grant.Key, grant.Key);
            if (type is not null) AddUnits(sim, player.Id, tile, type, grant.Value * multiplier);
        }
        GrantRole(sim, player, tile, "military:unitai_city_defense", era.StartingDefenseUnits + handicap.StartingDefenseUnits + (player.Ai is null ? 0 : handicap.AiStartingDefenseUnits));
        GrantRole(sim, player, tile, "military:unitai_worker", era.StartingWorkerUnits + handicap.StartingWorkerUnits + (player.Ai is null ? 0 : handicap.AiStartingWorkerUnits));
        GrantRole(sim, player, tile, "military:unitai_explore", era.StartingExploreUnits + handicap.StartingExploreUnits + (player.Ai is null ? 0 : handicap.AiStartingExploreUnits));
    }

    private static void GrantRole(Simulation sim, Player player, TileId tile, string role, int count)
    {
        if (count <= 0) return;
        var civ = sim.Content.Get<CivilizationDef>(player.Civilization);
        // Only the civilization's resolved classes can supply a role. Tech prerequisites apply to role grants.
        var candidate = sim.Content.Get<Ariel.Content.Schema.UnitDef>().All
            .Where(u => civ.UnitOverrides.GetValueOrDefault(u.DefaultUnit, u.DefaultUnit) == u.Id)
            .Where(u => u.Cost >= 0 && u.Domain == "military:domain_land" && RoleEligible(u, role))
            .Where(u => sim.Content.Requirements.Evaluate(u.Requirements, new(sim, player.Id)).Success)
.OrderByDescending(u => RoleValue(u, role)).ThenBy(u => u.Id, StringComparer.Ordinal).FirstOrDefault();
        if (candidate is not null) AddUnits(sim, player.Id, tile, candidate.Id, count);
    }

    private static bool RoleEligible(UnitDef unit, string role) => role switch
    {
        "military:unitai_worker" => unit.WorkRate > 0,
        "military:unitai_explore" => unit.Strength > 0 && !unit.NoRevealMap && !unit.CanFoundCity,
        "military:unitai_city_defense" => unit.Strength > 0 && !unit.NoDefensiveBonus,
        _ => unit.UnitAiTypes.GetValueOrDefault(role),
    };
    private static int RoleValue(UnitDef unit, string role) => role switch
    {
        "military:unitai_worker" => unit.WorkRate,
        "military:unitai_explore" => unit.Moves * 100 + (unit.NoBadGoodies ? 100 : 0) + unit.Strength,
        _ => unit.Strength * (100 + unit.CityDefense),
    };

    private static void AddUnits(Simulation sim, int owner, TileId tile, string type, int count)
    {
        var definition = sim.Content.Get<Ariel.Content.Schema.UnitDef>(type);
        for (int i = 0; i < count; i++)
            sim.State.AddUnit(new() { Id = sim.State.NextUnitId++, Owner = owner, Type = type,
                Position = sim.State.Grid.HexOf(tile), MovesLeft = definition.Moves });
    }
}
