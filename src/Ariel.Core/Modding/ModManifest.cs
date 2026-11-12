namespace Ariel.Modding;

/// <summary>Contents of a mod's <c>mod.json</c>.</summary>
public sealed class ModManifest
{
    /// <summary>Unqualified mod id and content namespace.</summary>
    public string Id { get; init; } = "";
    /// <summary>Display name.</summary>
    public string Name { get; init; } = "";
    /// <summary>Rules/content version included in content identity. Change it when code semantics change.</summary>
    public string Version { get; init; } = "0.0.0";
    /// <summary>Assembly file in the mod folder that contains the IMod implementation. Empty for a base module,
    /// whose code is linked into the host.</summary>
    public string Assembly { get; init; } = "";
    /// <summary>Optional assembly file in the mod folder that holds this mod's published API: the interfaces,
    /// commands, events, state and content definitions other mods and the client may compile against. It must carry
    /// <see cref="ModApiAttribute"/> naming this mod. The loader shares one copy of it across the process
    /// (<see cref="ModApis"/>). Empty when the mod publishes nothing.</summary>
    public string Api { get; init; } = "";
    /// <summary>Required mod ids loaded before this mod.</summary>
    public string[] Dependencies { get; init; } = [];
    /// <summary>Required content providers. May be cyclic; does not affect registration order.</summary>
    public string[] References { get; init; } = [];
}
