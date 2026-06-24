import { useEffect, useMemo, useState } from 'react'
import './App.css'

type MarketAlert = {
  severity: 'critical' | 'warning' | 'info' | string
  symbol: string
  title: string
  description: string
  value: number | null
}

type MarketMover = {
  symbol: string
  latestPrice: number
  previousPrice: number
  priceChangePercent: number
  quoteVolume24h: number | null
  spreadBps: number | null
}

type MarketVolumeLeader = {
  symbol: string
  lastPrice: number
  quoteVolume24h: number
  spreadBps: number | null
}

type AnalyticsOverview = {
  source: string
  latestCollectionUtc: string | null
  previousCollectionUtc: string | null
  latestSnapshotCount: number
  alerts: MarketAlert[]
  topMovers: MarketMover[]
  topVolumeLeaders: MarketVolumeLeader[]
}

const quoteFilters = ['ALL', 'USDT', 'USDC', 'BTC', 'BNB', 'TRY', 'IDR', 'BIDR'] as const

function App() {
  const [data, setData] = useState<AnalyticsOverview | null>(null)
  const [error, setError] = useState<string | null>(null)
  const [selectedQuote, setSelectedQuote] = useState<(typeof quoteFilters)[number]>('USDT')
  const [isLoading, setIsLoading] = useState(true)

  async function loadData() {
    try {
      setError(null)

      const response = await fetch('/api/analytics/overview')

      if (!response.ok) {
        throw new Error(`API returned ${response.status}`)
      }

      const json = await response.json() as AnalyticsOverview
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

  const filteredMovers = useMemo(() => {
    if (!data) return []

    return data.topMovers.filter(item =>
      selectedQuote === 'ALL' || item.symbol.endsWith(selectedQuote)
    )
  }, [data, selectedQuote])

  const filteredVolumeLeaders = useMemo(() => {
    if (!data) return []

    return data.topVolumeLeaders.filter(item =>
      selectedQuote === 'ALL' || item.symbol.endsWith(selectedQuote)
    )
  }, [data, selectedQuote])

  const filteredAlerts = useMemo(() => {
    if (!data) return []

    return data.alerts.filter(item =>
      selectedQuote === 'ALL' || item.symbol.endsWith(selectedQuote)
    )
  }, [data, selectedQuote])

  if (isLoading) {
    return (
      <main className="page">
        <section className="hero">
          <p className="eyebrow">iDEFI Market Intelligence</p>
          <h1>Loading market analytics…</h1>
        </section>
      </main>
    )
  }

  if (error) {
    return (
      <main className="page">
        <section className="hero">
          <p className="eyebrow">iDEFI Market Intelligence</p>
          <h1>Analytics API is not reachable</h1>
          <p className="muted">Error: {error}</p>
          <p className="muted">Make sure the .NET API is running on http://localhost:5245.</p>
          <button onClick={loadData}>Retry</button>
        </section>
      </main>
    )
  }

  if (!data) {
    return null
  }

  return (
    <main className="page">
      <section className="hero">
        <div>
          <p className="eyebrow">iDEFI Market Intelligence</p>
          <h1>Crypto market radar</h1>
          <p className="muted">
            Public Binance Spot snapshots analyzed for fast moves, volume leaders, and liquidity warnings.
          </p>
        </div>

        <div className="statusCard">
          <span className="statusDot" />
          <div>
            <strong>{data.source}</strong>
            <p>{formatDate(data.latestCollectionUtc)}</p>
          </div>
        </div>
      </section>

      <section className="statsGrid">
        <MetricCard label="Latest snapshot rows" value={formatInteger(data.latestSnapshotCount)} />
        <MetricCard label="Alerts" value={filteredAlerts.length.toString()} />
        <MetricCard label="Movers tracked" value={filteredMovers.length.toString()} />
        <MetricCard label="Refresh" value="30s" />
      </section>

      <section className="toolbar">
        <div>
          <h2>Market filter</h2>
          <p className="muted">Start with clean quote assets before looking at noisier pairs.</p>
        </div>

        <div className="pills">
          {quoteFilters.map(quote => (
            <button
              key={quote}
              className={selectedQuote === quote ? 'pill active' : 'pill'}
              onClick={() => setSelectedQuote(quote)}
            >
              {quote}
            </button>
          ))}
        </div>
      </section>

      <section className="contentGrid">
        <Panel title="Important alerts" subtitle="Signals worth checking manually">
          {filteredAlerts.length === 0 ? (
            <EmptyState text="No alerts for this filter." />
          ) : (
            <div className="alertList">
              {filteredAlerts.map((alert, index) => (
                <article className={`alert ${alert.severity}`} key={`${alert.symbol}-${index}`}>
                  <div>
                    <span className="symbol">{alert.symbol}</span>
                    <h3>{alert.title}</h3>
                    <p>{alert.description}</p>
                  </div>
                  <span className="severity">{alert.severity}</span>
                </article>
              ))}
            </div>
          )}
        </Panel>

        <Panel title="Fastest movers" subtitle="Largest move between the latest two scrapes">
          <div className="tableWrap">
            <table>
              <thead>
                <tr>
                  <th>Symbol</th>
                  <th>Move</th>
                  <th>Last</th>
                  <th>24h quote vol</th>
                  <th>Spread bps</th>
                </tr>
              </thead>
              <tbody>
                {filteredMovers.map(item => (
                  <tr key={item.symbol}>
                    <td className="symbol">{item.symbol}</td>
                    <td className={item.priceChangePercent >= 0 ? 'positive' : 'negative'}>
                      {formatPercent(item.priceChangePercent)}
                    </td>
                    <td>{formatCompact(item.latestPrice)}</td>
                    <td>{formatCompact(item.quoteVolume24h)}</td>
                    <td>{formatNullable(item.spreadBps)}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        </Panel>
      </section>

      <section className="fullWidth">
        <Panel title="Top volume leaders" subtitle="Highest 24h quote volume in the latest snapshot">
          <div className="tableWrap">
            <table>
              <thead>
                <tr>
                  <th>Symbol</th>
                  <th>Last price</th>
                  <th>24h quote volume</th>
                  <th>Spread bps</th>
                </tr>
              </thead>
              <tbody>
                {filteredVolumeLeaders.map(item => (
                  <tr key={item.symbol}>
                    <td className="symbol">{item.symbol}</td>
                    <td>{formatCompact(item.lastPrice)}</td>
                    <td>{formatCompact(item.quoteVolume24h)}</td>
                    <td>{formatNullable(item.spreadBps)}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        </Panel>
      </section>

      <footer>
        Informational analytics only. This dashboard does not place trades or guarantee profit.
      </footer>
    </main>
  )
}

function MetricCard({ label, value }: { label: string, value: string }) {
  return (
    <article className="metricCard">
      <p>{label}</p>
      <strong>{value}</strong>
    </article>
  )
}

function Panel({
  title,
  subtitle,
  children
}: {
  title: string
  subtitle: string
  children: React.ReactNode
}) {
  return (
    <section className="panel">
      <div className="panelHeader">
        <h2>{title}</h2>
        <p>{subtitle}</p>
      </div>
      {children}
    </section>
  )
}

function EmptyState({ text }: { text: string }) {
  return <div className="emptyState">{text}</div>
}

function formatDate(value: string | null) {
  if (!value) return 'No snapshot yet'

  return new Intl.DateTimeFormat(undefined, {
    dateStyle: 'medium',
    timeStyle: 'medium'
  }).format(new Date(value))
}

function formatInteger(value: number) {
  return new Intl.NumberFormat().format(value)
}

function formatPercent(value: number) {
  const sign = value > 0 ? '+' : ''

  return `${sign}${value.toFixed(2)}%`
}

function formatCompact(value: number | null) {
  if (value === null || Number.isNaN(value)) return '—'

  return new Intl.NumberFormat(undefined, {
    notation: 'compact',
    maximumFractionDigits: 4
  }).format(value)
}

function formatNullable(value: number | null) {
  if (value === null || Number.isNaN(value)) return '—'

  return value.toFixed(2)
}

export default App