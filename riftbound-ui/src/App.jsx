import { useState, useEffect } from 'react'
import LegendSlider from './components/LegendSlider'
import MetaSnapshot from './components/MetaSnapshot'
import { fetchLegends, fetchChampionSynergy } from './api'
import './App.css'

export default function App() {
  const [legends, setLegends] = useState([])
  const [selectedLegend, setSelectedLegend] = useState(null)
  const [synergyData, setSynergyData] = useState(null)
  const [loading, setLoading] = useState(false)
  const [error, setError] = useState(null)

  useEffect(() => {
    fetchLegends()
      .then(setLegends)
      .catch((err) => setError(err.message))
  }, [])

  const handleSelect = async (legend) => {
    if (selectedLegend?.id === legend.id) return
    setSelectedLegend(legend)
    setSynergyData(null)
    setLoading(true)
    setError(null)

    try {
      const data = await fetchChampionSynergy(legend.id)
      setSynergyData(data)
    } catch (err) {
      setError(err.message)
    } finally {
      setLoading(false)
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

      <main className="main-content">
        <MetaSnapshot data={synergyData} loading={loading} />
      </main>
    </div>
  )
}
