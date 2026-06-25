namespace CryptoMarketCollector.Worker.Models;

public sealed record BinanceKlineSnapshot
{
    public required string Symbol { get; init; }

    public string? BaseAsset { get; init; }

    public string? QuoteAsset { get; init; }

    public required string Interval { get; init; }

    public required DateTimeOffset OpenTimeUtc { get; init; }

    public required DateTimeOffset CloseTimeUtc { get; init; }

    public decimal OpenPrice { get; init; }

    public decimal HighPrice { get; init; }

    public decimal LowPrice { get; init; }

    public decimal ClosePrice { get; init; }

    public decimal VolumeBase { get; init; }

    public decimal VolumeQuote { get; init; }

    public long TradeCount { get; init; }

    public decimal TakerBuyBaseVolume { get; init; }

    public decimal TakerBuyQuoteVolume { get; init; }

    public required DateTimeOffset CollectedAtUtc { get; init; }

    public string? RawJson { get; init; }
}