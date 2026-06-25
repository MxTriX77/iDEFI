/*using CryptoMarketCollector.Worker;

var builder = Host.CreateApplicationBuilder(args);
builder.Services.AddHostedService<Worker>();

var host = builder.Build();
host.Run();
*/
using CryptoMarketCollector.Worker;
using CryptoMarketCollector.Worker.Options;
using CryptoMarketCollector.Worker.Sources;
using CryptoMarketCollector.Worker.Storage;

HostApplicationBuilder builder = Host.CreateApplicationBuilder(args);

builder.Services.Configure<CollectorOptions>(
    builder.Configuration.GetSection("Collector"));

builder.Services.AddHttpClient();

builder.Services.AddSingleton<IMarketDataSource, DemoMarketDataSource>();
builder.Services.AddSingleton<IMarketDataSource, BinanceSpotMarketDataSource>();
builder.Services.AddSingleton<IKlineCollectionService, BinanceKlineCollectionService>();
//builder.Services.AddSingleton<IMarketSnapshotSink, ConsoleMarketSnapshotSink>();
builder.Services.AddSingleton<IMarketSnapshotSink, SqliteMarketSnapshotSink>();

builder.Services.AddHostedService<Worker>();

IHost host = builder.Build();

await host.RunAsync();