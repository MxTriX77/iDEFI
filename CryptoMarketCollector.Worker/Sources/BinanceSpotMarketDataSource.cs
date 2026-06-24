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
}