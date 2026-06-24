namespace CryptoMarketCollector.Analytics.Api.Models;

public sealed record AnalyticsOverview(
    string Source,
    DateTimeOffset? LatestCollectionUtc,
    DateTimeOffset? PreviousCollectionUtc,
    int LatestSnapshotCount,
    IReadOnlyList<MarketAlert> Alerts,
    IReadOnlyList<MarketMover> TopMovers,
    IReadOnlyList<MarketVolumeLeader> TopVolumeLeaders
);

public sealed record MarketAlert(
    string Severity,
    string Symbol,
    string Title,
    string Description,
    decimal? Value
);

public sealed record MarketMover(
    string Symbol,
    decimal LatestPrice,
    decimal PreviousPrice,
    decimal PriceChangePercent,
    decimal? QuoteVolume24h,
    decimal? SpreadBps
);

public sealed record MarketVolumeLeader(
    string Symbol,
    decimal LastPrice,
    decimal QuoteVolume24h,
    decimal? SpreadBps
);