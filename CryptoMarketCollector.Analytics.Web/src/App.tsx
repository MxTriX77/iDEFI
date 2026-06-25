import { useEffect, useMemo, useState } from 'react'
import type { ReactNode } from 'react'
import './App.css'

type DecisionSuggestion = {
  symbol: string
  baseAsset: string | null
  quoteAsset: string | null

  decisionLabel: string
  decisionScore: number

  suggestedHorizonMinMinutes: number | null
  suggestedHorizonMaxMinutes: number | null

  latestPrice: number
  recentMovePercent: number | null
  priceChange24hPercent: number | null
  spreadBps: number | null
  quoteVolume24h: number | null
  tradeCount24h: number | null

  liquidityScore: number
  momentumScore: number
  crowdScore: number
  riskScore: number

  plainEnglishReason: string
  plainEnglishRisk: string
  invalidationNote: string
}

type DecisionSuggestionResponse = {
  scoredAtUtc: string | null
  latestCollectionUtc: string | null
  previousCollectionUtc: string | null
  suggestions: DecisionSuggestion[]
}

type DecisionGroup = {
  label: string
  title: string
  subtitle: string
  emoji: string
  className: string
}

const decisionGroups: DecisionGroup[] = [
  {
    label: 'SCALP_CANDIDATE',
    title: 'Scalp candidates',
    subtitle: 'Short-window setups. These need fast confirmation.',
    emoji: '🔥',
    className: 'hot',
  },
  {
    label: 'EARLY_ACTION_WATCH',
    title: 'Early action watch',
    subtitle: 'Something is starting, but it is not clean enough yet.',
    emoji: '⚡',
    className: 'early',
  },
  {
    label: 'MOMENTUM_CANDIDATE',
    title: 'Momentum candidates',
    subtitle: 'Pairs with stronger follow-through potential.',
    emoji: '🚀',
    className: 'momentum',
  },
  {
    label: 'HOLD_RESEARCH_CANDIDATE',
    title: 'Hold research',
    subtitle: 'Liquid pairs worth researching, not quick-flip signals.',
    emoji: '🧊',
    className: 'baseline',
  },
  {
    label: 'BASELINE_RESEARCH_CANDIDATE',
    title: 'Baseline research',
    subtitle: 'Clean, liquid radar pairs without a fresh action signal.',
    emoji: '📡',
    className: 'baseline',
  },
  {
    label: 'WATCH',
    title: 'Watch',
    subtitle: 'Not ready yet. Needs stronger evidence.',
    emoji: '👀',
    className: 'watch',
  },
  {
    label: 'AVOID',
    title: 'Avoid',
    subtitle: 'Too noisy, too expensive, too weak, or not enough data.',
    emoji: '⛔',
    className: 'avoid',
  },
]

const quickFilters = ['ALL', 'USDT', 'USDC'] as const

