using Civ.Content;
using Civ.Modding;

namespace Civ.BaseGame;

/// <summary>
/// The base game is an ordinary mod: it has no privileges over any other. Terrain is code, techs/buildings/culture
/// levels are sidecar JSON, rules are turn systems and commands.
/// </summary>
public sealed class BaseGameMod : IMod
{
    public void Register(IModContext ctx)
    {
        // Terrain, registered in code.
        ctx.Terrains.Register(new TerrainDef { Id = "base:ocean",     Name = "Ocean",     IsWater = true, Passable = false, Food = 1, Commerce = 2, Color = "#2a5d9f" });
        ctx.Terrains.Register(new TerrainDef { Id = "base:grassland", Name = "Grassland", Food = 2, Color = "#5fa84a" });
        ctx.Terrains.Register(new TerrainDef { Id = "base:plains",    Name = "Plains",    Food = 1, Production = 1, Color = "#c4b45a" });
        ctx.Terrains.Register(new TerrainDef { Id = "base:desert",    Name = "Desert",    Color = "#e0cf8a" });
        ctx.Terrains.Register(new TerrainDef { Id = "base:hills",     Name = "Hills",     MoveCost = 2, Production = 2, Color = "#8a7a52" });
        ctx.Terrains.Register(new TerrainDef { Id = "base:mountains", Name = "Mountains", Passable = false, Color = "#9a9a9a" });

        // Sidecar data.
        foreach (var t in ctx.Data.ReadJson<TechDef[]>("data/techs.json")) ctx.Techs.Register(t);
        foreach (var b in ctx.Data.ReadJson<BuildingDef[]>("data/buildings.json")) ctx.Buildings.Register(b);
        foreach (var c in ctx.Data.ReadJson<CultureLevelDef[]>("data/culture.json")) ctx.CultureLevels.Register(c);

        ctx.MapGenerators.Register(new ContinentsGenerator());

        ctx.RegisterCommand<FoundCityCommand>("found_city");
        ctx.RegisterCommand<SetResearchCommand>("set_research");
        ctx.RegisterCommand<StartBuildingCommand>("start_building");

        ctx.TurnSystems.Register(new ResearchSystem());
        ctx.TurnSystems.Register(new ProductionSystem());
        ctx.TurnSystems.Register(new CultureSystem());
    }
}
