import { useState, useEffect } from 'react'
import LegendSlider from './components/LegendSlider'
import MetaSnapshot from './components/MetaSnapshot'
import TierList from './components/TierList'
import { fetchLegends, fetchChampionSynergy, fetchTrend, fetchMatchups, fetchTierList } from './api'
import './App.css'

export default function App() {
  const [legends, setLegends] = useState([])
  const [selectedLegend, setSelectedLegend] = useState(null)
  const [synergyData, setSynergyData] = useState(null)
  const [trendData, setTrendData] = useState(null)
  const [matchupData, setMatchupData] = useState(null)
  const [tierListData, setTierListData] = useState(null)
  const [loading, setLoading] = useState(false)
  const [error, setError] = useState(null)
  const [dateRange, setDateRange] = useState('all')

  useEffect(() => {
    fetchLegends()
      .then(setLegends)
      .catch((err) => setError(err.message))
    fetchTierList(dateRange)
      .then(setTierListData)
      .catch(() => {})
  }, [])

  const loadLegendData = async (legendId, range) => {
    setLoading(true)
    setError(null)
    try {
      const [synergy, trend, matchups] = await Promise.all([
        fetchChampionSynergy(legendId, range),
        fetchTrend(legendId, range).catch(() => null),
        fetchMatchups(legendId, range).catch(() => null),
      ])
      setSynergyData(synergy)
      setTrendData(trend)
      setMatchupData(matchups)
    } catch (err) {
      setError(err.message)
    } finally {
      setLoading(false)
    }
  }

  const handleSelect = async (legend) => {
    if (selectedLegend?.id === legend.id) return
    setSelectedLegend(legend)
    setSynergyData(null)
    setTrendData(null)
    setMatchupData(null)
    await loadLegendData(legend.id, dateRange)
  }

  const handleRangeChange = async (range) => {
    setDateRange(range)
    fetchTierList(range).then(setTierListData).catch(() => {})
    if (selectedLegend) {
      await loadLegendData(selectedLegend.id, range)
    }
  }

  return (
    <div className="app">
      <header className="app-header">
        <h1>Riftbound Meta Analyzer</h1>
      </header>

      {error && <div className="app-error">{error}</div>}

      <section className="slider-section">
        <LegendSlider
          legends={legends}
          selectedId={selectedLegend?.id}
          onSelect={handleSelect}
        />
      </section>

      <div className="date-range-bar">
        {['1w', '2w', '1m', 'all'].map((r) => (
          <button
            key={r}
            className={`date-range-btn${dateRange === r ? ' active' : ''}`}
            onClick={() => handleRangeChange(r)}
          >
            {{ '1w': 'Last Week', '2w': 'Last 2 Weeks', '1m': 'Last Month', 'all': 'All Time' }[r]}
          </button>
        ))}
      </div>

      <main className="main-content">
        <MetaSnapshot data={synergyData} loading={loading} trendData={trendData} matchupData={matchupData} />
        {tierListData && tierListData.length > 0 && <TierList data={tierListData} />}
      </main>
    </div>
  )
}
