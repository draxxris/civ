using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Ariel.Content;

/// <summary>A commerce type a mod registers: gold, research, culture, or a type a feature mod owns. A content row whose
/// type implements this joins the database's commerce set (<see cref="ContentDb.Commerce"/>); the mod that defines the
/// row type gives it its behaviour.</summary>
public interface ICommerceType : IHasId
{
    /// <summary>Short key that names the type in commerce vectors (<c>"gold"</c>), in modifier keys
    /// (<c>commerce.flat.gold</c>) and in keys derived from it. Lower case letters, digits and underscores, starting with
    /// a letter, and unique among the loaded types. It is not the id, so the row can move between mods without changing
    /// any vector or ledger key.</summary>
    string Key { get; }
    /// <summary>Position in the commerce set: the order the slider, the city screen and the ledger list the types in.
    /// Ties break by <see cref="Key"/>.</summary>
    int Order { get; }
}

/// <summary>One loaded commerce type and its slot in the database's commerce layout.</summary>
public sealed class CommerceType
{
    internal CommerceType(ICommerceType definition, CommerceLayout layout, int index)
    {
        Definition = definition; Layout = layout; Index = index;
        FlatKey = layout.FlatKeys[index]; PercentKey = layout.PercentKeys[index];
    }
    /// <summary>The registered row.</summary>
    public ICommerceType Definition { get; }
    /// <summary>The row's id.</summary>
    public string Id => Definition.Id;
    /// <summary>Vector key (<see cref="ICommerceType.Key"/>), interned.</summary>
    public string Key => Layout[Index];
    /// <summary>The database's commerce layout.</summary>
    public CommerceLayout Layout { get; }
    /// <summary>Slot in <see cref="Layout"/>, which is also the position in <see cref="CommerceTypes"/>.</summary>
    public int Index { get; }
    /// <summary>Typed modifier key for flat amounts: <c>commerce.flat.&lt;key&gt;</c>.</summary>
    public string FlatKey { get; }
    /// <summary>Typed modifier key for percent amounts: <c>commerce.percent.&lt;key&gt;</c>.</summary>
    public string PercentKey { get; }
    /// <inheritdoc/>
    public override string ToString() => Key;
}

/// <summary>The commerce types one content database loaded, in order (<see cref="ICommerceType.Order"/>). Each type's
/// position is its slot in <see cref="Layout"/>, so a vector in this layout reads a type with one index.</summary>
public sealed class CommerceTypes : IReadOnlyList<CommerceType>
{
    private readonly CommerceType[] _types;
    /// <summary>No commerce types.</summary>
    public static CommerceTypes Empty { get; } = new([]);
    internal CommerceTypes(IReadOnlyList<ICommerceType> definitions)
    {
        var ordered = definitions.OrderBy(d => d.Order).ThenBy(d => d.Key, StringComparer.Ordinal).ToArray();
        Layout = ordered.Length == 0 ? null : CommerceLayout.Of(ordered.Select(d => d.Key).ToArray());
        _types = ordered.Select((d, i) => new CommerceType(d, Layout!, i)).ToArray();
        Zero = Layout is null ? default : CommerceVector.Zero(Layout);
    }
    /// <summary>Every loaded type's key in order, or null when none is loaded.</summary>
    public CommerceLayout? Layout { get; }
    /// <summary>A zero vector carrying every loaded type. State and commands start from it, so they always name every
    /// type.</summary>
    public CommerceVector Zero { get; }
    /// <inheritdoc/>
    public int Count => _types.Length;
    /// <inheritdoc/>
    public CommerceType this[int index] => _types[index];
    /// <summary>The type with this key; an unknown key is an error.</summary>
    public CommerceType this[string key] => TryGet(key, out var type) ? type : throw new KeyNotFoundException($"Commerce type '{key}' is not loaded");
    /// <summary>The type with this key, if loaded.</summary>
    public bool TryGet(ReadOnlySpan<char> key, out CommerceType type)
    {
        int index = Layout?.IndexOf(key) ?? -1;
        type = index < 0 ? null! : _types[index];
        return index >= 0;
    }
    /// <summary>The type with this key, if loaded.</summary>
    public bool TryGet(string key, out CommerceType type) => TryGet(key.AsSpan(), out type);
    /// <summary>Whether a modifier key is one of the sink's typed vector keys for this set: a yield component
    /// (<c>yield.flat.food</c>, ...) or a loaded commerce type (<c>commerce.percent.research</c>, ...).</summary>
    public bool IsTypedKey(string key) => ModifierSink.IsYieldKey(key)
        || (ModifierSink.CommerceKeyOf(key, out _) is { IsEmpty: false } commerce && TryGet(commerce, out _));
    /// <inheritdoc/>
    public IEnumerator<CommerceType> GetEnumerator() => ((IEnumerable<CommerceType>)_types).GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    /// <summary>Whether a vector key is well formed: a lower case letter, then lower case letters, digits or
    /// underscores.</summary>
    public static bool IsValidKey(ReadOnlySpan<char> key)
    {
        if (key.IsEmpty || key[0] is < 'a' or > 'z') return false;
        foreach (char c in key) if (c is not ((>= 'a' and <= 'z') or (>= '0' and <= '9') or '_')) return false;
        return true;
    }
}

