/*using System.Globalization;
using System.Text.Json;
using CryptoMarketCollector.Worker.Models;
using CryptoMarketCollector.Worker.Options;
using Microsoft.Extensions.Options;

namespace CryptoMarketCollector.Worker.Sources;

public sealed class BinanceSpotMarketDataSource : IMarketDataSource
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly CollectorOptions _options;
    private readonly ILogger<BinanceSpotMarketDataSource> _logger;

    public string Name => "binance_spot";

    public BinanceSpotMarketDataSource(
        IHttpClientFactory httpClientFactory,
        IOptions<CollectorOptions> options,
        ILogger<BinanceSpotMarketDataSource> logger)
    {
        _httpClientFactory = httpClientFactory;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<IReadOnlyList<MarketSnapshot>> CaptureAsync(
        CancellationToken cancellationToken)
    {
        if (!_options.Sources.BinanceSpot)
            return [];

        HttpClient client = _httpClientFactory.CreateClient();
        client.BaseAddress = new Uri(_options.BinanceSpot.BaseUrl);

        using HttpResponseMessage response =
            await client.GetAsync("/api/v3/ticker/24hr", cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            string errorBody =
                await response.Content.ReadAsStringAsync(cancellationToken);

            _logger.LogWarning(
                "Binance Spot request failed. StatusCode={StatusCode}, Body={Body}",
                (int)response.StatusCode,
                errorBody);

            response.EnsureSuccessStatusCode();
        }

        string json = await response.Content.ReadAsStringAsync(cancellationToken);

        using JsonDocument document = JsonDocument.Parse(json);

        DateTimeOffset collectedAt = DateTimeOffset.UtcNow;
        List<MarketSnapshot> snapshots = new();

        foreach (JsonElement item in document.RootElement.EnumerateArray())
        {
            string? symbol = ReadString(item, "symbol");

            if (string.IsNullOrWhiteSpace(symbol))
                continue;

            DateTimeOffset? closeTime = null;

            if (item.TryGetProperty("closeTime", out JsonElement closeTimeElement) &&
                closeTimeElement.TryGetInt64(out long closeTimeMs))
            {
                closeTime = DateTimeOffset.FromUnixTimeMilliseconds(closeTimeMs);
            }

            snapshots.Add(new MarketSnapshot
            {
                Source = Name,
                SourceSymbol = symbol,

                LastPrice = ReadDecimal(item, "lastPrice"),
                BidPrice = ReadDecimal(item, "bidPrice"),
                AskPrice = ReadDecimal(item, "askPrice"),

                Volume24hBase = ReadDecimal(item, "volume"),
                Volume24hQuote = ReadDecimal(item, "quoteVolume"),

                SourceTimestampUtc = closeTime,
                CollectedAtUtc = collectedAt,

                RawJson = item.GetRawText()
            });
        }

        return snapshots;
    }

    private static string? ReadString(JsonElement item, string propertyName)
    {
        return item.TryGetProperty(propertyName, out JsonElement element)
            ? element.GetString()
            : null;
    }

    private static decimal? ReadDecimal(JsonElement item, string propertyName)
    {
        if (!item.TryGetProperty(propertyName, out JsonElement element))
            return null;

        if (element.ValueKind == JsonValueKind.Number &&
            element.TryGetDecimal(out decimal numberValue))
        {
            return numberValue;
        }

        if (element.ValueKind == JsonValueKind.String &&
            decimal.TryParse(
                element.GetString(),
                NumberStyles.Any,
                CultureInfo.InvariantCulture,
                out decimal stringValue))
        {
            return stringValue;
        }

        return null;
    }
}*/
using System.Globalization;
using System.Text.Json;
using CryptoMarketCollector.Worker.Models;
using CryptoMarketCollector.Worker.Options;
using Microsoft.Extensions.Options;

namespace CryptoMarketCollector.Worker.Sources;

