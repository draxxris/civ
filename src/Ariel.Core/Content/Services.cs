namespace Ariel.Content;

/// <summary>Frozen non-content services, ordered by registration sequence across the mod load order.
/// Ids identify implementations; they do not reorder earlier registrations. A contract that is itself
/// <see cref="IHasId"/> (production kinds, the computer player's planners and valuers) is looked up by the implementation's own id, so two
/// implementations of it with one id fail at registration, during content load.</summary>
public sealed class ServiceRegistry
{
    private readonly Dictionary<Type, List<object>> _items = new();
    private readonly HashSet<(Type, string)> _ids = new();
    private bool _frozen;
    private readonly System.Collections.Concurrent.ConcurrentDictionary<Type, object> _views = new();
    private readonly List<string> _identities = new();
    internal IEnumerable<string> Identities => _identities;
    /// <summary>All services implementing the registered contract, in registration order.</summary>
    public IReadOnlyList<T> Get<T>() where T : class
    {
        if (!_items.TryGetValue(typeof(T), out var items)) return Array.Empty<T>();
        if (!_frozen) return items.Cast<T>().ToArray();
        return (IReadOnlyList<T>)_views.GetOrAdd(typeof(T), static (_, values) => Array.AsReadOnly(values.Cast<T>().ToArray()), items);
    }

    /// <summary>The one service registered under <typeparamref name="T"/>, or null when no loaded mod registered one.
    /// Use it for a contract a single mod provides, such as a mod's published API, whose provider may be absent.
    /// After freezing the answer is cached per contract type, so hot paths may call it freely and from any thread.
    /// Throws when several services implement <typeparamref name="T"/>: read those with <see cref="Get{T}"/>.</summary>
    public T? Find<T>() where T : class
    {
        var slots = Volatile.Read(ref _found);
        int slot = Slot<T>.Index;
        if (slot < slots.Length && slots[slot] is { } found) return ReferenceEquals(found, Absent) ? null : (T)found;
        return Resolve<T>(slot);
    }
    private T? Resolve<T>(int slot) where T : class
    {
        var all = Get<T>();
        if (all.Count > 1)
            throw new InvalidOperationException($"{all.Count} services implement {typeof(T).FullName}; Find needs at most one. Use Get for a seam with many implementations.");
        T? found = all.Count == 1 ? all[0] : null;
        if (!_frozen) return found;
        lock (_items)
        {
            var slots = _found;
            if (slot >= slots.Length) Array.Resize(ref slots, Math.Max(slot + 1, slots.Length * 2));
            slots[slot] = (object?)found ?? Absent;
            Volatile.Write(ref _found, slots);
        }
        return found;
    }
    /// <summary>Process-wide index of a contract type into every frozen registry's <see cref="_found"/> cache.</summary>
    private static class Slot<T> { internal static readonly int Index = Interlocked.Increment(ref _slots) - 1; }
    private static int _slots;
    private static readonly object Absent = new();
    private object?[] _found = [];

    internal void Register<T>(string id, T service, string owner) where T : class
    {
        if (_frozen) throw new InvalidOperationException("Services are frozen");
        if (!id.StartsWith(owner + ":", StringComparison.Ordinal) || id.Length == owner.Length + 1) throw new ArgumentException($"Service id '{id}' must belong to '{owner}'");
        if (!_ids.Add((typeof(T), id))) throw new InvalidOperationException($"Duplicate service '{id}' for {typeof(T).Name}");
        if (!_items.TryGetValue(typeof(T), out var list)) _items[typeof(T)] = list = new();
        if (service is IHasId named && typeof(IHasId).IsAssignableFrom(typeof(T)))
            foreach (IHasId existing in list)
                if (string.Equals(existing.Id, named.Id, StringComparison.Ordinal))
                    throw new InvalidOperationException($"Service '{id}' and an earlier {TypeName(typeof(T))} both have the id '{named.Id}'");
        list.Add(service);
        _identities.Add($"{ContractName(typeof(T))}:{id}");
    }
    internal void Freeze() => _frozen = true;

    /// <summary>A contract's readable name for load errors: <c>IAiValuer&lt;TechSubject&gt;</c> rather than a mangled generic name.</summary>
    private static string TypeName(Type type) => !type.IsGenericType ? type.Name
        : $"{type.Name[..type.Name.IndexOf('`')]}<{string.Join(", ", type.GetGenericArguments().Select(TypeName))}>";

    /// <summary>A contract's name in the content hash: the full name, and for a closed generic contract the full
    /// names of its arguments without the assembly-qualified form <see cref="Type.FullName"/> would give them.</summary>
    private static string ContractName(Type type) => !type.IsGenericType ? type.FullName!
        : $"{type.Namespace}.{type.Name[..type.Name.IndexOf('`')]}<{string.Join(",", type.GetGenericArguments().Select(ContractName))}>";
}

/// <summary>Stable identity of a loaded mod, included in saves and content hashes.</summary>
public sealed record LoadedMod(string Id, string Version);