/// <summary>The ordered commerce keys a vector carries. Layouts are interned by key sequence, so two vectors with the
/// same keys in the same order share one instance and combine slot by slot; vectors with other keys combine through
/// a merge each layout keeps. At most <see cref="Capacity"/> keys.</summary>
public sealed class CommerceLayout
{
    /// <summary>The most commerce types one vector carries.</summary>
    public const int Capacity = 8;
    private static readonly object Gate = new();
    private static ImmutableArray<CommerceLayout> _all = [];
    private static readonly ConcurrentDictionary<string, string> InternedKeys = new(StringComparer.Ordinal);
    private static readonly ConcurrentDictionary<string, string>.AlternateLookup<ReadOnlySpan<char>> InternedKeySpans =
        InternedKeys.GetAlternateLookup<ReadOnlySpan<char>>();
    private readonly string[] _keys;
    private volatile Merge[] _merges = [];

    private CommerceLayout(string[] keys)
    {
        _keys = keys;
        FlatKeys = keys.Select(k => "commerce.flat." + k).ToArray();
        PercentKeys = keys.Select(k => "commerce.percent." + k).ToArray();
        // State and save JSON name the components as the original four properties did: Gold, Research, ...
        JsonNames = keys.Select(k => char.ToUpperInvariant(k[0]) + k[1..]).ToArray();
    }
    /// <summary>Number of keys.</summary>
    public int Count => _keys.Length;
    /// <summary>Key in slot <paramref name="index"/>.</summary>
    public string this[int index] => _keys[index];
    internal string[] FlatKeys { get; }
    internal string[] PercentKeys { get; }
    internal string[] JsonNames { get; }

    /// <summary>Slot of a key, or -1.</summary>
    public int IndexOf(string key)
    {
        var keys = _keys;
        for (int i = 0; i < keys.Length; i++) if (ReferenceEquals(keys[i], key)) return i;
        return IndexOf(key.AsSpan());
    }
    /// <summary>Slot of a key, or -1.</summary>
    public int IndexOf(ReadOnlySpan<char> key)
    {
        var keys = _keys;
        for (int i = 0; i < keys.Length; i++) if (key.SequenceEqual(keys[i])) return i;
        return -1;
    }

    /// <summary>The layout of these keys in this order. Keys must be well formed (<see cref="CommerceTypes.IsValidKey"/>)
    /// and distinct.</summary>
    public static CommerceLayout Of(ReadOnlySpan<string> keys)
    {
        if (keys.Length > Capacity) throw new InvalidDataException($"A commerce vector holds at most {Capacity} types, not {keys.Length}");
        KeyBuffer buffer = default;
        Span<string> interned = buffer;
        interned = interned[..keys.Length];
        for (int i = 0; i < keys.Length; i++) interned[i] = Intern(keys[i]);
        return OfInterned(interned);
    }

    /// <summary><see cref="Of"/> for keys that are already interned (<see cref="Intern"/>).</summary>
    internal static CommerceLayout OfInterned(ReadOnlySpan<string> interned)
    {
        for (int i = 0; i < interned.Length; i++)
            for (int j = 0; j < i; j++)
                if (ReferenceEquals(interned[j], interned[i])) throw new InvalidDataException($"Commerce type '{interned[i]}' appears twice");
        return Find(interned) ?? Add(interned.ToArray());
    }

    [InlineArray(Capacity)]
    internal struct KeyBuffer { private string _first; }

