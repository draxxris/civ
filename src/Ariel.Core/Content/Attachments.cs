namespace Ariel.Content;

/// <summary>What one mod says, in its own vocabulary, about a definition it may not own: Corporations saying a civic
/// forbids corporations, for example. An attachment is ordinary content of the attaching mod's own type, so it is
/// loaded, namespaced, overridden, validated and hashed like any definition. Its <see cref="Target"/> must name a
/// loaded definition of <see cref="TargetType"/>, from a provider the registering mod declares; a definition has at
/// most one attachment of each type. Any mod may register rows of a published attachment type, about its own content
/// or another's, and the type's owner reads them with <see cref="ContentDb.Attachment{T}"/>.</summary>
public abstract class ContentAttachment : ContentDef
{
    /// <summary>Id of the definition this row is about.</summary>
    public string Target { get; init; } = "";
    /// <summary>The definition type <see cref="Target"/> must name.</summary>
    public abstract Type TargetType();
}

/// <summary>An attachment to definitions of <typeparamref name="TTarget"/> (or a subtype).</summary>
public abstract class ContentAttachment<TTarget> : ContentAttachment where TTarget : ContentDef
{
    /// <inheritdoc />
    public sealed override Type TargetType() => typeof(TTarget);
}
