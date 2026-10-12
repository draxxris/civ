namespace Civ.Modding;

/// <summary>Contents of a mod's <c>mod.json</c>.</summary>
public sealed class ModManifest
{
    public string Id { get; init; } = "";
    public string Name { get; init; } = "";
    public string Version { get; init; } = "0.0.0";
    /// <summary>Assembly file in the mod folder that contains the IMod implementation.</summary>
    public string Assembly { get; init; } = "";
    public string[] Dependencies { get; init; } = [];
}
