/*using System.Globalization;
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
}*/
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

        await CreateBaseTableAsync(connection, cancellationToken);
        await EnsureColumnAsync(connection, "market_snapshots", "trading_status", "TEXT NULL", cancellationToken);

        await EnsureColumnAsync(connection, "market_snapshots", "price_change", "TEXT NULL", cancellationToken);
        await EnsureColumnAsync(connection, "market_snapshots", "price_change_percent", "TEXT NULL", cancellationToken);
        await EnsureColumnAsync(connection, "market_snapshots", "weighted_avg_price", "TEXT NULL", cancellationToken);
        await EnsureColumnAsync(connection, "market_snapshots", "previous_close_price", "TEXT NULL", cancellationToken);

        await EnsureColumnAsync(connection, "market_snapshots", "last_quantity", "TEXT NULL", cancellationToken);
        await EnsureColumnAsync(connection, "market_snapshots", "bid_quantity", "TEXT NULL", cancellationToken);
        await EnsureColumnAsync(connection, "market_snapshots", "ask_quantity", "TEXT NULL", cancellationToken);

        await EnsureColumnAsync(connection, "market_snapshots", "open_price", "TEXT NULL", cancellationToken);
        await EnsureColumnAsync(connection, "market_snapshots", "high_price", "TEXT NULL", cancellationToken);
        await EnsureColumnAsync(connection, "market_snapshots", "low_price", "TEXT NULL", cancellationToken);

        await EnsureColumnAsync(connection, "market_snapshots", "first_trade_id", "INTEGER NULL", cancellationToken);
        await EnsureColumnAsync(connection, "market_snapshots", "last_trade_id", "INTEGER NULL", cancellationToken);
        await EnsureColumnAsync(connection, "market_snapshots", "trade_count", "INTEGER NULL", cancellationToken);

        await EnsureColumnAsync(connection, "market_snapshots", "window_open_time_utc", "TEXT NULL", cancellationToken);

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
                trading_status,

                price_change,
                price_change_percent,
                weighted_avg_price,
                previous_close_price,

                last_price,
                last_quantity,

                bid_price,
                bid_quantity,

                ask_price,
                ask_quantity,

                open_price,
                high_price,
                low_price,

                spread,
                spread_bps,

                volume_24h_base,
                volume_24h_quote,
                market_cap_usd,

                first_trade_id,
                last_trade_id,
                trade_count,

                window_open_time_utc,
                source_timestamp_utc,
                collected_at_utc,

                raw_json
            )
            VALUES (
                $source,
                $source_symbol,
                $base_asset,
                $quote_asset,
                $trading_status,

                $price_change,
                $price_change_percent,
                $weighted_avg_price,
                $previous_close_price,

                $last_price,
                $last_quantity,

                $bid_price,
                $bid_quantity,

                $ask_price,
                $ask_quantity,

                $open_price,
                $high_price,
                $low_price,

                $spread,
                $spread_bps,

                $volume_24h_base,
                $volume_24h_quote,
                $market_cap_usd,

                $first_trade_id,
                $last_trade_id,
                $trade_count,

                $window_open_time_utc,
                $source_timestamp_utc,
                $collected_at_utc,

                $raw_json
            );
            """;

            command.Parameters.AddWithValue("$source", snapshot.Source);
            command.Parameters.AddWithValue("$source_symbol", snapshot.SourceSymbol);
            command.Parameters.AddWithValue("$base_asset", DbValue(snapshot.BaseAsset));
            command.Parameters.AddWithValue("$quote_asset", DbValue(snapshot.QuoteAsset));
            command.Parameters.AddWithValue("$trading_status", DbValue(snapshot.TradingStatus));

            command.Parameters.AddWithValue("$price_change", DbValue(snapshot.PriceChange));
            command.Parameters.AddWithValue("$price_change_percent", DbValue(snapshot.PriceChangePercent));
            command.Parameters.AddWithValue("$weighted_avg_price", DbValue(snapshot.WeightedAvgPrice));
            command.Parameters.AddWithValue("$previous_close_price", DbValue(snapshot.PreviousClosePrice));

            command.Parameters.AddWithValue("$last_price", DbValue(snapshot.LastPrice));
            command.Parameters.AddWithValue("$last_quantity", DbValue(snapshot.LastQuantity));

            command.Parameters.AddWithValue("$bid_price", DbValue(snapshot.BidPrice));
            command.Parameters.AddWithValue("$bid_quantity", DbValue(snapshot.BidQuantity));

            command.Parameters.AddWithValue("$ask_price", DbValue(snapshot.AskPrice));
            command.Parameters.AddWithValue("$ask_quantity", DbValue(snapshot.AskQuantity));

            command.Parameters.AddWithValue("$open_price", DbValue(snapshot.OpenPrice));
            command.Parameters.AddWithValue("$high_price", DbValue(snapshot.HighPrice));
            command.Parameters.AddWithValue("$low_price", DbValue(snapshot.LowPrice));

            command.Parameters.AddWithValue("$spread", DbValue(spread));
            command.Parameters.AddWithValue("$spread_bps", DbValue(spreadBps));

            command.Parameters.AddWithValue("$volume_24h_base", DbValue(snapshot.Volume24hBase));
            command.Parameters.AddWithValue("$volume_24h_quote", DbValue(snapshot.Volume24hQuote));
            command.Parameters.AddWithValue("$market_cap_usd", DbValue(snapshot.MarketCapUsd));

            command.Parameters.AddWithValue("$first_trade_id", DbValue(snapshot.FirstTradeId));
            command.Parameters.AddWithValue("$last_trade_id", DbValue(snapshot.LastTradeId));
            command.Parameters.AddWithValue("$trade_count", DbValue(snapshot.TradeCount));

            command.Parameters.AddWithValue(
                "$window_open_time_utc",
                DbValue(snapshot.WindowOpenTimeUtc?.UtcDateTime.ToString("O")));

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

    private static async Task CreateBaseTableAsync(
        SqliteConnection connection,
        CancellationToken cancellationToken)
    {
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

        CREATE INDEX IF NOT EXISTS ix_market_snapshots_quote_asset
        ON market_snapshots(source, quote_asset, collected_at_utc);
        """;

        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task EnsureColumnAsync(
        SqliteConnection connection,
        string tableName,
        string columnName,
        string columnDefinition,
        CancellationToken cancellationToken)
    {
        if (await ColumnExistsAsync(connection, tableName, columnName, cancellationToken))
            return;

        await using SqliteCommand command = connection.CreateCommand();

        command.CommandText =
            $"ALTER TABLE {tableName} ADD COLUMN {columnName} {columnDefinition};";

        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task<bool> ColumnExistsAsync(
        SqliteConnection connection,
        string tableName,
        string columnName,
        CancellationToken cancellationToken)
    {
        await using SqliteCommand command = connection.CreateCommand();

        command.CommandText = $"PRAGMA table_info({tableName});";

        await using SqliteDataReader reader =
            await command.ExecuteReaderAsync(cancellationToken);

        while (await reader.ReadAsync(cancellationToken))
        {
            string existingColumnName = reader.GetString(1);

            if (string.Equals(
                existingColumnName,
                columnName,
                StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
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

    private static object DbValue(long? value)
    {
        return value.HasValue
            ? value.Value
            : DBNull.Value;
    }
}