namespace Civ.Content;

/// <summary>Frozen non-content services, ordered by registration sequence across the mod load order.
/// Ids identify implementations; they do not reorder earlier registrations.</summary>
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
    internal void Register<T>(string id, T service, string owner) where T : class
    {
        if (_frozen) throw new InvalidOperationException("Services are frozen");
        if (!id.StartsWith(owner + ":", StringComparison.Ordinal) || id.Length == owner.Length + 1) throw new ArgumentException($"Service id '{id}' must belong to '{owner}'");
        if (!_ids.Add((typeof(T), id))) throw new InvalidOperationException($"Duplicate service '{id}' for {typeof(T).Name}");
        if (!_items.TryGetValue(typeof(T), out var list)) _items[typeof(T)] = list = new();
        list.Add(service);
        _identities.Add($"{typeof(T).FullName}:{id}");
    }
    internal void Freeze() => _frozen = true;
}

/// <summary>Stable identity of a loaded mod, included in saves and content hashes.</summary>
public sealed record LoadedMod(string Id, string Version);
