using Ariel.Content;

namespace Ariel.Game;

/// <summary>Canonical directed identifier for an active trade route originating from source to partner city.</summary>
public readonly record struct TradeRouteKey : IComparable<TradeRouteKey>
{
    public int SourceCity { get; }
    public int PartnerCity { get; }

    public TradeRouteKey(int sourceCity, int partnerCity)
    {
        SourceCity = sourceCity;
        PartnerCity = partnerCity;
    }

    public static TradeRouteKey Of(int sourceCity, int partnerCity) => new(sourceCity, partnerCity);

    public string Key => $"{SourceCity}->{PartnerCity}";

    public override string ToString() => Key;

    public CityPair Unordered => CityPair.Of(SourceCity, PartnerCity);

    public int CompareTo(TradeRouteKey other)
    {
        int cmp = SourceCity.CompareTo(other.SourceCity);
        return cmp != 0 ? cmp : PartnerCity.CompareTo(other.PartnerCity);
    }

    public static TradeRouteKey Parse(string key)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        int arrow = key.IndexOf("->", StringComparison.Ordinal);
        if (arrow < 0) throw new FormatException($"Invalid trade route key: {key}");
        return new(int.Parse(key.AsSpan(0, arrow)), int.Parse(key.AsSpan(arrow + 2)));
    }
}

/// <summary>Canonical unordered pair of two trading cities with First &lt;= Second.</summary>
public readonly record struct CityPair : IComparable<CityPair>
{
    public int First { get; }
    public int Second { get; }

    public CityPair(int a, int b)
    {
        First = Math.Min(a, b);
        Second = Math.Max(a, b);
    }

    public static CityPair Of(int a, int b) => new(a, b);

    public string Key => $"{First}<->{Second}";

    public override string ToString() => Key;

    public bool Contains(int city) => First == city || Second == city;

    public int CompareTo(CityPair other)
    {
        int cmp = First.CompareTo(other.First);
        return cmp != 0 ? cmp : Second.CompareTo(other.Second);
    }

    public static CityPair Parse(string key)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        int sep = key.IndexOf("<->", StringComparison.Ordinal);
        if (sep < 0) throw new FormatException($"Invalid city pair key: {key}");
        return new(int.Parse(key.AsSpan(0, sep)), int.Parse(key.AsSpan(sep + 3)));
    }
}

/// <summary>Route profit and yields retain hundredths until city route aggregation.</summary>
public sealed record TradeRouteResult(int PartnerCity, bool Foreign, bool Overseas, long ProfitHundredths,
    YieldVector YieldsHundredths, Ledger Ledger, int SourceCity = -1)
{
    public TradeRouteKey Key => TradeRouteKey.Of(SourceCity, PartnerCity);
    public CityPair Cities => CityPair.Of(SourceCity, PartnerCity);
}

public sealed record CityTradeResult(int Slots, IReadOnlyList<TradeRouteResult> Routes, YieldVector Yields, Ledger Ledger);
/// <summary>Active routes are derived from current connectivity. Queries are read-only and RNG-free.</summary>
public interface ITradeRouteService
{
    int MaxRoutes(Simulation sim, int city);
    CityTradeResult Calculate(Simulation sim, int city);
    TradeRouteResult CalculateProfit(Simulation sim, int city, int partnerCity);
    /// <summary>Whether an active directed trade route currently runs from source to partner city.</summary>
    bool HasRoute(Simulation sim, int sourceCity, int partnerCity);
    /// <summary>Whether an active trade route currently runs in either direction between two cities.</summary>
    bool HasBilateralRoute(Simulation sim, int cityA, int cityB);
    /// <summary>Active trade routes originating from all cities of a player.</summary>
    IReadOnlyList<TradeRouteResult> ActiveRoutes(Simulation sim, int player);
    /// <summary>All active trade routes originating across the entire simulation.</summary>
    IReadOnlyList<TradeRouteResult> AllActiveRoutes(Simulation sim);
}
