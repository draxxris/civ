namespace Civ.Content;

/// <summary>Deterministically ordered (ordinal id) registry. Mods register into it during load; it is frozen afterwards.</summary>
public sealed class Registry<T> where T : class, IHasId
{
    private readonly SortedDictionary<string, T> _items = new(StringComparer.Ordinal);
    private readonly Dictionary<string, string> _owners = new(StringComparer.Ordinal);
    private bool _frozen;

    public string Kind { get; }
    public Registry(string kind) { Kind = kind; }

    public int Count => _items.Count;
    public IEnumerable<T> All => _items.Values;
    public bool Contains(string id) => _items.ContainsKey(id);
    public T Get(string id) => _items.TryGetValue(id, out var v) ? v : throw new KeyNotFoundException($"{Kind} '{id}' is not registered");
    public bool TryGet(string id, out T item) => _items.TryGetValue(id, out item!);
    public string OwnerOf(string id) => _owners[id];

    internal void Register(T item, string modId)
    {
        EnsureOpen();
        ValidateId(item.Id, modId);
        if (_items.ContainsKey(item.Id))
            throw new InvalidOperationException($"{Kind} '{item.Id}' is already registered by '{_owners[item.Id]}'. Use Override to replace it.");
        _items[item.Id] = item;
        _owners[item.Id] = modId;
    }

    internal void Override(T item, string modId)
    {
        EnsureOpen();
        if (!_items.ContainsKey(item.Id))
            throw new InvalidOperationException($"Cannot override unknown {Kind} '{item.Id}'");
        _items[item.Id] = item;
        _owners[item.Id] = modId;
    }

    internal void Freeze() => _frozen = true;

    private void EnsureOpen()
    {
        if (_frozen) throw new InvalidOperationException($"{Kind} registry is frozen");
    }

    private void ValidateId(string id, string modId)
    {
        if (!id.StartsWith(modId + ":", StringComparison.Ordinal) || id.Length == modId.Length + 1)
            throw new ArgumentException($"{Kind} id '{id}' must be namespaced as '{modId}:<name>'");
    }
}

/// <summary>The view of a registry that one mod gets: it can add its own content and override anyone's.</summary>
public sealed class ModRegistry<T> where T : class, IHasId
{
    private readonly Registry<T> _inner;
    private readonly string _modId;
    internal ModRegistry(Registry<T> inner, string modId) { _inner = inner; _modId = modId; }

    public void Register(T item) => _inner.Register(item, _modId);
    public void Override(T item) => _inner.Override(item, _modId);
    public bool Contains(string id) => _inner.Contains(id);
    public T Get(string id) => _inner.Get(id);
    public IEnumerable<T> All => _inner.All;
}
