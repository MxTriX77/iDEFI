/*namespace CryptoMarketCollector.Worker.Options;

public sealed class CollectorOptions
{
    // FOR NOW 60 SEC. we keep this whole thingy simple ahh like in a form of POC. later on we'll complicate things
    public int PollSeconds { get; set; } = 60;

    public string SqlitePath { get; set; } = "data/marketdata.db";

    public SourceOptions Sources { get; set; } = new();

    public CoinGeckoOptions CoinGecko { get; set; } = new();

    public BinanceSpotOptions BinanceSpot { get; set; } = new();
}

public sealed class SourceOptions
{
    public bool BinanceSpot { get; set; } = true;

    public bool CoinGecko { get; set; } = true;
}

public sealed class CoinGeckoOptions
{
    public string BaseUrl { get; set; } = "https://api.coingecko.com/api/v3";

    public string VsCurrency { get; set; } = "usd";

    public int Pages { get; set; } = 1;

    public int PerPage { get; set; } = 250;

    public string? DemoApiKey { get; set; }
}

public sealed class BinanceSpotOptions
{
    public string BaseUrl { get; set; } = "https://api.binance.com";
}*/

/*namespace CryptoMarketCollector.Worker.Options;

public sealed class CollectorOptions
{
    public int PollSeconds { get; set; } = 60;

    public bool RunOnceAndExit { get; set; } = false;

    public string SqlitePath { get; set; } = "data/marketdata.db";

    public SourceOptions Sources { get; set; } = new();

    public CoinGeckoOptions CoinGecko { get; set; } = new();

    public BinanceSpotOptions BinanceSpot { get; set; } = new();
}

public sealed class SourceOptions
{
    public bool Demo { get; set; } = true;

    public bool BinanceSpot { get; set; } = false;

    public bool CoinGecko { get; set; } = false;
}

public sealed class CoinGeckoOptions
{
    public string BaseUrl { get; set; } = "https://api.coingecko.com/api/v3";

    public string VsCurrency { get; set; } = "usd";

    public int Pages { get; set; } = 1;

    public int PerPage { get; set; } = 250;

    public string? DemoApiKey { get; set; }
}

public sealed class BinanceSpotOptions
{
    public string BaseUrl { get; set; } = "https://api.binance.com";
}*/
namespace CryptoMarketCollector.Worker.Options;

public sealed class CollectorOptions
{
    public int PollSeconds { get; set; } = 60;

    public bool RunOnceAndExit { get; set; } = true;

    public string SqlitePath { get; set; } = "data/marketdata.db";

    public SourceOptions Sources { get; set; } = new();

    public CoinGeckoOptions CoinGecko { get; set; } = new();

    public BinanceSpotOptions BinanceSpot { get; set; } = new();

    public BinanceKlineOptions BinanceKlines { get; set; } = new();
}

public sealed class SourceOptions
{
    public bool Demo { get; set; } = false;

    public bool BinanceSpot { get; set; } = true;

    public bool BinanceKlines { get; set; } = true;

    public bool CoinGecko { get; set; } = false;
}

public sealed class CoinGeckoOptions
{
    public string BaseUrl { get; set; } = "https://api.coingecko.com/api/v3";

    public string VsCurrency { get; set; } = "usd";

    public int Pages { get; set; } = 1;

    public int PerPage { get; set; } = 250;

    public string? DemoApiKey { get; set; }
}

public sealed class BinanceSpotOptions
{
    public string BaseUrl { get; set; } = "https://data-api.binance.vision";
}

public sealed class BinanceKlineOptions
{
    public string[] QuoteAssets { get; set; } = ["USDT", "USDC"];

    public string[] Intervals { get; set; } = ["1m", "5m", "15m"];

    public int Limit { get; set; } = 120;

    public int MaxSymbols { get; set; } = 60;

    public decimal MinQuoteVolume24h { get; set; } = 500_000m;

    public decimal MaxSpreadBps { get; set; } = 50m;

    public int DelayMsBetweenRequests { get; set; } = 150;
}