public sealed class BinanceSpotMarketDataSource : IMarketDataSource
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly CollectorOptions _options;
    private readonly ILogger<BinanceSpotMarketDataSource> _logger;

    private IReadOnlyDictionary<string, BinanceSymbolInfo>? _symbolInfoBySymbol;
    private DateTimeOffset _symbolInfoLoadedAtUtc = DateTimeOffset.MinValue;

    public string Name => "binance_spot";

    public BinanceSpotMarketDataSource(
        IHttpClientFactory httpClientFactory,
        IOptions<CollectorOptions> options,
        ILogger<BinanceSpotMarketDataSource> logger)
    {
        _httpClientFactory = httpClientFactory;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<IReadOnlyList<MarketSnapshot>> CaptureAsync(
        CancellationToken cancellationToken)
    {
        if (!_options.Sources.BinanceSpot)
            return [];

        HttpClient client = _httpClientFactory.CreateClient();
        client.BaseAddress = new Uri(_options.BinanceSpot.BaseUrl);

        IReadOnlyDictionary<string, BinanceSymbolInfo> symbolInfoBySymbol =
            await GetSymbolInfoBySymbolAsync(client, cancellationToken);

        using HttpResponseMessage response =
            await client.GetAsync("/api/v3/ticker/24hr", cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            string errorBody =
                await response.Content.ReadAsStringAsync(cancellationToken);

            _logger.LogWarning(
                "Binance Spot ticker request failed. StatusCode={StatusCode}, Body={Body}",
                (int)response.StatusCode,
                errorBody);

            response.EnsureSuccessStatusCode();
        }

        string json = await response.Content.ReadAsStringAsync(cancellationToken);

        using JsonDocument document = JsonDocument.Parse(json);

        DateTimeOffset collectedAt = DateTimeOffset.UtcNow;
        List<MarketSnapshot> snapshots = new();

        foreach (JsonElement item in document.RootElement.EnumerateArray())
        {
            string? symbol = ReadString(item, "symbol");

            if (string.IsNullOrWhiteSpace(symbol))
                continue;

            symbolInfoBySymbol.TryGetValue(symbol, out BinanceSymbolInfo? symbolInfo);

            snapshots.Add(new MarketSnapshot
            {
                Source = Name,
                SourceSymbol = symbol,

                BaseAsset = symbolInfo?.BaseAsset,
                QuoteAsset = symbolInfo?.QuoteAsset,
                TradingStatus = symbolInfo?.Status,

                PriceChange = ReadDecimal(item, "priceChange"),
                PriceChangePercent = ReadDecimal(item, "priceChangePercent"),
                WeightedAvgPrice = ReadDecimal(item, "weightedAvgPrice"),
                PreviousClosePrice = ReadDecimal(item, "prevClosePrice"),

                LastPrice = ReadDecimal(item, "lastPrice"),
                LastQuantity = ReadDecimal(item, "lastQty"),

                BidPrice = ReadDecimal(item, "bidPrice"),
                BidQuantity = ReadDecimal(item, "bidQty"),

                AskPrice = ReadDecimal(item, "askPrice"),
                AskQuantity = ReadDecimal(item, "askQty"),

                OpenPrice = ReadDecimal(item, "openPrice"),
                HighPrice = ReadDecimal(item, "highPrice"),
                LowPrice = ReadDecimal(item, "lowPrice"),

                Volume24hBase = ReadDecimal(item, "volume"),
                Volume24hQuote = ReadDecimal(item, "quoteVolume"),

                FirstTradeId = ReadLong(item, "firstId"),
                LastTradeId = ReadLong(item, "lastId"),
                TradeCount = ReadLong(item, "count"),

                WindowOpenTimeUtc = ReadUnixMilliseconds(item, "openTime"),
                SourceTimestampUtc = ReadUnixMilliseconds(item, "closeTime"),
                CollectedAtUtc = collectedAt,

                RawJson = item.GetRawText()
            });
        }

        return snapshots;
    }

    private async Task<IReadOnlyDictionary<string, BinanceSymbolInfo>> GetSymbolInfoBySymbolAsync(
        HttpClient client,
        CancellationToken cancellationToken)
    {
        bool cacheFresh =
            _symbolInfoBySymbol is not null &&
            DateTimeOffset.UtcNow - _symbolInfoLoadedAtUtc < TimeSpan.FromHours(12);

        if (cacheFresh)
            return _symbolInfoBySymbol!;

        using HttpResponseMessage response =
            await client.GetAsync("/api/v3/exchangeInfo", cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            string errorBody =
                await response.Content.ReadAsStringAsync(cancellationToken);

            _logger.LogWarning(
                "Binance exchangeInfo request failed. StatusCode={StatusCode}, Body={Body}",
                (int)response.StatusCode,
                errorBody);

            response.EnsureSuccessStatusCode();
        }

        string json = await response.Content.ReadAsStringAsync(cancellationToken);

        using JsonDocument document = JsonDocument.Parse(json);

        Dictionary<string, BinanceSymbolInfo> result =
            new(StringComparer.OrdinalIgnoreCase);

        if (document.RootElement.TryGetProperty("symbols", out JsonElement symbolsElement))
        {
            foreach (JsonElement symbolElement in symbolsElement.EnumerateArray())
            {
                string? symbol = ReadString(symbolElement, "symbol");

                if (string.IsNullOrWhiteSpace(symbol))
                    continue;

                result[symbol] = new BinanceSymbolInfo(
                    Symbol: symbol,
                    BaseAsset: ReadString(symbolElement, "baseAsset"),
                    QuoteAsset: ReadString(symbolElement, "quoteAsset"),
                    Status: ReadString(symbolElement, "status"));
            }
        }

        _symbolInfoBySymbol = result;
        _symbolInfoLoadedAtUtc = DateTimeOffset.UtcNow;

        _logger.LogInformation(
            "Loaded {Count} Binance symbol metadata rows.",
            result.Count);

        return result;
    }

    private static string? ReadString(JsonElement item, string propertyName)
    {
        return item.TryGetProperty(propertyName, out JsonElement element)
            ? element.GetString()
            : null;
    }

    private static decimal? ReadDecimal(JsonElement item, string propertyName)
    {
        if (!item.TryGetProperty(propertyName, out JsonElement element))
            return null;

        if (element.ValueKind == JsonValueKind.Number &&
            element.TryGetDecimal(out decimal numberValue))
        {
            return numberValue;
        }

        if (element.ValueKind == JsonValueKind.String &&
            decimal.TryParse(
                element.GetString(),
                NumberStyles.Any,
                CultureInfo.InvariantCulture,
                out decimal stringValue))
        {
            return stringValue;
        }

        return null;
    }

    private static long? ReadLong(JsonElement item, string propertyName)
    {
        if (!item.TryGetProperty(propertyName, out JsonElement element))
            return null;

        if (element.ValueKind == JsonValueKind.Number &&
            element.TryGetInt64(out long numberValue))
        {
            return numberValue;
        }

        if (element.ValueKind == JsonValueKind.String &&
            long.TryParse(
                element.GetString(),
                NumberStyles.Any,
                CultureInfo.InvariantCulture,
                out long stringValue))
        {
            return stringValue;
        }

        return null;
    }

    private static DateTimeOffset? ReadUnixMilliseconds(
        JsonElement item,
        string propertyName)
    {
        long? milliseconds = ReadLong(item, propertyName);

        return milliseconds.HasValue
            ? DateTimeOffset.FromUnixTimeMilliseconds(milliseconds.Value)
            : null;
    }

    private sealed record BinanceSymbolInfo(
        string Symbol,
        string? BaseAsset,
        string? QuoteAsset,
        string? Status);
}