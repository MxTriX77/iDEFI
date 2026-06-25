# iDEFI Crypto Market Radar

tihs is a tiny market goblin that watches crypto numbers and tries to tell you when something looks interesting.

!!WARNING !!! NOt a trading bot.
not financial advice.

not “bro trust me, 100x tomorrow”.

just a little radar that collects public Binance market data, does some math in the basement, and shows you plain-English cards like:

```txt
EARLY ACTION WATCH
BASELINE RESEARCH
WATCH
AVOID
```

basically: less staring at charts like a haunted VICTORIAN child, more “ok what is actually moving ?”

## what this thing does

The project has three parts:

```txt
collector
  goes to Binance public data and grabs market snapshots

analytics api
  reads the snapshots and turns them into simple decision-style signals

web dashboard
  shows the signals in a clean React page
```

The collector saves the market data into SQLite.

SQLite is just a small local database file.
No drama. No server beast. No wizard tower.
Just a file sitting there, holding numbers like a responsible little brick.

## what it looks for

The app mostly asks four questions.

### 1. is the coin moving?

If the price starts moving between scrapes, the app pays attention.

If it is just lying there like a bored lizard under a lamp, it probably becomes baseline/watch material.

### 2. are people actually trading it?

A move with volume is more interesting.

A move with no volume is just one mosquito bumping into the chart.

### 3. is the spread ugly?

Spread is the gap between buying and selling.

Tiny spread: nice, door is open.
Huge spread: the door is made of knives.

If the spread is too wide, the app gets suspicious.

### 4. is this too chaotic?

Some volatility is useful.
Too much volatility is a raccoon driving a forklift.

The app tries to avoid rewarding total nonsense just because it moves.

## the labels

### `EARLY_ACTION_WATCH`

Something is starting.

Not “buy now”.
More like:

```txt
hey, this one twitched in an interesting way. maybe watch the next scrape.
```

### `SCALP_CANDIDATE`

A short-window setup.

The idea here is minutes, not marriage.
If the move does not continue soon, the signal gets stale and should be treated like old fries.

### `MOMENTUM_CANDIDATE`

The coin is not just twitching once.
It may be walking with some direction.

Still risky. Still crypto. Still goblin weather.

### `BASELINE_RESEARCH_CANDIDATE`

Clean, liquid, worth keeping on the radar.

This usually means:

```txt
not exciting right now, but respectable enough to not throw into the swamp.
```

### `WATCH`

Something exists, but not enough.

The app is basically saying:

```txt
hmm. maybe. don't get dramatic yet.
```

### `AVOID`

The market looks too thin, too expensive, too noisy, or too cursed.

Could it still pump? sure.
Could a pigeon become mayor? also sure.
We are not building the strategy around that.

## how the score works

Every token gets a score from `0` to `100`.

The score is not magic. It is just a weighted vibe-check based on simple things:

```txt
is it moving?
is there volume?
is the spread acceptable?
is the risk not disgusting?
```

The app rewards:

```txt
fresh movement
real trading activity
tight spread
cleaner market behavior
```

The app punishes:

```txt
wide spread
weak volume
chaotic price range
negative movement
not enough data
```

So the score means:

```txt
how interesting is this thing for research right now?
```

Not:

```txt
the universe commands you to buy this token.
```

The app is a radar, not a prophet.

## example

```txt
DCRUSDT
Early Action Watch · 5–30 min
Score: 58/100

Why it showed up:
DCR moved recently, people are trading it, and the spread is not horrible.

What can go wrong:
Volume is not amazing. If the next scrape does not continue the move,
this idea becomes cold soup.

Invalid if:
price stops moving, volume fades, or spread widens.
```

That is the whole point: simple answer, simple reason, simple risk.

No finance cosplay.
No spreadsheet priesthood.
No “alpha signal proprietary machine learning neural whale detector 9000”.

Just:

```txt
what moved?
was it real?
is it tradable?
what would make this idea dumb?
```

## how to run

Run the collector to grab fresh data:

```bash
dotnet run --project CryptoMarketCollector.Worker/CryptoMarketCollector.Worker.csproj
```

Run the API:

```bash
ASPNETCORE_URLS=http://localhost:5245 \
dotnet run --project CryptoMarketCollector.Analytics.Api/CryptoMarketCollector.Analytics.Api.csproj
```

Run the dashboard:

```bash
cd CryptoMarketCollector.Analytics.Web
npm run dev -- --host 0.0.0.0
```

Open:

```txt
http://localhost:5173
```

## current state

This is early.

Right now it works from Binance public market snapshots.
Next upgrade is candle data, so the app can understand short-term movement better instead of judging the market from a few snapshots like a caffeinated detective.

## boring but important

This project is for personal research and experiments.

It does not guarantee profit.
It does not place trades.
It does not know the future.
Crypto can move like a shopping cart with one cursed wheel.

use brain. stay chill.
