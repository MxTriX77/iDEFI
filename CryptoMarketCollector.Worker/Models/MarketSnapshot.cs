/*namespace CryptoMarketCollector.Worker.Models;

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
*/
namespace CryptoMarketCollector.Worker.Models;

public sealed record MarketSnapshot
{
    public required string Source { get; init; }
    public required string SourceSymbol { get; init; }

    public string? BaseAsset { get; init; }
    public string? QuoteAsset { get; init; }
    public string? TradingStatus { get; init; }

    public decimal? PriceChange { get; init; }
    public decimal? PriceChangePercent { get; init; }
    public decimal? WeightedAvgPrice { get; init; }
    public decimal? PreviousClosePrice { get; init; }

    public decimal? LastPrice { get; init; }
    public decimal? LastQuantity { get; init; }

    public decimal? BidPrice { get; init; }
    public decimal? BidQuantity { get; init; }

    public decimal? AskPrice { get; init; }
    public decimal? AskQuantity { get; init; }

    public decimal? OpenPrice { get; init; }
    public decimal? HighPrice { get; init; }
    public decimal? LowPrice { get; init; }

    public decimal? Volume24hBase { get; init; }
    public decimal? Volume24hQuote { get; init; }
    public decimal? MarketCapUsd { get; init; }

    public long? FirstTradeId { get; init; }
    public long? LastTradeId { get; init; }
    public long? TradeCount { get; init; }

    public DateTimeOffset? WindowOpenTimeUtc { get; init; }
    public DateTimeOffset? SourceTimestampUtc { get; init; }
    public required DateTimeOffset CollectedAtUtc { get; init; }

    public string? RawJson { get; init; }
}