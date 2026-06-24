using System.Globalization;
using CryptoMarketCollector.Analytics.Api.Models;
using CryptoMarketCollector.Analytics.Api.Options;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Options;

namespace CryptoMarketCollector.Analytics.Api.Services;

public sealed class MarketAnalyticsService
{
    private readonly AnalyticsOptions _options;
    private readonly ILogger<MarketAnalyticsService> _logger;

    public MarketAnalyticsService(
        IOptions<AnalyticsOptions> options,
        ILogger<MarketAnalyticsService> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    public async Task<AnalyticsOverview> GetOverviewAsync(
        CancellationToken cancellationToken)
    {
        string fullPath = Path.GetFullPath(_options.SqlitePath);

        if (!File.Exists(fullPath))
        {
            _logger.LogWarning("SQLite database not found at {Path}", fullPath);

            return new AnalyticsOverview(
                Source: "binance_spot",
                LatestCollectionUtc: null,
                PreviousCollectionUtc: null,
                LatestSnapshotCount: 0,
                Alerts: [],
                TopMovers: [],
                TopVolumeLeaders: []);
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
            return new AnalyticsOverview(
                Source: "binance_spot",
                LatestCollectionUtc: null,
                PreviousCollectionUtc: null,
                LatestSnapshotCount: 0,
                Alerts: [],
                TopMovers: [],
                TopVolumeLeaders: []);
        }

        int latestCount =
            await GetSnapshotCountAsync(connection, latestTime, cancellationToken);

        IReadOnlyList<MarketVolumeLeader> volumeLeaders =
            await GetTopVolumeLeadersAsync(connection, latestTime, cancellationToken);

        IReadOnlyList<MarketMover> movers =
            previousTime is null
                ? []
                : await GetTopMoversAsync(connection, latestTime, previousTime, cancellationToken);

        IReadOnlyList<MarketAlert> alerts =
            BuildAlerts(movers, volumeLeaders);

        return new AnalyticsOverview(
            Source: "binance_spot",
            LatestCollectionUtc: ParseDateTimeOffset(latestTime),
            PreviousCollectionUtc: ParseDateTimeOffset(previousTime),
            LatestSnapshotCount: latestCount,
            Alerts: alerts,
            TopMovers: movers,
            TopVolumeLeaders: volumeLeaders);
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

    private static async Task<int> GetSnapshotCountAsync(
        SqliteConnection connection,
        string latestTime,
        CancellationToken cancellationToken)
    {
        await using SqliteCommand command = connection.CreateCommand();

        command.CommandText = """
        SELECT COUNT(*)
        FROM market_snapshots
        WHERE source = 'binance_spot'
          AND collected_at_utc = $latestTime;
        """;

        command.Parameters.AddWithValue("$latestTime", latestTime);

        object? result = await command.ExecuteScalarAsync(cancellationToken);

        return Convert.ToInt32(result, CultureInfo.InvariantCulture);
    }

    private static async Task<IReadOnlyList<MarketVolumeLeader>> GetTopVolumeLeadersAsync(
        SqliteConnection connection,
        string latestTime,
        CancellationToken cancellationToken)
    {
        await using SqliteCommand command = connection.CreateCommand();

        command.CommandText = """
        SELECT
            source_symbol,
            CAST(last_price AS REAL) AS last_price_num,
            CAST(volume_24h_quote AS REAL) AS volume_quote_num,
            CAST(spread_bps AS REAL) AS spread_bps_num
        FROM market_snapshots
        WHERE source = 'binance_spot'
          AND collected_at_utc = $latestTime
          AND volume_24h_quote IS NOT NULL
          AND last_price IS NOT NULL
        ORDER BY volume_quote_num DESC
        LIMIT 20;
        """;

        command.Parameters.AddWithValue("$latestTime", latestTime);

        List<MarketVolumeLeader> result = [];

        await using SqliteDataReader reader =
            await command.ExecuteReaderAsync(cancellationToken);

        while (await reader.ReadAsync(cancellationToken))
        {
            result.Add(new MarketVolumeLeader(
                Symbol: reader.GetString(0),
                LastPrice: Convert.ToDecimal(reader.GetDouble(1), CultureInfo.InvariantCulture),
                QuoteVolume24h: Convert.ToDecimal(reader.GetDouble(2), CultureInfo.InvariantCulture),
                SpreadBps: reader.IsDBNull(3)
                    ? null
                    : Convert.ToDecimal(reader.GetDouble(3), CultureInfo.InvariantCulture)));
        }

        return result;
    }

    private static async Task<IReadOnlyList<MarketMover>> GetTopMoversAsync(
        SqliteConnection connection,
        string latestTime,
        string previousTime,
        CancellationToken cancellationToken)
    {
        await using SqliteCommand command = connection.CreateCommand();

        command.CommandText = """
        WITH latest AS (
            SELECT
                source_symbol,
                CAST(last_price AS REAL) AS latest_price,
                CAST(volume_24h_quote AS REAL) AS quote_volume,
                CAST(spread_bps AS REAL) AS spread_bps
            FROM market_snapshots
            WHERE source = 'binance_spot'
              AND collected_at_utc = $latestTime
              AND last_price IS NOT NULL
        ),
        previous AS (
            SELECT
                source_symbol,
                CAST(last_price AS REAL) AS previous_price
            FROM market_snapshots
            WHERE source = 'binance_spot'
              AND collected_at_utc = $previousTime
              AND last_price IS NOT NULL
        )
        SELECT
            latest.source_symbol,
            latest.latest_price,
            previous.previous_price,
            ((latest.latest_price - previous.previous_price) / previous.previous_price) * 100.0 AS price_change_percent,
            latest.quote_volume,
            latest.spread_bps
        FROM latest
        INNER JOIN previous
            ON previous.source_symbol = latest.source_symbol
        WHERE previous.previous_price > 0
          AND latest.latest_price > 0
        ORDER BY ABS(price_change_percent) DESC
        LIMIT 30;
        """;

        command.Parameters.AddWithValue("$latestTime", latestTime);
        command.Parameters.AddWithValue("$previousTime", previousTime);

        List<MarketMover> result = [];

        await using SqliteDataReader reader =
            await command.ExecuteReaderAsync(cancellationToken);

        while (await reader.ReadAsync(cancellationToken))
        {
            result.Add(new MarketMover(
                Symbol: reader.GetString(0),
                LatestPrice: Convert.ToDecimal(reader.GetDouble(1), CultureInfo.InvariantCulture),
                PreviousPrice: Convert.ToDecimal(reader.GetDouble(2), CultureInfo.InvariantCulture),
                PriceChangePercent: Convert.ToDecimal(reader.GetDouble(3), CultureInfo.InvariantCulture),
                QuoteVolume24h: reader.IsDBNull(4)
                    ? null
                    : Convert.ToDecimal(reader.GetDouble(4), CultureInfo.InvariantCulture),
                SpreadBps: reader.IsDBNull(5)
                    ? null
                    : Convert.ToDecimal(reader.GetDouble(5), CultureInfo.InvariantCulture)));
        }

        return result;
    }

    private static IReadOnlyList<MarketAlert> BuildAlerts(
        IReadOnlyList<MarketMover> movers,
        IReadOnlyList<MarketVolumeLeader> volumeLeaders)
    {
        List<MarketAlert> alerts = [];

        foreach (MarketMover mover in movers.Take(20))
        {
            bool highVolume = mover.QuoteVolume24h is >= 10_000_000m;
            bool tightEnoughSpread = mover.SpreadBps is null or <= 50m;
            decimal absoluteMove = Math.Abs(mover.PriceChangePercent);

            if (absoluteMove >= 1.0m && highVolume && tightEnoughSpread)
            {
                alerts.Add(new MarketAlert(
                    Severity: absoluteMove >= 3.0m ? "critical" : "warning",
                    Symbol: mover.Symbol,
                    Title: "Fast move on meaningful volume",
                    Description: $"{mover.Symbol} moved {mover.PriceChangePercent:F2}% between the latest two scrapes with 24h quote volume around {mover.QuoteVolume24h:N0}.",
                    Value: mover.PriceChangePercent));
            }

            if (mover.SpreadBps is >= 100m)
            {
                alerts.Add(new MarketAlert(
                    Severity: "info",
                    Symbol: mover.Symbol,
                    Title: "Wide spread detected",
                    Description: $"{mover.Symbol} has a spread around {mover.SpreadBps:F2} bps. This may be illiquid or expensive to trade.",
                    Value: mover.SpreadBps));
            }
        }

        foreach (MarketVolumeLeader leader in volumeLeaders.Take(5))
        {
            alerts.Add(new MarketAlert(
                Severity: "info",
                Symbol: leader.Symbol,
                Title: "Top volume pair",
                Description: $"{leader.Symbol} is among the highest 24h quote-volume symbols in the latest snapshot.",
                Value: leader.QuoteVolume24h));
        }

        return alerts
            .Take(30)
            .ToList();
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
}