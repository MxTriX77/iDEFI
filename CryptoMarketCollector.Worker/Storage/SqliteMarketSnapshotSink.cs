using System.Globalization;
using CryptoMarketCollector.Worker.Models;
using CryptoMarketCollector.Worker.Options;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Options;

namespace CryptoMarketCollector.Worker.Storage;

public sealed class SqliteMarketSnapshotSink : IMarketSnapshotSink
{
    private readonly CollectorOptions _options;
    private readonly ILogger<SqliteMarketSnapshotSink> _logger;

    public SqliteMarketSnapshotSink(
        IOptions<CollectorOptions> options,
        ILogger<SqliteMarketSnapshotSink> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    public async Task InitializeAsync(CancellationToken cancellationToken)
    {
        string? directory = Path.GetDirectoryName(_options.SqlitePath);

        if (!string.IsNullOrWhiteSpace(directory))
            Directory.CreateDirectory(directory);

        await using SqliteConnection connection = CreateConnection();
        await connection.OpenAsync(cancellationToken);

        await using SqliteCommand command = connection.CreateCommand();

        command.CommandText = """
        CREATE TABLE IF NOT EXISTS market_snapshots (
            id INTEGER PRIMARY KEY AUTOINCREMENT,

            source TEXT NOT NULL,
            source_symbol TEXT NOT NULL,

            base_asset TEXT NULL,
            quote_asset TEXT NULL,

            last_price TEXT NULL,
            bid_price TEXT NULL,
            ask_price TEXT NULL,

            spread TEXT NULL,
            spread_bps TEXT NULL,

            volume_24h_base TEXT NULL,
            volume_24h_quote TEXT NULL,
            market_cap_usd TEXT NULL,

            source_timestamp_utc TEXT NULL,
            collected_at_utc TEXT NOT NULL,

            raw_json TEXT NULL
        );

        CREATE INDEX IF NOT EXISTS ix_market_snapshots_lookup
        ON market_snapshots(source, source_symbol, collected_at_utc);

        CREATE INDEX IF NOT EXISTS ix_market_snapshots_collected_at
        ON market_snapshots(collected_at_utc);
        """;

        await command.ExecuteNonQueryAsync(cancellationToken);

        _logger.LogInformation(
            "SQLite market snapshot sink initialized at {SqlitePath}.",
            _options.SqlitePath);
    }

    public async Task SaveAsync(
        IReadOnlyList<MarketSnapshot> snapshots,
        CancellationToken cancellationToken)
    {
        if (snapshots.Count == 0)
        {
            _logger.LogInformation("No market snapshots to save.");
            return;
        }

        await using SqliteConnection connection = CreateConnection();
        await connection.OpenAsync(cancellationToken);

        await using SqliteTransaction transaction =
            (SqliteTransaction)await connection.BeginTransactionAsync(cancellationToken);

        foreach (MarketSnapshot snapshot in snapshots)
        {
            decimal? spread = null;
            decimal? spreadBps = null;

            if (snapshot.AskPrice is > 0 && snapshot.BidPrice is > 0)
            {
                spread = snapshot.AskPrice - snapshot.BidPrice;

                if (snapshot.LastPrice is > 0)
                    spreadBps = spread / snapshot.LastPrice * 10_000m;
            }

            await using SqliteCommand command = connection.CreateCommand();
            command.Transaction = transaction;

            command.CommandText = """
            INSERT INTO market_snapshots (
                source,
                source_symbol,
                base_asset,
                quote_asset,
                last_price,
                bid_price,
                ask_price,
                spread,
                spread_bps,
                volume_24h_base,
                volume_24h_quote,
                market_cap_usd,
                source_timestamp_utc,
                collected_at_utc,
                raw_json
            )
            VALUES (
                $source,
                $source_symbol,
                $base_asset,
                $quote_asset,
                $last_price,
                $bid_price,
                $ask_price,
                $spread,
                $spread_bps,
                $volume_24h_base,
                $volume_24h_quote,
                $market_cap_usd,
                $source_timestamp_utc,
                $collected_at_utc,
                $raw_json
            );
            """;

            command.Parameters.AddWithValue("$source", snapshot.Source);
            command.Parameters.AddWithValue("$source_symbol", snapshot.SourceSymbol);
            command.Parameters.AddWithValue("$base_asset", DbValue(snapshot.BaseAsset));
            command.Parameters.AddWithValue("$quote_asset", DbValue(snapshot.QuoteAsset));

            command.Parameters.AddWithValue("$last_price", DbValue(snapshot.LastPrice));
            command.Parameters.AddWithValue("$bid_price", DbValue(snapshot.BidPrice));
            command.Parameters.AddWithValue("$ask_price", DbValue(snapshot.AskPrice));
            command.Parameters.AddWithValue("$spread", DbValue(spread));
            command.Parameters.AddWithValue("$spread_bps", DbValue(spreadBps));

            command.Parameters.AddWithValue("$volume_24h_base", DbValue(snapshot.Volume24hBase));
            command.Parameters.AddWithValue("$volume_24h_quote", DbValue(snapshot.Volume24hQuote));
            command.Parameters.AddWithValue("$market_cap_usd", DbValue(snapshot.MarketCapUsd));

            command.Parameters.AddWithValue(
                "$source_timestamp_utc",
                DbValue(snapshot.SourceTimestampUtc?.UtcDateTime.ToString("O")));

            command.Parameters.AddWithValue(
                "$collected_at_utc",
                snapshot.CollectedAtUtc.UtcDateTime.ToString("O"));

            command.Parameters.AddWithValue("$raw_json", DbValue(snapshot.RawJson));

            await command.ExecuteNonQueryAsync(cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);

        _logger.LogInformation(
            "Saved {Count} market snapshots to SQLite.",
            snapshots.Count);
    }

    private SqliteConnection CreateConnection()
    {
        return new SqliteConnection($"Data Source={_options.SqlitePath}");
    }

    private static object DbValue(string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? DBNull.Value
            : value;
    }

    private static object DbValue(decimal? value)
    {
        return value.HasValue
            ? value.Value.ToString(CultureInfo.InvariantCulture)
            : DBNull.Value;
    }
}