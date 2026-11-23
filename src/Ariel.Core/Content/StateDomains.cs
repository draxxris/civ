using System.Text.Json.Nodes;

namespace Ariel.Content;

/// <summary>A named part of the game state, registered by the module that owns its meaning, so that writers can say
/// they write only inside it and readers can say they never read it (<see cref="Game.Simulation"/>, "Write windows").
/// <see cref="Paths"/> locate its fields in the canonical state JSON, from the state's root: segments are separated by
/// <c>/</c>, <c>*</c> matches every element of an array or every property of an object, and the last segment names
/// the field (<c>Cities/*/Worked</c>, <c>Cities/*/Extensions/{module}:{component}/{field}</c>). The paths are used only
/// to verify declarations; they do not route writes. A field belongs to at most one domain, and everything that no
/// domain names is the state's remainder, which every reader reads.</summary>
/// <para>A private domain (<see cref="Private"/>) is read only by its own module's turn systems, commands and planning
/// code, never by a modifier source or by anything a source asks (<see cref="IStateReader"/>): the computer player's
/// saved intent, or a tally only its own turn system spends, for instance. Every reader counts it as unread without
/// naming it.</para>
public sealed record StateDomain(string Id, IReadOnlyList<string> Paths, bool Private = false);

/// <summary>A rule that reads the state and can say which state domains it never reads, directly or through any rule
/// it asks: a modifier source, or a provider another module's rule calls while it computes.</summary>
public interface IStateReader
{
    /// <summary>The state domains (<see cref="StateDomain"/>) this reader never reads, directly or through any rule it
    /// asks, given the loaded content: a reader that calls other modules' providers must leave out what any of them
    /// may read (<see cref="StateDomainRegistry.UnreadByAll"/>). Null, the default, declares nothing. A wrong
    /// declaration makes the game use stale answers; <see cref="Game.Simulation.VerifyWrites"/> catches it where a run
    /// exercises it. An id no loaded module registered declares nothing.</summary>
    IReadOnlyList<string>? UnreadDomains(ContentDb content) => null;
}

/// <summary>The registered state domains, at most <see cref="Capacity"/>, each with a bit in a <see cref="ulong"/>
/// mask. A declaration names domains by id; the registry resolves a list to its mask once per list instance.</summary>
public sealed class StateDomainRegistry
{
    /// <summary>The most domains a ruleset may register: one bit each in a 64-bit mask.</summary>
    public const int Capacity = 64;
    private readonly List<StateDomain> _domains = new();
    private readonly Dictionary<string, int> _index = new(StringComparer.Ordinal);
    private readonly System.Runtime.CompilerServices.ConditionalWeakTable<IReadOnlyList<string>, StrongBox> _masks = new();
    private bool _frozen;

    private sealed class StrongBox(ulong value, bool unknown) { public readonly ulong Value = value; public readonly bool Unknown = unknown; }

    /// <summary>Registered domains in registration order; a domain's bit is its index.</summary>
    public IReadOnlyList<StateDomain> All => _domains;

    /// <summary>The mask of the private domains, which every reader counts as unread.</summary>
    public ulong PrivateMask { get; private set; }

    /// <summary>The mask of every registered domain.</summary>
    public ulong AllMask => _domains.Count == Capacity ? ulong.MaxValue : (1UL << _domains.Count) - 1;