    /// <summary>The interned instance of a key, so layouts compare keys by reference.</summary>
    internal static string Intern(ReadOnlySpan<char> key)
    {
        if (InternedKeySpans.TryGetValue(key, out var known)) return known;
        if (!CommerceTypes.IsValidKey(key)) throw new InvalidDataException($"'{key}' is not a commerce type key");
        // Literal keys in code ("gold") are interned by the runtime; sharing their instance makes lookups by literal
        // compare by reference.
        var text = string.Intern(key.ToString());
        return InternedKeys.GetOrAdd(text, text);
    }

    private static CommerceLayout? Find(ReadOnlySpan<string> interned)
    {
        foreach (var layout in _all)
        {
            if (layout._keys.Length != interned.Length) continue;
            bool same = true;
            for (int i = 0; i < interned.Length && same; i++) same = ReferenceEquals(layout._keys[i], interned[i]);
            if (same) return layout;
        }
        return null;
    }

    private static CommerceLayout Add(string[] interned)
    {
        lock (Gate)
        {
            if (Find(interned) is { } raced) return raced;
            var layout = new CommerceLayout(interned);
            _all = _all.Add(layout);
            return layout;
        }
    }

    /// <summary>How this layout and another combine: the layout of the result and, for each side, the result slot of
    /// each of its slots (null when the side already has the result's layout). Kept per pair, so combining vectors of
    /// two layouts allocates nothing after the first time.</summary>
    internal Merge MergeWith(CommerceLayout other)
    {
        foreach (var merge in _merges) if (ReferenceEquals(merge.Other, other)) return merge;
        var result = Contains(other) ? this : other.Contains(this) ? other : Union(other);
        var made = new Merge(other, result, ReferenceEquals(result, this) ? null : Map(this, result),
            ReferenceEquals(result, other) ? null : Map(other, result));
        lock (Gate)
        {
            foreach (var merge in _merges) if (ReferenceEquals(merge.Other, other)) return merge;
            _merges = [.. _merges, made];
        }
        return made;
    }

    private bool Contains(CommerceLayout other)
    {
        foreach (var key in other._keys) if (IndexOf(key) < 0) return false;
        return true;
    }

    private CommerceLayout Union(CommerceLayout other)
    {
        var keys = new List<string>(_keys);
        foreach (var key in other._keys) if (IndexOf(key) < 0) keys.Add(key);
        return Of(keys.ToArray());
    }

    private static byte[] Map(CommerceLayout from, CommerceLayout to)
    {
        var map = new byte[from.Count];
        for (int i = 0; i < map.Length; i++) map[i] = (byte)to.IndexOf(from._keys[i]);
        return map;
    }

    internal sealed record Merge(CommerceLayout Other, CommerceLayout Result, byte[]? ThisMap, byte[]? OtherMap);

    /// <inheritdoc/>
    public override string ToString() => string.Join(",", _keys);
}

