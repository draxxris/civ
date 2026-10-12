namespace Civ.Content;

public interface IHasId { string Id { get; } }

/// <summary>Base for data-driven definitions. Ids are namespaced: "modid:name". Plain init props so sidecar JSON binds directly.</summary>
public abstract class ContentDef : IHasId
{
    public string Id { get; init; } = "";
    public string Name { get; init; } = "";
}

public sealed class TerrainDef : ContentDef
{
    public int MoveCost { get; init; } = 1;
    public bool IsWater { get; init; }
    public bool Passable { get; init; } = true;
    public int Food { get; init; }
    public int Production { get; init; }
    public int Commerce { get; init; }
    /// <summary>Hint for clients, "#rrggbb".</summary>
    public string Color { get; init; } = "#888888";
}

public sealed class TechDef : ContentDef
{
    public int Cost { get; init; } = 10;
    public string[] Prerequisites { get; init; } = [];
}

public sealed class BuildingDef : ContentDef
{
    public int Cost { get; init; } = 20;
    public string? RequiresTech { get; init; }
    public int Food { get; init; }
    public int Production { get; init; }
    public int Commerce { get; init; }
    public int Culture { get; init; }
}

/// <summary>A cultural level (Civ4: Poor, Fledgling, Developing...). Reached when accumulated culture >= Threshold.</summary>
public sealed class CultureLevelDef : ContentDef
{
    public long Threshold { get; init; }
}

/// <summary>Registers a command class under an id so it can be serialized for the network.</summary>
public sealed record CommandTypeDef(string Id, Type Type) : IHasId;
