using System.Globalization;
using CryptoMarketCollector.Analytics.Api.Models;
using CryptoMarketCollector.Analytics.Api.Options;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Options;

namespace CryptoMarketCollector.Analytics.Api.Services;

public sealed class DecisionSuggestionService
{
    private readonly AnalyticsOptions _options;
    private readonly ILogger<DecisionSuggestionService> _logger;

    public DecisionSuggestionService(
        IOptions<AnalyticsOptions> options,
        ILogger<DecisionSuggestionService> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    public async Task<DecisionSuggestionResponse> GetSuggestionsAsync(
        CancellationToken cancellationToken)
    {
        string fullPath = Path.GetFullPath(_options.SqlitePath);

        if (!File.Exists(fullPath))
        {
            _logger.LogWarning("SQLite database not found at {Path}", fullPath);

            return new DecisionSuggestionResponse(
                ScoredAtUtc: DateTimeOffset.UtcNow,
                LatestCollectionUtc: null,
                PreviousCollectionUtc: null,
                Suggestions: []);
        }

        await using SqliteConnection connection =
            new($"Data Source={fullPath};Mode=ReadOnly");

        await connection.OpenAsync(cancellationToken);

        List<string> collectionTimes =
            await GetLatestCollectionTimesAsync(connection, cancellationToken);

        string? latestTime = collectionTimes.ElementAtOrDefault(0);
        string? previousTime = collectionTimes.ElementAtOrDefault(1);

        if (latestTime is null)
        {
            return new DecisionSuggestionResponse(
                ScoredAtUtc: DateTimeOffset.UtcNow,
                LatestCollectionUtc: null,
                PreviousCollectionUtc: null,
                Suggestions: []);
        }

        IReadOnlyList<RawCandidate> candidates =
            await GetRawCandidatesAsync(
                connection,
                latestTime,
                previousTime,
                cancellationToken);

        IReadOnlyList<DecisionSuggestion> suggestions =
            candidates
                .Select(BuildSuggestion)
                .OrderByDescending(x => x.DecisionScore)
                .ThenBy(x => x.RiskScore)
                .Take(60)
                .ToList();

        return new DecisionSuggestionResponse(
            ScoredAtUtc: DateTimeOffset.UtcNow,
            LatestCollectionUtc: ParseDateTimeOffset(latestTime),
            PreviousCollectionUtc: ParseDateTimeOffset(previousTime),
            Suggestions: suggestions);
    }

    private static async Task<List<string>> GetLatestCollectionTimesAsync(
        SqliteConnection connection,
        CancellationToken cancellationToken)
    {
        await using SqliteCommand command = connection.CreateCommand();

        command.CommandText = """
        SELECT collected_at_utc
        FROM market_snapshots
        WHERE source = 'binance_spot'
          AND quote_asset IN ('USDT', 'USDC')
        GROUP BY collected_at_utc
        ORDER BY collected_at_utc DESC
        LIMIT 2;
        """;

        List<string> result = [];

        await using SqliteDataReader reader =
            await command.ExecuteReaderAsync(cancellationToken);

        while (await reader.ReadAsync(cancellationToken))
        {
            result.Add(reader.GetString(0));
        }

        return result;
    }

    private static async Task<IReadOnlyList<RawCandidate>> GetRawCandidatesAsync(
        SqliteConnection connection,
        string latestTime,
        string? previousTime,
        CancellationToken cancellationToken)
    {
        await using SqliteCommand command = connection.CreateCommand();

        command.CommandText = """
        WITH latest AS (
            SELECT
                source_symbol,
                base_asset,
                quote_asset,
                trading_status,

                CAST(last_price AS REAL) AS latest_price,
                CAST(price_change_percent AS REAL) AS price_change_24h_percent,
                CAST(spread_bps AS REAL) AS spread_bps,
                CAST(volume_24h_quote AS REAL) AS quote_volume_24h,
                trade_count,
                CAST(high_price AS REAL) AS high_price,
                CAST(low_price AS REAL) AS low_price
            FROM market_snapshots
            WHERE source = 'binance_spot'
              AND collected_at_utc = $latestTime
              AND quote_asset IN ('USDT', 'USDC')
              AND last_price IS NOT NULL
        ),
        previous AS (
            SELECT
                source_symbol,
                CAST(last_price AS REAL) AS previous_price
            FROM market_snapshots
            WHERE source = 'binance_spot'
              AND collected_at_utc = COALESCE($previousTime, $latestTime)
              AND quote_asset IN ('USDT', 'USDC')
              AND last_price IS NOT NULL
        )
        SELECT
            latest.source_symbol,
            latest.base_asset,
            latest.quote_asset,
            latest.trading_status,

            latest.latest_price,
            previous.previous_price,

            CASE
                WHEN previous.previous_price IS NULL OR previous.previous_price <= 0 THEN NULL
                ELSE ((latest.latest_price - previous.previous_price) / previous.previous_price) * 100.0
            END AS recent_move_percent,

            latest.price_change_24h_percent,
            latest.spread_bps,
            latest.quote_volume_24h,
            latest.trade_count,
            latest.high_price,
            latest.low_price
        FROM latest
        LEFT JOIN previous
            ON previous.source_symbol = latest.source_symbol
        WHERE latest.latest_price > 0
          AND (latest.trading_status IS NULL OR latest.trading_status = 'TRADING')
        ORDER BY latest.quote_volume_24h DESC
        LIMIT 1200;
        """;

        command.Parameters.AddWithValue("$latestTime", latestTime);
        command.Parameters.AddWithValue("$previousTime", previousTime is null ? DBNull.Value : previousTime);

        List<RawCandidate> result = [];

        await using SqliteDataReader reader =
            await command.ExecuteReaderAsync(cancellationToken);

        while (await reader.ReadAsync(cancellationToken))
        {
            result.Add(new RawCandidate(
                Symbol: reader.GetString(0),
                BaseAsset: ReadNullableString(reader, 1),
                QuoteAsset: ReadNullableString(reader, 2),
                TradingStatus: ReadNullableString(reader, 3),

                LatestPrice: ReadDecimal(reader, 4) ?? 0m,
                PreviousPrice: ReadDecimal(reader, 5),
                RecentMovePercent: ReadDecimal(reader, 6),

                PriceChange24hPercent: ReadDecimal(reader, 7),
                SpreadBps: ReadDecimal(reader, 8),
                QuoteVolume24h: ReadDecimal(reader, 9),
                TradeCount24h: ReadLong(reader, 10),
                HighPrice: ReadDecimal(reader, 11),
                LowPrice: ReadDecimal(reader, 12)));
        }

        return result;
    }

    private static DecisionSuggestion BuildSuggestion(RawCandidate candidate)
    {
        decimal liquidityScore = CalculateLiquidityScore(candidate);
        decimal momentumScore = CalculateMomentumScore(candidate);
        decimal crowdScore = CalculateCrowdScore(candidate);
        decimal riskScore = CalculateRiskScore(candidate);

        decimal decisionScore =
            Clamp(
                liquidityScore * 0.35m +
                momentumScore * 0.30m +
                crowdScore * 0.20m +
                (100m - riskScore) * 0.15m,
                0m,
                100m);

        string label = DecideLabel(
            candidate,
            decisionScore,
            liquidityScore,
            momentumScore,
            crowdScore,
            riskScore);

        (int? minMinutes, int? maxMinutes) = GetHorizon(label);

        return new DecisionSuggestion(
            Symbol: candidate.Symbol,
            BaseAsset: candidate.BaseAsset,
            QuoteAsset: candidate.QuoteAsset,

            DecisionLabel: label,
            DecisionScore: Math.Round(decisionScore, 2),

            SuggestedHorizonMinMinutes: minMinutes,
            SuggestedHorizonMaxMinutes: maxMinutes,

            LatestPrice: candidate.LatestPrice,
            RecentMovePercent: candidate.RecentMovePercent,
            PriceChange24hPercent: candidate.PriceChange24hPercent,
            SpreadBps: candidate.SpreadBps,
            QuoteVolume24h: candidate.QuoteVolume24h,
            TradeCount24h: candidate.TradeCount24h,

            LiquidityScore: Math.Round(liquidityScore, 2),
            MomentumScore: Math.Round(momentumScore, 2),
            CrowdScore: Math.Round(crowdScore, 2),
            RiskScore: Math.Round(riskScore, 2),

            PlainEnglishReason: BuildReason(candidate, label),
            PlainEnglishRisk: BuildRisk(candidate, label, riskScore),
            InvalidationNote: BuildInvalidationNote(label));
    }

    private static decimal CalculateLiquidityScore(RawCandidate candidate)
    {
        decimal volumeScore = candidate.QuoteVolume24h switch
        {
            >= 100_000_000m => 50m,
            >= 25_000_000m => 42m,
            >= 10_000_000m => 34m,
            >= 2_000_000m => 25m,
            >= 500_000m => 15m,
            >= 100_000m => 8m,
            _ => 2m
        };

        decimal spreadScore = candidate.SpreadBps switch
        {
            null => 10m,
            <= 5m => 50m,
            <= 15m => 42m,
            <= 30m => 34m,
            <= 60m => 22m,
            <= 100m => 10m,
            _ => 0m
        };

        return Clamp(volumeScore + spreadScore, 0m, 100m);
    }

    private static decimal CalculateMomentumScore(RawCandidate candidate)
    {
        decimal recent = candidate.RecentMovePercent ?? 0m;
        decimal day = candidate.PriceChange24hPercent ?? 0m;

        decimal recentScore = recent switch
        {
            >= 3.0m => 45m,
            >= 1.5m => 38m,
            >= 0.75m => 30m,
            >= 0.30m => 20m,
            > 0m => 10m,
            >= -0.25m => 4m,
            _ => 0m
        };

        decimal dayScore = day switch
        {
            >= 12m => 25m,
            >= 6m => 20m,
            >= 2m => 14m,
            >= 0.5m => 8m,
            >= -1m => 4m,
            _ => 0m
        };

        decimal notTooStretchedBonus =
            Math.Abs(day) <= 20m ? 20m : 5m;

        decimal directionBonus =
            recent > 0m && day > 0m ? 10m : 0m;

        return Clamp(recentScore + dayScore + notTooStretchedBonus + directionBonus, 0m, 100m);
    }

    private static decimal CalculateCrowdScore(RawCandidate candidate)
    {
        decimal tradeScore = candidate.TradeCount24h switch
        {
            >= 1_000_000 => 45m,
            >= 300_000 => 36m,
            >= 100_000 => 28m,
            >= 30_000 => 18m,
            >= 10_000 => 10m,
            >= 2_000 => 5m,
            _ => 1m
        };

        decimal volumeScore = candidate.QuoteVolume24h switch
        {
            >= 100_000_000m => 45m,
            >= 25_000_000m => 35m,
            >= 10_000_000m => 26m,
            >= 2_000_000m => 18m,
            >= 500_000m => 10m,
            _ => 3m
        };

        decimal freshMoveBonus =
            candidate.RecentMovePercent is >= 0.3m ? 10m : 0m;

        return Clamp(tradeScore + volumeScore + freshMoveBonus, 0m, 100m);
    }

    private static decimal CalculateRiskScore(RawCandidate candidate)
    {
        decimal risk = 0m;

        if (candidate.SpreadBps is null)
            risk += 15m;
        else if (candidate.SpreadBps > 150m)
            risk += 45m;
        else if (candidate.SpreadBps > 100m)
            risk += 35m;
        else if (candidate.SpreadBps > 60m)
            risk += 22m;
        else if (candidate.SpreadBps > 30m)
            risk += 10m;

        if (candidate.QuoteVolume24h is null or < 100_000m)
            risk += 35m;
        else if (candidate.QuoteVolume24h < 500_000m)
            risk += 25m;
        else if (candidate.QuoteVolume24h < 2_000_000m)
            risk += 12m;

        decimal rangePercent = CalculateRangePercent(candidate);

        if (rangePercent > 45m)
            risk += 25m;
        else if (rangePercent > 25m)
            risk += 15m;
        else if (rangePercent > 15m)
            risk += 8m;

        if (candidate.RecentMovePercent is > 5m)
            risk += 18m;

        if (candidate.PriceChange24hPercent is < -12m)
            risk += 18m;

        return Clamp(risk, 0m, 100m);
    }

    private static string DecideLabel(
        RawCandidate candidate,
        decimal decisionScore,
        decimal liquidityScore,
        decimal momentumScore,
        decimal crowdScore,
        decimal riskScore)
    {
        if (riskScore >= 70m ||
            liquidityScore < 25m ||
            candidate.SpreadBps is > 100m ||
            candidate.QuoteVolume24h is null or < 250_000m)
        {
            return "AVOID";
        }

        bool strongFreshMove =
            candidate.RecentMovePercent is >= 0.5m;

        bool strongMomentum =
            momentumScore >= 65m;

        bool strongCrowd =
            crowdScore >= 55m;

        bool veryLiquid =
            liquidityScore >= 70m;

        if (decisionScore >= 72m &&
            strongFreshMove &&
            strongCrowd &&
            riskScore <= 45m)
        {
            return "SCALP_CANDIDATE";
        }

        if (decisionScore >= 65m &&
            strongMomentum &&
            veryLiquid &&
            riskScore <= 40m)
        {
            return "MOMENTUM_CANDIDATE";
        }

        if (decisionScore >= 62m &&
            veryLiquid &&
            riskScore <= 30m &&
            candidate.PriceChange24hPercent is >= -2m and <= 8m)
        {
            return "HOLD_RESEARCH_CANDIDATE";
        }

        if (decisionScore >= 45m && riskScore < 60m)
            return "WATCH";

        return "AVOID";
    }

    private static (int? MinMinutes, int? MaxMinutes) GetHorizon(string label)
    {
        return label switch
        {
            "SCALP_CANDIDATE" => (5, 15),
            "MOMENTUM_CANDIDATE" => (15, 90),
            "HOLD_RESEARCH_CANDIDATE" => (240, 10_080),
            "WATCH" => (null, null),
            "AVOID" => (null, null),
            _ => (null, null)
        };
    }

    private static string BuildReason(RawCandidate candidate, string label)
    {
        string recentText = candidate.RecentMovePercent.HasValue
            ? $"{candidate.Symbol} moved {candidate.RecentMovePercent.Value:F2}% since the previous scrape"
            : $"{candidate.Symbol} does not have enough recent comparison data yet";

        string volumeText = candidate.QuoteVolume24h.HasValue
            ? $"24h quote volume is about {candidate.QuoteVolume24h.Value:N0}"
            : "24h volume is missing";

        string spreadText = candidate.SpreadBps.HasValue
            ? $"the spread is about {candidate.SpreadBps.Value:F2} bps"
            : "spread data is missing";

        return label switch
        {
            "SCALP_CANDIDATE" =>
                $"{recentText}. {volumeText}, and {spreadText}. In simple words: the coin just woke up, people are trading it, and the buy/sell door is not too expensive.",

            "MOMENTUM_CANDIDATE" =>
                $"{recentText}. The 24h move is {candidate.PriceChange24hPercent:F2}%, {volumeText}, and {spreadText}. In simple words: this looks less like a random twitch and more like a move with some follow-through.",

            "HOLD_RESEARCH_CANDIDATE" =>
                $"{volumeText}, and {spreadText}. In simple words: this is more of a liquid research candidate than a quick flip candidate.",

            "WATCH" =>
                $"{recentText}. {volumeText}, and {spreadText}. In simple words: something may be forming, but the evidence is not strong enough yet.",

            _ =>
                $"{volumeText}, and {spreadText}. In simple words: the market conditions are too weak, too expensive, or too noisy to treat this as interesting right now."
        };
    }

    private static string BuildRisk(
        RawCandidate candidate,
        string label,
        decimal riskScore)
    {
        List<string> risks = [];

        if (candidate.SpreadBps is null)
            risks.Add("spread is missing");
        else if (candidate.SpreadBps > 60m)
            risks.Add("spread is wide, so entering and exiting may be expensive");

        if (candidate.QuoteVolume24h is null or < 2_000_000m)
            risks.Add("volume is not strong enough for comfort");

        if (candidate.RecentMovePercent is > 3m)
            risks.Add("the move may already be late");

        if (CalculateRangePercent(candidate) > 25m)
            risks.Add("the 24h range is large, so the token may be very jumpy");

        if (risks.Count == 0)
            risks.Add("main risk is that the signal can fade or reverse");

        string prefix = label switch
        {
            "SCALP_CANDIDATE" => "This is a short-window idea, not a hold signal.",
            "MOMENTUM_CANDIDATE" => "This needs follow-through; if the move stalls, the idea weakens.",
            "HOLD_RESEARCH_CANDIDATE" => "This is not a quick-profit signal; it only says the pair is liquid enough to research.",
            "WATCH" => "This is not ready yet.",
            _ => "Avoid means the setup is not attractive right now."
        };

        return $"{prefix} Risk score is {riskScore:F0}/100 because {string.Join(", ", risks)}.";
    }

    private static string BuildInvalidationNote(string label)
    {
        return label switch
        {
            "SCALP_CANDIDATE" =>
                "Invalid if price stops moving upward, volume fades, or the spread widens. This signal should expire quickly.",

            "MOMENTUM_CANDIDATE" =>
                "Invalid if the next snapshots stop making progress or volume drops while price goes sideways.",

            "HOLD_RESEARCH_CANDIDATE" =>
                "Invalid if liquidity dries up, spread widens, or the pair starts underperforming the broader market.",

            "WATCH" =>
                "Needs more evidence: better volume, cleaner spread, or stronger movement.",

            _ =>
                "Avoid until liquidity, spread, and movement improve."
        };
    }

    private static decimal CalculateRangePercent(RawCandidate candidate)
    {
        if (candidate.HighPrice is null ||
            candidate.LowPrice is null ||
            candidate.LatestPrice <= 0m)
        {
            return 0m;
        }

        return Math.Abs(candidate.HighPrice.Value - candidate.LowPrice.Value) /
            candidate.LatestPrice *
            100m;
    }

    private static decimal Clamp(decimal value, decimal min, decimal max)
    {
        return Math.Max(min, Math.Min(max, value));
    }

    private static string? ReadNullableString(SqliteDataReader reader, int ordinal)
    {
        return reader.IsDBNull(ordinal)
            ? null
            : reader.GetString(ordinal);
    }

    private static decimal? ReadDecimal(SqliteDataReader reader, int ordinal)
    {
        return reader.IsDBNull(ordinal)
            ? null
            : Convert.ToDecimal(reader.GetDouble(ordinal), CultureInfo.InvariantCulture);
    }

    private static long? ReadLong(SqliteDataReader reader, int ordinal)
    {
        return reader.IsDBNull(ordinal)
            ? null
            : reader.GetInt64(ordinal);
    }

    private static DateTimeOffset? ParseDateTimeOffset(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        return DateTimeOffset.TryParse(
            value,
            CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal,
            out DateTimeOffset parsed)
                ? parsed.ToUniversalTime()
                : null;
    }

    private sealed record RawCandidate(
        string Symbol,
        string? BaseAsset,
        string? QuoteAsset,
        string? TradingStatus,

        decimal LatestPrice,
        decimal? PreviousPrice,
        decimal? RecentMovePercent,

        decimal? PriceChange24hPercent,
        decimal? SpreadBps,
        decimal? QuoteVolume24h,
        long? TradeCount24h,
        decimal? HighPrice,
        decimal? LowPrice
    );
}