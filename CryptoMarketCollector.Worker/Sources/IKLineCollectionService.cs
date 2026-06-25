namespace CryptoMarketCollector.Worker.Sources;

public interface IKlineCollectionService
{
    string Name { get; }

    Task CaptureAsync(CancellationToken cancellationToken);
}