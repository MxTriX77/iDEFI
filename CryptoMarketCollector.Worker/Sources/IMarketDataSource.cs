using CryptoMarketCollector.Worker.Models;

namespace CryptoMarketCollector.Worker.Sources;

public interface IMarketDataSource
{
    string Name { get; }

    Task<IReadOnlyList<MarketSnapshot>> CaptureAsync(CancellationToken cancellationToken);
}