/// <summary>Integer amounts by commerce type: a small fixed-capacity map from type key to amount. The vector carries
/// its keys (<see cref="Layout"/>), so it reads and writes JSON without knowing the database, and a key it does not
/// carry reads as zero: <c>{"gold": 5}</c> equals <c>{"gold": 5, "research": 0}</c>. A default vector carries no keys and is
/// zero. Percent math truncates toward zero after summing sources. Never allocates.</summary>
[JsonConverter(typeof(CommerceVectorConverter))]
public readonly struct CommerceVector : IEquatable<CommerceVector>
{
    [InlineArray(CommerceLayout.Capacity)]
    internal struct Amounts { private long _first; }

    private readonly CommerceLayout? _layout;
    private readonly Amounts _amounts;

    private CommerceVector(CommerceLayout? layout, in Amounts amounts) { _layout = layout; _amounts = amounts; }

    /// <summary>The keys this vector carries, or null for a default vector.</summary>
    public CommerceLayout? Layout => _layout;
    /// <summary>Number of keys carried.</summary>
    public int Count => _layout?.Count ?? 0;

    /// <summary>A zero vector carrying every key of <paramref name="layout"/>.</summary>
    public static CommerceVector Zero(CommerceLayout layout) => new(layout, default);
    /// <summary>A vector of these amounts in this layout's slots.</summary>
    public static CommerceVector Of(CommerceLayout layout, ReadOnlySpan<long> amounts)
    {
        if (amounts.Length != layout.Count) throw new ArgumentException($"Layout {layout} needs {layout.Count} amounts, not {amounts.Length}", nameof(amounts));
        Amounts values = default;
        amounts.CopyTo(values);
        return new(layout, values);
    }
    /// <summary>A vector of these keys and amounts, in this order.</summary>
    public static CommerceVector Of(params ReadOnlySpan<(string Key, long Amount)> parts)
    {
        if (parts.IsEmpty) return default;
        var keys = new string[parts.Length];
        Amounts values = default;
        for (int i = 0; i < parts.Length; i++) { keys[i] = parts[i].Key; values[i] = parts[i].Amount; }
        return new(CommerceLayout.Of(keys), values);
    }

    /// <summary>Amount in slot <paramref name="index"/> of <see cref="Layout"/>.</summary>
    public long At(int index) => _layout is { } layout && (uint)index < (uint)layout.Count
        ? _amounts[index] : throw new ArgumentOutOfRangeException(nameof(index));
    /// <summary>Amount of a type, zero when the vector does not carry it.</summary>
    public long this[string key] => _layout?.IndexOf(key) is int i and >= 0 ? _amounts[i] : 0;
    /// <summary>Amount of a loaded type; one index when the vector has the database's layout.</summary>
    public long this[CommerceType type] => ReferenceEquals(_layout, type.Layout) ? _amounts[type.Index] : this[type.Key];

    /// <summary>This vector with one type's amount replaced. A key the vector does not carry is added after its keys.</summary>
    public CommerceVector With(string key, long amount)
    {
        if (_layout?.IndexOf(key) is int i and >= 0)
        {
            var values = _amounts;
            values[i] = amount;
            return new(_layout, values);
        }
        return this + Of((key, amount));
    }
    /// <summary>This vector with one type's amount replaced.</summary>
    public CommerceVector With(CommerceType type, long amount)
    {
        if (!ReferenceEquals(_layout, type.Layout)) return With(type.Key, amount);
        var values = _amounts;
        values[type.Index] = amount;
        return new(_layout, values);
    }
    /// <summary>This vector with <paramref name="amount"/> added to one type, which it need not carry yet.</summary>
    public CommerceVector Plus(ReadOnlySpan<char> key, long amount)
    {
        if (_layout?.IndexOf(key) is int i and >= 0)
        {
            var values = _amounts;
            values[i] += amount;
            return new(_layout, values);
        }
        return this + Of((CommerceLayout.Intern(key), amount));
    }

    /// <summary>Whether every amount is zero.</summary>
    public bool IsZero
    {
        get
        {
            for (int i = 0; i < Count; i++) if (_amounts[i] != 0) return false;
            return true;
        }
    }

    /// <summary>Component-wise sum. The result carries the keys of both sides.</summary>
    public static CommerceVector operator +(CommerceVector a, CommerceVector b)
    {
        if (b._layout is null) return a;
        if (a._layout is null) return b;
        Amounts values;
        if (ReferenceEquals(a._layout, b._layout))
        {
            values = a._amounts;
            for (int i = 0; i < a._layout.Count; i++) values[i] += b._amounts[i];
            return new(a._layout, values);
        }
        var merge = a._layout.MergeWith(b._layout);
        values = default;
        Spread(a, merge.ThisMap, ref values);
        Spread(b, merge.OtherMap, ref values);
        return new(merge.Result, values);
    }
    private static void Spread(in CommerceVector from, byte[]? map, ref Amounts into)
    {
        int count = from.Count;
        if (map is null) for (int i = 0; i < count; i++) into[i] += from._amounts[i];
        else for (int i = 0; i < count; i++) into[map[i]] += from._amounts[i];
    }
    /// <summary>Integer scaling.</summary>
    public static CommerceVector operator *(CommerceVector a, long n)
    {
        var values = a._amounts;
        for (int i = 0; i < a.Count; i++) values[i] *= n;
        return new(a._layout, values);
    }
    /// <summary>Integer division of every amount, truncating toward zero (hundredths to whole points).</summary>
    public static CommerceVector operator /(CommerceVector a, long n)
    {
        var values = a._amounts;
        for (int i = 0; i < a.Count; i++) values[i] /= n;
        return new(a._layout, values);
    }
    /// <summary>Apply percentage bonuses component-wise; 25 means +25%. Multipliers clamp at zero, so reductions below
    /// -100% yield nothing. The result carries this vector's keys.</summary>
    public CommerceVector WithPercent(CommerceVector percent)
    {
        var values = _amounts;
        bool same = ReferenceEquals(_layout, percent._layout);
        for (int i = 0; i < Count; i++)
            values[i] = values[i] * Math.Max(0, 100 + (same ? percent._amounts[i] : percent[_layout![i]])) / 100;
        return new(_layout, values);
    }

    /// <summary>Equal when every type has the same amount; a key one side does not carry counts as zero.</summary>
    public bool Equals(CommerceVector other)
    {
        if (ReferenceEquals(_layout, other._layout))
        {
            for (int i = 0; i < Count; i++) if (_amounts[i] != other._amounts[i]) return false;
            return true;
        }
        for (int i = 0; i < Count; i++) if (_amounts[i] != other[_layout![i]]) return false;
        for (int i = 0; i < other.Count; i++) if (other._amounts[i] != this[other._layout![i]]) return false;
        return true;
    }
    /// <inheritdoc/>
    public override bool Equals(object? obj) => obj is CommerceVector other && Equals(other);
    /// <summary>Independent of key order and of zero amounts, as <see cref="Equals(CommerceVector)"/> is.</summary>
    public override int GetHashCode()
    {
        int hash = 0;
        for (int i = 0; i < Count; i++) if (_amounts[i] != 0) hash += HashCode.Combine(_layout![i], _amounts[i]);
        return hash;
    }
    /// <summary>Same amounts.</summary>
    public static bool operator ==(CommerceVector a, CommerceVector b) => a.Equals(b);
    /// <summary>Different amounts.</summary>
    public static bool operator !=(CommerceVector a, CommerceVector b) => !a.Equals(b);

    /// <summary>Keys and amounts in layout order, zeros included.</summary>
    public Enumerator GetEnumerator() => new(this);
    /// <summary>Enumerates (key, amount) pairs without allocating.</summary>
    public struct Enumerator(CommerceVector vector)
    {
        private int _index = -1;
        /// <summary>Current pair.</summary>
        public readonly (string Key, long Amount) Current => (vector._layout![_index], vector._amounts[_index]);
        /// <summary>Advance.</summary>
        public bool MoveNext() => ++_index < vector.Count;
    }
    /// <inheritdoc/>
    public override string ToString() => "{" + string.Join(", ", Pairs()) + "}";
    private IEnumerable<string> Pairs() { foreach (var (key, amount) in this) yield return key + ": " + amount; }
}