    internal void Register(StateDomain domain, string owner)
    {
        if (_frozen) throw new InvalidOperationException("State domain registry is frozen");
        if (!domain.Id.StartsWith(owner + ":", StringComparison.Ordinal) || domain.Id.Length == owner.Length + 1)
            throw new ArgumentException($"State domain '{domain.Id}' must belong to '{owner}'");
        if (_index.ContainsKey(domain.Id)) throw new InvalidOperationException($"Duplicate state domain '{domain.Id}'");
        if (_domains.Count == Capacity) throw new InvalidOperationException($"More than {Capacity} state domains");
        if (domain.Paths.Count == 0) throw new ArgumentException($"State domain '{domain.Id}' names no field");
        foreach (var path in domain.Paths)
            if (path.Length == 0 || path.Split('/').Any(s => s.Length == 0) || path.EndsWith('*'))
                throw new ArgumentException($"State domain '{domain.Id}' has a malformed path '{path}'");
        foreach (var other in _domains)
            foreach (var path in domain.Paths)
                if (other.Paths.Any(p => Overlap(p, path)))
                    throw new ArgumentException($"State domain '{domain.Id}' path '{path}' overlaps '{other.Id}'");
        if (domain.Private) PrivateMask |= 1UL << _domains.Count;
        _index.Add(domain.Id, _domains.Count);
        _domains.Add(domain);
    }

    internal void Freeze() => _frozen = true;

    /// <summary>The mask of the listed domain ids. An id no loaded module registered is left out, and
    /// <paramref name="unknown"/> says so: a domain that is not registered names no field, so what a declaration meant
    /// by it lies in the state's remainder. A writer that names one therefore writes the remainder, and a reader that
    /// names one as unread declares nothing by it.</summary>
    public ulong Mask(IReadOnlyList<string> ids, out bool unknown)
    {
        var box = _masks.GetValue(ids, list =>
        {
            ulong mask = 0;
            bool missing = false;
            foreach (var id in list)
                if (_index.TryGetValue(id, out int bit)) mask |= 1UL << bit;
                else missing = true;
            return new StrongBox(mask, missing);
        });
        unknown = box.Unknown;
        return box.Value;
    }

    /// <summary>The domains a reader never reads, as a mask: what it declares, and every private domain.</summary>
    public ulong UnreadMask(IStateReader reader, ContentDb content) =>
        (reader.UnreadDomains(content) is { } ids ? Mask(ids, out _) : 0UL) | PrivateMask;

    /// <summary>The ids in <paramref name="own"/> that every one of <paramref name="providers"/> also declares unread:
    /// what a reader that calls those providers may declare. A provider that is not an <see cref="IStateReader"/>, or
    /// declares nothing, leaves nothing.</summary>
    public static IReadOnlyList<string> UnreadByAll(IReadOnlyList<string> own, ContentDb content, IEnumerable<object> providers)
    {
        IEnumerable<string> result = own;
        foreach (var provider in providers)
        {
            if ((provider as IStateReader)?.UnreadDomains(content) is not { } theirs) return [];
            result = result.Intersect(theirs, StringComparer.Ordinal);
        }
        return result.ToArray();
    }

    /// <summary>Whether a domain id is registered.</summary>
    public bool Contains(string id) => _index.ContainsKey(id);

    /// <summary>Remove every field of the domains in <paramref name="mask"/> from a state JSON tree, so that what is
    /// left can be compared before and after a write that declared only those domains.</summary>
    public void Strip(JsonNode root, ulong mask)
    {
        for (int i = 0; i < _domains.Count; i++)
            if ((mask & (1UL << i)) != 0)
                foreach (var path in _domains[i].Paths) Strip(root, path.Split('/'), 0);
    }

    private static void Strip(JsonNode? node, string[] segments, int at)
    {
        if (node is null) return;
        string segment = segments[at];
        bool last = at == segments.Length - 1;
        if (segment == "*")
        {
            if (node is JsonArray array) foreach (var item in array) Strip(item, segments, at + 1);
            else if (node is JsonObject all) foreach (var (_, value) in all) Strip(value, segments, at + 1);
            return;
        }
        if (node is not JsonObject obj) return;
        if (last) obj.Remove(segment);
        else if (obj.TryGetPropertyValue(segment, out var child)) Strip(child, segments, at + 1);
    }

    // Two paths overlap when one names a field the other contains or the same field.
    private static bool Overlap(string a, string b)
    {
        var x = a.Split('/'); var y = b.Split('/');
        for (int i = 0; i < Math.Min(x.Length, y.Length); i++)
            if (x[i] != y[i] && x[i] != "*" && y[i] != "*") return false;
        return true;
    }
}
