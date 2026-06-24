using CryptoMarketCollector.Worker.Models;

namespace CryptoMarketCollector.Worker.Storage;

public interface IMarketSnapshotSink
{
    Task InitializeAsync(CancellationToken cancellationToken);

    Task SaveAsync(
        IReadOnlyList<MarketSnapshot> snapshots,
        CancellationToken cancellationToken);
}