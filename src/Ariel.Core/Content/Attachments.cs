namespace Ariel.Content;

/// <summary>What one mod says, in its own vocabulary, about a definition it may not own: Corporations saying a civic
/// forbids corporations, for example. An attachment is ordinary content of the attaching mod's own type, so it is
/// loaded, namespaced, overridden, validated and hashed like any definition. Its <see cref="Target"/> must name a
/// loaded definition of <see cref="TargetType"/>, from a provider the registering mod declares; a definition has at
/// most one attachment of each type. Any mod may register rows of a published attachment type, about its own content
/// or another's, and the type's owner reads them with <see cref="ContentDb.Attachment{T}"/>.
/// <para>An attachment's <see cref="AmendsTargetAttribute"/> fields are not read through the attachment: they add to
/// the target's field of the same name when content freezes.</para></summary>
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

/// <summary>An attachment field that adds to its target's field of the same name and type, so every reader of the target
/// (rules, the computer player, presentation) sees one definition. It is how a mod puts its own vocabulary into another
/// module's rows: the amounts of its commerce type in a base building's commerce vectors, slots for its specialist,
/// its units in a civilization's overrides, while the base rows name none of it and removing the mod removes all of it.
/// <para>When content freezes, before it is hashed, each target is replaced by a copy carrying its fields plus every
/// amending row's, rows applied in ordinal id order. Commerce vectors add. Maps add their entries; a key both name is
/// an error unless its values combine (vectors, sequences, maps). Sequences append. A field of any other type fills
/// in a target field that is empty (null), such as the promotion a base building grants when that promotion is a
/// mod's. Amending a set value is a load error, as are a field the target lacks and a target that itself amends.
/// References in an amending field are validated as the target field's are (its
/// <see cref="ContentReferenceAttribute"/>s), but against the amending row's mod, which must declare every provider it
/// names; the merged target is validated as its own mod registered it, never again with the additions.</para></summary>
[AttributeUsage(AttributeTargets.Property, Inherited = true)]
public sealed class AmendsTargetAttribute : Attribute;
