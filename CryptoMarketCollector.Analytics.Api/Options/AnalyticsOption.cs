namespace CryptoMarketCollector.Analytics.Api.Options;

public sealed class AnalyticsOptions
{
  public string SqlitePath { get; set; } = "../CryptoMarketCollector.Worker/data/marketdata.db";
}
