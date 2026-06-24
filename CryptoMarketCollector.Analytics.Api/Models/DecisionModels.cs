namespace CryptoMarketCollector.Analytics.Api.Models;

public sealed record DecisionSuggestionResponse(
    DateTimeOffset? ScoredAtUtc,
    DateTimeOffset? LatestCollectionUtc,
    DateTimeOffset? PreviousCollectionUtc,
    IReadOnlyList<DecisionSuggestion> Suggestions
);

public sealed record DecisionSuggestion(
    string Symbol,
    string? BaseAsset,
    string? QuoteAsset,

    string DecisionLabel,
    decimal DecisionScore,

    int? SuggestedHorizonMinMinutes,
    int? SuggestedHorizonMaxMinutes,

    decimal LatestPrice,
    decimal? RecentMovePercent,
    decimal? PriceChange24hPercent,
    decimal? SpreadBps,
    decimal? QuoteVolume24h,
    long? TradeCount24h,

    decimal LiquidityScore,
    decimal MomentumScore,
    decimal CrowdScore,
    decimal RiskScore,

    string PlainEnglishReason,
    string PlainEnglishRisk,
    string InvalidationNote
);