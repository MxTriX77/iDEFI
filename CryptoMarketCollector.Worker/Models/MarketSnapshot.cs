namespace CryptoMarketCollector.Worker.Models;

public sealed record MarketSnapshot
{
	public required string Source {get; init;}
	public required string SourceSymbol {get ; init;}

	 public string? BaseAsset { get; init; }
    public string? QuoteAsset { get; init; }

    public decimal? LastPrice { get; init; }
    public decimal? BidPrice { get; init; }
    public decimal? AskPrice { get; init; }

    public decimal? Volume24hBase { get; init; }
    public decimal? Volume24hQuote { get; init; }
    public decimal? MarketCapUsd { get; init; }

    public DateTimeOffset? SourceTimestampUtc { get; init; }
    public required DateTimeOffset CollectedAtUtc { get; init; }

    public string? RawJson { get; init; }
}