/// <summary>Writes a vector as an object of its keys, zeros included, named as the original four properties were
/// (<c>Gold</c>, or <c>gold</c> under a camel case policy), and reads any key case-insensitively.</summary>
public sealed class CommerceVectorConverter : JsonConverter<CommerceVector>
{
    /// <inheritdoc/>
    public override CommerceVector Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.StartObject) throw new JsonException("A commerce vector is an object of amounts by type");
        CommerceLayout.KeyBuffer buffer = default;
        Span<string> keys = buffer;
        Span<long> amounts = stackalloc long[CommerceLayout.Capacity];
        Span<char> name = stackalloc char[64];
        int count = 0;
        while (reader.Read() && reader.TokenType != JsonTokenType.EndObject)
        {
            if (reader.TokenType != JsonTokenType.PropertyName) throw new JsonException("Expected a commerce type key");
            if (count == CommerceLayout.Capacity) throw new JsonException($"A commerce vector holds at most {CommerceLayout.Capacity} types");
            if (reader.HasValueSequence || reader.ValueSpan.Length > name.Length) throw new JsonException("Commerce type key is too long");
            int length = reader.CopyString(name);
            for (int i = 0; i < length; i++) name[i] = char.ToLowerInvariant(name[i]);
            string key;
            try { key = CommerceLayout.Intern(name[..length]); }
            catch (InvalidDataException e) { throw new JsonException(e.Message, e); }
            for (int i = 0; i < count; i++)
                if (ReferenceEquals(keys[i], key)) throw new JsonException($"Commerce type '{key}' appears twice");
            reader.Read();
            keys[count] = key;
            amounts[count++] = reader.GetInt64();
        }
        return count == 0 ? default : CommerceVector.Of(CommerceLayout.OfInterned(keys[..count]), amounts[..count]);
    }
    /// <inheritdoc/>
    public override void Write(Utf8JsonWriter writer, CommerceVector value, JsonSerializerOptions options)
    {
        writer.WriteStartObject();
        if (value.Layout is { } layout)
        {
            var policy = options.PropertyNamingPolicy;
            for (int i = 0; i < layout.Count; i++)
            {
                writer.WritePropertyName(policy is null ? layout.JsonNames[i] : policy.ConvertName(layout.JsonNames[i]));
                writer.WriteNumberValue(value.At(i));
            }
        }
        writer.WriteEndObject();
    }
}
