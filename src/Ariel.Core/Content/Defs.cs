namespace Ariel.Content;

/// <summary>Stable namespaced identity shared by content and rule registries.</summary>
public interface IHasId
{
    /// <summary>Ordinal id in the form modid:name.</summary>
    string Id { get; }
}

/// <summary>Base for sidecar definitions. Definitions and nested collections must not be mutated after loading.</summary>
public abstract class ContentDef : IHasId
{
    /// <summary>Stable namespaced identity.</summary>
    public string Id { get; init; } = "";
    /// <summary>Localized display name.</summary>
    public string Name { get; init; } = "";
    /// <summary>Optional eligibility expression, interpreted by registered leaf evaluators.</summary>
    public Requirement? Requirements { get; init; }
}

/// <summary>Command serialization identity and its CLR type.</summary>
public sealed record CommandTypeDef(string Id, Type Type) : IHasId;
