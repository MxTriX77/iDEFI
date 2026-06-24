using CryptoMarketCollector.Worker.Models;

namespace CryptoMarketCollector.Worker.Storage;

public sealed class ConsoleMarketSnapshotSink : IMarketSnapshotSink
{
    private readonly ILogger<ConsoleMarketSnapshotSink> _logger;

    public ConsoleMarketSnapshotSink(ILogger<ConsoleMarketSnapshotSink> logger)
    {
        _logger = logger;
    }

    public Task InitializeAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Console market snapshot sink initialized.");
        return Task.CompletedTask;
    }

    public Task SaveAsync(
        IReadOnlyList<MarketSnapshot> snapshots,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Saving {Count} market snapshots.", snapshots.Count);

        foreach (MarketSnapshot snapshot in snapshots.Take(10))
        {
            decimal? spread = null;
            decimal? spreadBps = null;

            if (snapshot.AskPrice is > 0 && snapshot.BidPrice is > 0)
            {
                spread = snapshot.AskPrice - snapshot.BidPrice;

                if (snapshot.LastPrice is > 0)
                    spreadBps = spread / snapshot.LastPrice * 10_000m;
            }

            _logger.LogInformation(
                "{Source} {Symbol}: last={LastPrice}, bid={BidPrice}, ask={AskPrice}, spread={Spread}, spreadBps={SpreadBps}",
                snapshot.Source,
                snapshot.SourceSymbol,
                snapshot.LastPrice,
                snapshot.BidPrice,
                snapshot.AskPrice,
                spread,
                spreadBps);
        }

        return Task.CompletedTask;
    }
}