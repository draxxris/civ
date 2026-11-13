using System.Runtime.CompilerServices;
using Ariel.Content;
using Ariel.Content.Schema;
using Ariel.Game;

namespace Ariel.Mods.Religion;

/// <summary>Saved city presence; multiple religions can coexist.</summary>
public sealed class ReligionCityState
{
    public SortedSet<string> Religions { get; set; } = new(StringComparer.Ordinal);
}
/// <summary>Saved player choice. Secular civics suppress, but do not erase, the last choice.</summary>
public sealed class ReligionPlayerState
{
    public string? LastStateReligion { get; set; }
    public int ConversionTimer { get; set; }
}
/// <summary>Founding records survive destruction of a holy city; a religion cannot be founded twice.</summary>
public sealed class ReligionGameState
{
    public SortedDictionary<string, ReligionFounding> Founded { get; set; } = new(StringComparer.Ordinal);
}
public sealed record ReligionFounding(int HolyCity, int Turn);
public sealed record ReligionConversion(int AnarchyTurns, int CooldownTurns, Ledger Ledger);
public sealed record ReligionSpreadOdds(int Threshold, int RandomRange);
public sealed record ReligionFounded(string Religion, int City, int Player) : DomainEvent;
public sealed record ReligionSpread(string Religion, int City) : DomainEvent;
public sealed record ReligionRemoved(string Religion, int City, string Reason) : DomainEvent;
public sealed record InquisitionAttempt(int Unit, int City, int Player, bool Success, IReadOnlyList<string> RemovedReligions, long CompensationGold) : DomainEvent;
public sealed record StateReligionChanged(int Player, string? Religion) : DomainEvent;

/// <summary>The Religion mod's published service, for the client, the AI and other mods; read it with
/// <c>sim.Service&lt;IReligionService&gt;()</c>, null when the mod is not loaded. Queries never mutate state or consume
/// RNG.</summary>
public interface IReligionService
{
    string? StateReligion(Simulation sim, int player);
    IReadOnlyList<string> Present(Simulation sim, int city);
    int? HolyCity(Simulation sim, string religion);
    IReadOnlyList<ReligionDef> Choices(Simulation sim, int player);
    string? ValidateConversion(Simulation sim, int player, string? religion);
    ReligionConversion PreviewConversion(Simulation sim, int player);
    void Convert(Simulation sim, int player, string? religion);
    ReligionSpreadOdds NaturalSpreadOdds(Simulation sim, int city, string religion);
    int DecayChancePermille(Simulation sim, int city, string religion);
    bool IsInquisitionEnabled(Simulation sim, int player);
    bool HasInquisitionTargets(Simulation sim, int city);
}
public sealed class SetStateReligionCommand : GameCommand
{
    public string? Religion { get; init; }
    // Only the Religion mod registers this command, and it registers the service with it.
    public override string? Validate(Simulation sim) => sim.Service<IReligionService>()!.ValidateConversion(sim, Player, Religion);
    public override void Apply(Simulation sim) => sim.Service<IReligionService>()!.Convert(sim, Player, Religion);
}
/// <summary>The missions the Religion mod registers.</summary>
public static class ReligionMissions
{
    /// <summary>A missionary tries to bring its religion to the city. The subject is the religion id.</summary>
    public const string Spread = "religion:spread";
    /// <summary>An inquisitor tries to remove every religion but the state religion from the city. No subject.</summary>
    public const string Inquisition = "religion:inquisition";
    /// <summary>A prophet builds a religion's shrine in its holy city. The subject is the building id.</summary>
    public const string Shrine = "religion:shrine";
}

/// <summary>The memory kinds the Religion mod records (its <c>data/memory-kind.json</c>).</summary>
public static class ReligionMemories
{
    /// <summary>Remembered by followers of a religion an inquisition removed, and by owners of its holy city.</summary>
    public const string Inquisition = "religion:inquisition";
    /// <summary>Remembered by followers of a religion whose holy city was razed.</summary>
    public const string RazedHolyCity = "religion:razed_holy_city";
}

/// <summary>Translates between a religion's rules id (<see cref="ReligionDef.Id"/>, kept in saves) and the identity id
/// every other content record names it by (<see cref="ReligionDef.Identity"/>).</summary>
public static class ReligionIdentities
{
    private static readonly ConditionalWeakTable<ContentDb, Dictionary<string, ReligionDef>> Index = new();

    /// <summary>The religion with this identity, or null for none (or a null identity).</summary>
    public static ReligionDef? ByIdentity(ContentDb content, string? identity) =>
        identity is not null && Index.GetValue(content, Build).TryGetValue(identity, out var religion) ? religion : null;

    /// <summary>The rules id of the religion with this identity, or null.</summary>
    public static string? Religion(ContentDb content, string? identity) => ByIdentity(content, identity)?.Id;

    /// <summary>The identity of the religion with this rules id.</summary>
    public static string Identity(ContentDb content, string religion) => content.Get<ReligionDef>(religion).Identity;

    private static Dictionary<string, ReligionDef> Build(ContentDb content) =>
        content.Get<ReligionDef>().All.ToDictionary(r => r.Identity, StringComparer.Ordinal);
}
