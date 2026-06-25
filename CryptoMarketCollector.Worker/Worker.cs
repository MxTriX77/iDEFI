/*namespace CryptoMarketCollector.Worker;

public class Worker(ILogger<Worker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            if (logger.IsEnabled(LogLevel.Information))
            {
                logger.LogInformation("Worker running at: {time}", DateTimeOffset.Now);
            }
            await Task.Delay(1000, stoppingToken);
        }
    }
}*/
using CryptoMarketCollector.Worker.Models;
using CryptoMarketCollector.Worker.Options;
using CryptoMarketCollector.Worker.Sources;
using CryptoMarketCollector.Worker.Storage;
using Microsoft.Extensions.Options;

namespace CryptoMarketCollector.Worker;

public sealed class Worker : BackgroundService
{
    private readonly ILogger<Worker> _logger;
    private readonly IEnumerable<IMarketDataSource> _sources;
    private readonly IEnumerable<IKlineCollectionService> _klineCollectors;
    private readonly IMarketSnapshotSink _sink;
    private readonly CollectorOptions _options;
    private readonly IHostApplicationLifetime _hostApplicationLifetime;

    public Worker(
        ILogger<Worker> logger,
        IEnumerable<IMarketDataSource> sources,
        IEnumerable<IKlineCollectionService> klineCollectors,
        IMarketSnapshotSink sink,
        IOptions<CollectorOptions> options,
        IHostApplicationLifetime hostApplicationLifetime)
    {
        _logger = logger;
        _sources = sources;
        _klineCollectors = klineCollectors;
        _sink = sink;
        _options = options.Value;
        _hostApplicationLifetime = hostApplicationLifetime;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Crypto market collector started.");

        await _sink.InitializeAsync(stoppingToken);

        await RunOnceAsync(stoppingToken);

        if (_options.RunOnceAndExit)
        {
            _logger.LogInformation("RunOnceAndExit is enabled. Stopping application.");
            _hostApplicationLifetime.StopApplication();
            return;
        }

        using PeriodicTimer timer = new(
            TimeSpan.FromSeconds(_options.PollSeconds));

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await timer.WaitForNextTickAsync(stoppingToken);
                await RunOnceAsync(stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }

        _logger.LogInformation("Crypto market collector stopped.");
    }

    private async Task RunOnceAsync(CancellationToken cancellationToken)
    {
        DateTimeOffset startedAt = DateTimeOffset.UtcNow;

        foreach (IMarketDataSource source in _sources)
        {
            try
            {
                IReadOnlyList<MarketSnapshot> snapshots =
                    await source.CaptureAsync(cancellationToken);

                await _sink.SaveAsync(snapshots, cancellationToken);

                _logger.LogInformation(
                    "Captured {Count} snapshots from {Source} in {ElapsedMs} ms.",
                    snapshots.Count,
                    source.Name,
                    (DateTimeOffset.UtcNow - startedAt).TotalMilliseconds);
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Failed to capture market snapshots from {Source}.",
                    source.Name);
            }
        }

        foreach (IKlineCollectionService collector in _klineCollectors)
        {
            try
            {
                await collector.CaptureAsync(cancellationToken);

                _logger.LogInformation(
                    "Completed kline collector {Collector}.",
                    collector.Name);
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Failed to run kline collector {Collector}.",
                    collector.Name);
            }
        }
    }
}