function App() {
  const [data, setData] = useState<DecisionSuggestionResponse | null>(null)
  const [error, setError] = useState<string | null>(null)
  const [isLoading, setIsLoading] = useState(true)
  const [selectedQuote, setSelectedQuote] = useState<(typeof quickFilters)[number]>('USDT')
  const [selectedGroup, setSelectedGroup] = useState<string>('ALL')

  async function loadData() {
    try {
      setError(null)

      const response = await fetch('/api/analytics/decision-suggestions')

      if (!response.ok) {
        throw new Error(`API returned ${response.status}`)
      }

      const json = (await response.json()) as DecisionSuggestionResponse
      setData(json)
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Unknown error')
    } finally {
      setIsLoading(false)
    }
  }

  useEffect(() => {
    loadData()

    const timer = window.setInterval(loadData, 30_000)

    return () => window.clearInterval(timer)
  }, [])

  const filteredSuggestions = useMemo(() => {
    if (!data) return []

    return data.suggestions.filter((item) => {
      const quoteMatch =
        selectedQuote === 'ALL' || item.quoteAsset === selectedQuote

      const groupMatch =
        selectedGroup === 'ALL' || item.decisionLabel === selectedGroup

      return quoteMatch && groupMatch
    })
  }, [data, selectedQuote, selectedGroup])

  const countsByLabel = useMemo(() => {
    const counts = new Map<string, number>()

    for (const item of filteredSuggestions) {
      counts.set(item.decisionLabel, (counts.get(item.decisionLabel) ?? 0) + 1)
    }

    return counts
  }, [filteredSuggestions])

  const strongest = filteredSuggestions[0] ?? null

  if (isLoading) {
    return (
      <Shell>
        <Hero
          title="Loading decision radar…"
          subtitle="Reading the latest public Binance snapshots and scoring the market."
          data={null}
        />
      </Shell>
    )
  }

  if (error) {
    return (
      <Shell>
        <Hero
          title="Analytics API is not reachable"
          subtitle={`Error: ${error}. Make sure the .NET API is running on http://localhost:5245.`}
          data={null}
        />
        <button className="primaryButton" onClick={loadData}>
          Retry
        </button>
      </Shell>
    )
  }

  if (!data) {
    return null
  }

  return (
    <Shell>
      <Hero
        title="Crypto decision radar"
        subtitle="Plain-English research signals from public market data. No guarantees, no auto-trading."
        data={data}
      />

      <section className="summaryGrid">
        <MetricCard label="Suggestions" value={filteredSuggestions.length.toString()} />
        <MetricCard label="Strongest score" value={strongest ? `${strongest.decisionScore.toFixed(0)}/100` : '—'} />
        <MetricCard label="Strongest symbol" value={strongest?.symbol ?? '—'} />
        <MetricCard label="Refresh" value="30s" />
      </section>

      <section className="controlPanel">
        <div>
          <h2>Radar filters</h2>
          <p>
            Start with USDT/USDC markets. They are easier to understand than mixed fiat or tiny quote pairs.
          </p>
        </div>

        <div className="controlStack">
          <div className="pills">
            {quickFilters.map((quote) => (
              <button
                key={quote}
                className={selectedQuote === quote ? 'pill active' : 'pill'}
                onClick={() => setSelectedQuote(quote)}
              >
                {quote}
              </button>
            ))}
          </div>

          <div className="pills wide">
            <button
              className={selectedGroup === 'ALL' ? 'pill active' : 'pill'}
              onClick={() => setSelectedGroup('ALL')}
            >
              ALL
            </button>

            {decisionGroups.map((group) => (
              <button
                key={group.label}
                className={selectedGroup === group.label ? 'pill active' : 'pill'}
                onClick={() => setSelectedGroup(group.label)}
              >
                {group.emoji} {group.title}
              </button>
            ))}
          </div>
        </div>
      </section>

      <section className="groupTabs">
        {decisionGroups.map((group) => (
          <button
            key={group.label}
            className={`groupTab ${group.className} ${selectedGroup === group.label ? 'selected' : ''}`}
            onClick={() => setSelectedGroup(group.label)}
          >
            <span>{group.emoji}</span>
            <strong>{countsByLabel.get(group.label) ?? 0}</strong>
            <small>{group.title}</small>
          </button>
        ))}
      </section>

      <section className="decisionSections">
        {decisionGroups.map((group) => {
          const items = filteredSuggestions.filter(
            (item) => item.decisionLabel === group.label,
          )

          if (items.length === 0) {
            return null
          }

          return (
            <DecisionSection
              key={group.label}
              group={group}
              suggestions={items}
            />
          )
        })}

        {filteredSuggestions.length === 0 && (
          <section className="emptyPanel">
            <h2>No suggestions for this filter</h2>
            <p>
              Try another quote asset, or run the collector again to create more comparable snapshots.
            </p>
          </section>
        )}
      </section>

      <footer>
        Research only. This app does not place trades, guarantee profit, or know the future.
      </footer>
    </Shell>
  )
}

function Shell({ children }: { children: ReactNode }) {
  return <main className="page">{children}</main>
}

function Hero({
  title,
  subtitle,
  data,
}: {
  title: string
  subtitle: string
  data: DecisionSuggestionResponse | null
}) {
  return (
    <section className="hero">
      <div>
        <p className="eyebrow">iDEFI Market Intelligence</p>
        <h1>{title}</h1>
        <p className="heroSubtitle">{subtitle}</p>
      </div>

      <div className="statusCard">
        <span className="statusDot" />
        <div>
          <strong>{data ? 'Live research feed' : 'Starting feed'}</strong>
          <p>Latest: {formatDate(data?.latestCollectionUtc ?? null)}</p>
          <p>Previous: {formatDate(data?.previousCollectionUtc ?? null)}</p>
        </div>
      </div>
    </section>
  )
}

function MetricCard({ label, value }: { label: string; value: string }) {
  return (
    <article className="metricCard">
      <p>{label}</p>
      <strong>{value}</strong>
    </article>
  )
}

function DecisionSection({
  group,
  suggestions,
}: {
  group: DecisionGroup
  suggestions: DecisionSuggestion[]
}) {
  return (
    <section className={`decisionSection ${group.className}`}>
      <div className="sectionHeader">
        <div>
          <h2>
            <span>{group.emoji}</span> {group.title}
          </h2>
          <p>{group.subtitle}</p>
        </div>

        <strong>{suggestions.length}</strong>
      </div>

      <div className="cardsGrid">
        {suggestions.map((suggestion) => (
          <DecisionCard key={`${suggestion.symbol}-${suggestion.decisionLabel}`} suggestion={suggestion} />
        ))}
      </div>
    </section>
  )
}

