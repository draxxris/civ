using System.Collections;
using System.Numerics;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Civ.Game;

/// <summary>Compact team-id bitset with ascending enumeration. Teams have nonnegative ids.
/// Storage expands in 64-team blocks and trims empty trailing blocks. Dense team ids minimize memory.</summary>
[JsonConverter(typeof(TeamMaskConverter))]
public sealed class TeamMask : IEnumerable<int>
{
    private readonly List<ulong> _words = new();
    internal IReadOnlyList<ulong> Words => _words;
    /// <summary>Number of teams in this set.</summary>
    public int Count => _words.Sum(w => BitOperations.PopCount(w));
    /// <summary>Whether a nonnegative team id is present.</summary>
    public bool Contains(int team) => team >= 0 && team / 64 < _words.Count && (_words[team / 64] & (1UL << (team % 64))) != 0;
    /// <summary>Add a team id. Return true if membership changed.</summary>
    public bool Add(int team)
    {
        if (team < 0) throw new ArgumentOutOfRangeException(nameof(team));
        int word = team / 64;
        while (_words.Count <= word) _words.Add(0);
        ulong bit = 1UL << (team % 64);
        bool changed = (_words[word] & bit) == 0;
        _words[word] |= bit;
        return changed;
    }
    /// <summary>Remove a team id. Return true if membership changed.</summary>
    public bool Remove(int team)
    {
        if (!Contains(team)) return false;
        _words[team / 64] &= ~(1UL << (team % 64));
        Trim();
        return true;
    }
    /// <summary>Remove all memberships and release stored blocks.</summary>
    public void Clear() => _words.Clear();
    private void Trim() { while (_words.Count > 0 && _words[^1] == 0) _words.RemoveAt(_words.Count - 1); }
    internal static TeamMask FromWords(IEnumerable<ulong> words)
    {
        var mask = new TeamMask(); mask._words.AddRange(words); mask.Trim(); return mask;
    }
    /// <summary>Enumerate present team ids in ascending order.</summary>
    public IEnumerator<int> GetEnumerator()
    {
        for (int word = 0; word < _words.Count; word++)
        {
            ulong bits = _words[word];
            while (bits != 0)
            {
                yield return checked(word * 64 + BitOperations.TrailingZeroCount(bits));
                bits &= bits - 1;
            }
        }
    }
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}

/// <summary>Team mask JSON is an array of unsigned 64-bit blocks, with no trailing empty blocks.</summary>
public sealed class TeamMaskConverter : JsonConverter<TeamMask>
{
    /// <inheritdoc/>
    public override TeamMask Read(ref Utf8JsonReader reader, Type type, JsonSerializerOptions options) =>
        TeamMask.FromWords(JsonSerializer.Deserialize<ulong[]>(ref reader, options) ?? throw new JsonException("Empty team mask"));
    /// <inheritdoc/>
    public override void Write(Utf8JsonWriter writer, TeamMask value, JsonSerializerOptions options) =>
        JsonSerializer.Serialize(writer, value.Words.ToArray(), options);
}
