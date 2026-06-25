using System.Globalization;
using System.Text.Json;
using CryptoMarketCollector.Worker.Models;
using CryptoMarketCollector.Worker.Options;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Options;

namespace CryptoMarketCollector.Worker.Sources;

public sealed class BinanceKlineCollectionService : IKlineCollectionService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly CollectorOptions _options;
    private readonly ILogger<BinanceKlineCollectionService> _logger;

    public string Name => "binance_klines";

    public BinanceKlineCollectionService(
        IHttpClientFactory httpClientFactory,
        IOptions<CollectorOptions> options,
        ILogger<BinanceKlineCollectionService> logger)
    {
        _httpClientFactory = httpClientFactory;
        _options = options.Value;
        _logger = logger;
    }

    public async Task CaptureAsync(CancellationToken cancellationToken)
    {
        if (!_options.Sources.BinanceKlines)
            return;

        await EnsureSchemaAsync(cancellationToken);

        IReadOnlyList<CandidateSymbol> symbols =
            await GetCandidateSymbolsAsync(cancellationToken);

        if (symbols.Count == 0)
        {
            _logger.LogWarning(
                "No candidate symbols found for Binance kline collection. Run Binance ticker collection first.");

            return;
        }

        HttpClient client = _httpClientFactory.CreateClient();
        client.BaseAddress = new Uri(_options.BinanceSpot.BaseUrl);

        int savedRows = 0;
        DateTimeOffset collectedAt = DateTimeOffset.UtcNow;

        foreach (CandidateSymbol symbol in symbols)
        {
            foreach (string interval in _options.BinanceKlines.Intervals)
            {
                IReadOnlyList<BinanceKlineSnapshot> klines =
                    await FetchKlinesAsync(
                        client,
                        symbol,
                        interval,
                        collectedAt,
                        cancellationToken);

                await SaveKlinesAsync(klines, cancellationToken);
                savedRows += klines.Count;

                if (_options.BinanceKlines.DelayMsBetweenRequests > 0)
                {
                    await Task.Delay(
                        _options.BinanceKlines.DelayMsBetweenRequests,
                        cancellationToken);
                }
            }
        }

        _logger.LogInformation(
            "Saved {Count} Binance kline rows for {SymbolCount} symbols.",
            savedRows,
            symbols.Count);
    }

    private async Task<IReadOnlyList<BinanceKlineSnapshot>> FetchKlinesAsync(
        HttpClient client,
        CandidateSymbol symbol,
        string interval,
        DateTimeOffset collectedAt,
        CancellationToken cancellationToken)
    {
        string url =
            $"/api/v3/klines" +
            $"?symbol={Uri.EscapeDataString(symbol.Symbol)}" +
            $"&interval={Uri.EscapeDataString(interval)}" +
            $"&limit={_options.BinanceKlines.Limit}";

        using HttpResponseMessage response =
            await client.GetAsync(url, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            string errorBody =
                await response.Content.ReadAsStringAsync(cancellationToken);

            _logger.LogWarning(
                "Binance kline request failed for {Symbol} {Interval}. StatusCode={StatusCode}, Body={Body}",
                symbol.Symbol,
                interval,
                (int)response.StatusCode,
                errorBody);

            return [];
        }

        string json = await response.Content.ReadAsStringAsync(cancellationToken);

        using JsonDocument document = JsonDocument.Parse(json);

        List<BinanceKlineSnapshot> result = [];

        foreach (JsonElement row in document.RootElement.EnumerateArray())
        {
            if (row.ValueKind != JsonValueKind.Array || row.GetArrayLength() < 11)
                continue;

            long openTimeMs = row[0].GetInt64();
            long closeTimeMs = row[6].GetInt64();

            result.Add(new BinanceKlineSnapshot
            {
                Symbol = symbol.Symbol,
                BaseAsset = symbol.BaseAsset,
                QuoteAsset = symbol.QuoteAsset,
                Interval = interval,

                OpenTimeUtc = DateTimeOffset.FromUnixTimeMilliseconds(openTimeMs),
                CloseTimeUtc = DateTimeOffset.FromUnixTimeMilliseconds(closeTimeMs),

                OpenPrice = ReadDecimal(row[1]),
                HighPrice = ReadDecimal(row[2]),
                LowPrice = ReadDecimal(row[3]),
                ClosePrice = ReadDecimal(row[4]),

                VolumeBase = ReadDecimal(row[5]),
                VolumeQuote = ReadDecimal(row[7]),
                TradeCount = row[8].GetInt64(),
                TakerBuyBaseVolume = ReadDecimal(row[9]),
                TakerBuyQuoteVolume = ReadDecimal(row[10]),

                CollectedAtUtc = collectedAt,
                RawJson = row.GetRawText()
            });
        }

        return result;
    }

    private async Task<IReadOnlyList<CandidateSymbol>> GetCandidateSymbolsAsync(
        CancellationToken cancellationToken)
    {
        await using SqliteConnection connection = CreateConnection();
        await connection.OpenAsync(cancellationToken);

        await using SqliteCommand command = connection.CreateCommand();

        string quoteAssetParams = string.Join(
            ",",
            _options.BinanceKlines.QuoteAssets.Select((_, index) => $"$quoteAsset{index}"));

        command.CommandText = $"""
        WITH latest_time AS (
            SELECT MAX(collected_at_utc) AS collected_at_utc
            FROM market_snapshots
            WHERE source = 'binance_spot'
              AND quote_asset IN ({quoteAssetParams})
        )
        SELECT
            source_symbol,
            base_asset,
            quote_asset
        FROM market_snapshots
        WHERE source = 'binance_spot'
          AND collected_at_utc = (SELECT collected_at_utc FROM latest_time)
          AND quote_asset IN ({quoteAssetParams})
          AND (trading_status IS NULL OR trading_status = 'TRADING')
          AND last_price IS NOT NULL
          AND volume_24h_quote IS NOT NULL
          AND CAST(volume_24h_quote AS REAL) >= $minQuoteVolume
          AND (
              spread_bps IS NULL
              OR CAST(spread_bps AS REAL) <= $maxSpreadBps
          )
        ORDER BY CAST(volume_24h_quote AS REAL) DESC
        LIMIT $maxSymbols;
        """;

        for (int i = 0; i < _options.BinanceKlines.QuoteAssets.Length; i++)
        {
            command.Parameters.AddWithValue(
                $"$quoteAsset{i}",
                _options.BinanceKlines.QuoteAssets[i]);
        }

        command.Parameters.AddWithValue(
            "$minQuoteVolume",
            _options.BinanceKlines.MinQuoteVolume24h.ToString(CultureInfo.InvariantCulture));

        command.Parameters.AddWithValue(
            "$maxSpreadBps",
            _options.BinanceKlines.MaxSpreadBps.ToString(CultureInfo.InvariantCulture));

        command.Parameters.AddWithValue("$maxSymbols", _options.BinanceKlines.MaxSymbols);

        List<CandidateSymbol> result = [];

        await using SqliteDataReader reader =
            await command.ExecuteReaderAsync(cancellationToken);

        while (await reader.ReadAsync(cancellationToken))
        {
            result.Add(new CandidateSymbol(
                Symbol: reader.GetString(0),
                BaseAsset: reader.IsDBNull(1) ? null : reader.GetString(1),
                QuoteAsset: reader.IsDBNull(2) ? null : reader.GetString(2)));
        }

        return result;
    }

    private async Task EnsureSchemaAsync(CancellationToken cancellationToken)
    {
        await using SqliteConnection connection = CreateConnection();
        await connection.OpenAsync(cancellationToken);

        await using SqliteCommand command = connection.CreateCommand();

        command.CommandText = """
        CREATE TABLE IF NOT EXISTS binance_klines (
            id INTEGER PRIMARY KEY AUTOINCREMENT,

            symbol TEXT NOT NULL,
            base_asset TEXT NULL,
            quote_asset TEXT NULL,
            interval TEXT NOT NULL,

            open_time_utc TEXT NOT NULL,
            close_time_utc TEXT NOT NULL,

            open_price TEXT NOT NULL,
            high_price TEXT NOT NULL,
            low_price TEXT NOT NULL,
            close_price TEXT NOT NULL,

            volume_base TEXT NOT NULL,
            volume_quote TEXT NOT NULL,
            trade_count INTEGER NOT NULL,

            taker_buy_base_volume TEXT NOT NULL,
            taker_buy_quote_volume TEXT NOT NULL,

            collected_at_utc TEXT NOT NULL,
            raw_json TEXT NULL,

            UNIQUE(symbol, interval, open_time_utc)
        );

        CREATE INDEX IF NOT EXISTS ix_binance_klines_lookup
        ON binance_klines(symbol, interval, open_time_utc);

        CREATE INDEX IF NOT EXISTS ix_binance_klines_quote
        ON binance_klines(quote_asset, interval, open_time_utc);
        """;

        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private async Task SaveKlinesAsync(
        IReadOnlyList<BinanceKlineSnapshot> klines,
        CancellationToken cancellationToken)
    {
        if (klines.Count == 0)
            return;

        await using SqliteConnection connection = CreateConnection();
        await connection.OpenAsync(cancellationToken);

        await using SqliteTransaction transaction =
            (SqliteTransaction)await connection.BeginTransactionAsync(cancellationToken);

        foreach (BinanceKlineSnapshot kline in klines)
        {
            await using SqliteCommand command = connection.CreateCommand();
            command.Transaction = transaction;

            command.CommandText = """
            INSERT INTO binance_klines (
                symbol,
                base_asset,
                quote_asset,
                interval,
                open_time_utc,
                close_time_utc,
                open_price,
                high_price,
                low_price,
                close_price,
                volume_base,
                volume_quote,
                trade_count,
                taker_buy_base_volume,
                taker_buy_quote_volume,
                collected_at_utc,
                raw_json
            )
            VALUES (
                $symbol,
                $base_asset,
                $quote_asset,
                $interval,
                $open_time_utc,
                $close_time_utc,
                $open_price,
                $high_price,
                $low_price,
                $close_price,
                $volume_base,
                $volume_quote,
                $trade_count,
                $taker_buy_base_volume,
                $taker_buy_quote_volume,
                $collected_at_utc,
                $raw_json
            )
            ON CONFLICT(symbol, interval, open_time_utc)
            DO UPDATE SET
                close_time_utc = excluded.close_time_utc,
                open_price = excluded.open_price,
                high_price = excluded.high_price,
                low_price = excluded.low_price,
                close_price = excluded.close_price,
                volume_base = excluded.volume_base,
                volume_quote = excluded.volume_quote,
                trade_count = excluded.trade_count,
                taker_buy_base_volume = excluded.taker_buy_base_volume,
                taker_buy_quote_volume = excluded.taker_buy_quote_volume,
                collected_at_utc = excluded.collected_at_utc,
                raw_json = excluded.raw_json;
            """;

            command.Parameters.AddWithValue("$symbol", kline.Symbol);
            command.Parameters.AddWithValue("$base_asset", DbValue(kline.BaseAsset));
            command.Parameters.AddWithValue("$quote_asset", DbValue(kline.QuoteAsset));
            command.Parameters.AddWithValue("$interval", kline.Interval);

            command.Parameters.AddWithValue("$open_time_utc", kline.OpenTimeUtc.UtcDateTime.ToString("O"));
            command.Parameters.AddWithValue("$close_time_utc", kline.CloseTimeUtc.UtcDateTime.ToString("O"));

            command.Parameters.AddWithValue("$open_price", DbValue(kline.OpenPrice));
            command.Parameters.AddWithValue("$high_price", DbValue(kline.HighPrice));
            command.Parameters.AddWithValue("$low_price", DbValue(kline.LowPrice));
            command.Parameters.AddWithValue("$close_price", DbValue(kline.ClosePrice));

            command.Parameters.AddWithValue("$volume_base", DbValue(kline.VolumeBase));
            command.Parameters.AddWithValue("$volume_quote", DbValue(kline.VolumeQuote));
            command.Parameters.AddWithValue("$trade_count", kline.TradeCount);

            command.Parameters.AddWithValue("$taker_buy_base_volume", DbValue(kline.TakerBuyBaseVolume));
            command.Parameters.AddWithValue("$taker_buy_quote_volume", DbValue(kline.TakerBuyQuoteVolume));

            command.Parameters.AddWithValue("$collected_at_utc", kline.CollectedAtUtc.UtcDateTime.ToString("O"));
            command.Parameters.AddWithValue("$raw_json", DbValue(kline.RawJson));

            await command.ExecuteNonQueryAsync(cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
    }

    private SqliteConnection CreateConnection()
    {
        return new SqliteConnection($"Data Source={_options.SqlitePath}");
    }

    private static decimal ReadDecimal(JsonElement element)
    {
        return decimal.Parse(
            element.GetString() ?? "0",
            NumberStyles.Any,
            CultureInfo.InvariantCulture);
    }

    private static object DbValue(string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? DBNull.Value
            : value;
    }

    private static object DbValue(decimal value)
    {
        return value.ToString(CultureInfo.InvariantCulture);
    }

    private sealed record CandidateSymbol(
        string Symbol,
        string? BaseAsset,
        string? QuoteAsset);
}