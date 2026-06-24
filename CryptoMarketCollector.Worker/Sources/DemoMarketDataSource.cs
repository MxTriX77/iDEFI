using CryptoMarketCollector.Worker.Models;
using CryptoMarketCollector.Worker.Options;
using Microsoft.Extensions.Options;

namespace CryptoMarketCollector.Worker.Sources;

public sealed class DemoMarketDataSource : IMarketDataSource
{
    private readonly CollectorOptions _options;

    public string Name => "demo";

    public DemoMarketDataSource(IOptions<CollectorOptions> options)
    {
        _options = options.Value;
    }

    public Task<IReadOnlyList<MarketSnapshot>> CaptureAsync(
        CancellationToken cancellationToken)
    {
        if (!_options.Sources.Demo)
            return Task.FromResult<IReadOnlyList<MarketSnapshot>>([]);

        DateTimeOffset now = DateTimeOffset.UtcNow;

        IReadOnlyList<MarketSnapshot> snapshots =
        [
            new MarketSnapshot
            {
                Source = Name,
                SourceSymbol = "BTCUSDT",
                BaseAsset = "BTC",
                QuoteAsset = "USDT",
                LastPrice = 65000.12m,
                BidPrice = 64999.98m,
                AskPrice = 65000.25m,
                Volume24hBase = 12345.67m,
                Volume24hQuote = 802469135.12m,
                SourceTimestampUtc = now,
                CollectedAtUtc = now,
                RawJson = """
                {"symbol":"BTCUSDT","lastPrice":"65000.12","bidPrice":"64999.98","askPrice":"65000.25"}
                """
            },
            new MarketSnapshot
            {
                Source = Name,
                SourceSymbol = "ETHUSDT",
                BaseAsset = "ETH",
                QuoteAsset = "USDT",
                LastPrice = 3500.45m,
                BidPrice = 3500.40m,
                AskPrice = 3500.55m,
                Volume24hBase = 98765.43m,
                Volume24hQuote = 345679012.34m,
                SourceTimestampUtc = now,
                CollectedAtUtc = now,
                RawJson = """
                {"symbol":"ETHUSDT","lastPrice":"3500.45","bidPrice":"3500.40","askPrice":"3500.55"}
                """
            }
        ];

        return Task.FromResult(snapshots);
    }
}