function DecisionCard({ suggestion }: { suggestion: DecisionSuggestion }) {
  return (
    <article className={`decisionCard ${labelClass(suggestion.decisionLabel)}`}>
      <div className="cardTop">
        <div>
          <p className="quote">
            {suggestion.baseAsset ?? '—'} / {suggestion.quoteAsset ?? '—'}
          </p>
          <h3>{suggestion.symbol}</h3>
          <p className="labelLine">
            {humanizeLabel(suggestion.decisionLabel)}
            {formatHorizon(suggestion) ? ` · ${formatHorizon(suggestion)}` : ''}
          </p>
        </div>

        <ScoreRing score={suggestion.decisionScore} />
      </div>

      <div className="miniStats">
        <MiniStat label="Recent" value={formatPercent(suggestion.recentMovePercent)} tone={toneFromNumber(suggestion.recentMovePercent)} />
        <MiniStat label="24h" value={formatPercent(suggestion.priceChange24hPercent)} tone={toneFromNumber(suggestion.priceChange24hPercent)} />
        <MiniStat label="Spread" value={formatBps(suggestion.spreadBps)} />
        <MiniStat label="Volume" value={formatCompact(suggestion.quoteVolume24h)} />
      </div>

      <div className="reasonBlock">
        <h4>Why it showed up</h4>
        <p>{suggestion.plainEnglishReason}</p>
      </div>

      <div className="reasonBlock risk">
        <h4>What can go wrong</h4>
        <p>{suggestion.plainEnglishRisk}</p>
      </div>

      <div className="invalidation">
        <strong>Invalid if:</strong> {suggestion.invalidationNote}
      </div>

      <div className="scoreBars">
        <ScoreBar label="Liquidity" value={suggestion.liquidityScore} />
        <ScoreBar label="Momentum" value={suggestion.momentumScore} />
        <ScoreBar label="Crowd" value={suggestion.crowdScore} />
        <ScoreBar label="Risk" value={suggestion.riskScore} inverse />
      </div>
    </article>
  )
}

function ScoreRing({ score }: { score: number }) {
  return (
    <div className="scoreRing">
      <strong>{score.toFixed(0)}</strong>
      <span>/100</span>
    </div>
  )
}

function MiniStat({
  label,
  value,
  tone,
}: {
  label: string
  value: string
  tone?: 'positive' | 'negative' | 'neutral'
}) {
  return (
    <div className={`miniStat ${tone ?? 'neutral'}`}>
      <span>{label}</span>
      <strong>{value}</strong>
    </div>
  )
}

function ScoreBar({
  label,
  value,
  inverse = false,
}: {
  label: string
  value: number
  inverse?: boolean
}) {
  const clamped = Math.max(0, Math.min(100, value))
  const displayValue = inverse ? 100 - clamped : clamped

  return (
    <div className="scoreBar">
      <div>
        <span>{label}</span>
        <strong>{value.toFixed(0)}</strong>
      </div>
      <div className="barTrack">
        <div className="barFill" style={{ width: `${displayValue}%` }} />
      </div>
    </div>
  )
}

function humanizeLabel(label: string) {
  return label
    .toLowerCase()
    .split('_')
    .map((part) => part.charAt(0).toUpperCase() + part.slice(1))
    .join(' ')
}

function labelClass(label: string) {
  return label.toLowerCase().replaceAll('_', '-')
}

function formatHorizon(suggestion: DecisionSuggestion) {
  if (
    suggestion.suggestedHorizonMinMinutes === null ||
    suggestion.suggestedHorizonMaxMinutes === null
  ) {
    return null
  }

  return `${suggestion.suggestedHorizonMinMinutes}–${suggestion.suggestedHorizonMaxMinutes} min`
}

function formatDate(value: string | null) {
  if (!value) return 'Not enough data yet'

  return new Intl.DateTimeFormat(undefined, {
    dateStyle: 'medium',
    timeStyle: 'medium',
  }).format(new Date(value))
}

function formatPercent(value: number | null) {
  if (value === null || Number.isNaN(value)) return '—'

  const sign = value > 0 ? '+' : ''

  return `${sign}${value.toFixed(2)}%`
}

function formatBps(value: number | null) {
  if (value === null || Number.isNaN(value)) return '—'

  return `${value.toFixed(2)} bps`
}

function formatCompact(value: number | null) {
  if (value === null || Number.isNaN(value)) return '—'

  return new Intl.NumberFormat(undefined, {
    notation: 'compact',
    maximumFractionDigits: 2,
  }).format(value)
}

function toneFromNumber(value: number | null): 'positive' | 'negative' | 'neutral' {
  if (value === null) return 'neutral'
  if (value > 0) return 'positive'
  if (value < 0) return 'negative'
  return 'neutral'
}

export default App