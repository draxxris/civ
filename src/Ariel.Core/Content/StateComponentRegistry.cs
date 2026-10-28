namespace Ariel.Content;

/// <summary>Stable mod-owned state type identities. Register once during mod loading.</summary>
public sealed class StateComponentRegistry
{
    private readonly Dictionary<Type, string> _ids = new();
    private readonly SortedDictionary<string, Type> _types = new(StringComparer.Ordinal);
    private bool _frozen;
    /// <summary>Resolve the stable id of a registered component type.</summary>
    public string IdOf(Type type) => _ids.TryGetValue(type, out var id) ? id : throw new InvalidOperationException($"Unregistered state type '{type.FullName}'");
    /// <summary>Resolve the stable id of a registered component type, or null when the type is not registered.</summary>
    public string? TryIdOf(Type type) => _ids.GetValueOrDefault(type);
    /// <summary>Resolve a component id; unknown types cannot be loaded safely, even with forced content loading.</summary>
    public Type TypeOf(string id) => _types.TryGetValue(id, out var type) ? type : throw new InvalidDataException($"Unknown state component '{id}'");
    internal IEnumerable<string> Ids => _types.Keys;
    internal void Register<T>(string id, string owner) where T : class
    {
        if (_frozen) throw new InvalidOperationException("State component registry is frozen");
        if (!id.StartsWith(owner + ":", StringComparison.Ordinal) || id.Length == owner.Length + 1) throw new ArgumentException($"State component '{id}' must belong to '{owner}'");
        if (_types.ContainsKey(id) || _ids.ContainsKey(typeof(T))) throw new InvalidOperationException($"Duplicate state component '{id}' or type '{typeof(T).FullName}'");
        if (typeof(T).IsAbstract) throw new ArgumentException("State component types must be concrete");
        StateSchema.Validate(typeof(T));
        _types.Add(id, typeof(T)); _ids.Add(typeof(T), id);
    }
    internal void Freeze() => _frozen = true;
}
