import { useState } from 'react'
import './TierList.css'

const tierColors = {
  S: { bg: '#3d0f3d', border: '#e040fb', text: '#f48fb1' },
  A: { bg: '#0f3d2e', border: '#4caf50', text: '#a8ffb0' },
  B: { bg: '#0f3460', border: '#6c63ff', text: '#c8c3ff' },
  C: { bg: '#3d3d0f', border: '#facc15', text: '#facc15' },
  D: { bg: '#3d0f0f', border: '#e57373', text: '#ffb0b0' },
}

export default function TierList({ data }) {
  const [open, setOpen] = useState(true)
  const tiers = ['S', 'A', 'B', 'C', 'D']
  const grouped = {}
  for (const tier of tiers) grouped[tier] = []
  for (const entry of data) {
    if (grouped[entry.tier]) grouped[entry.tier].push(entry)
  }

  return (
    <div className="tier-list">
      <button className="collapsible-section__toggle" onClick={() => setOpen(o => !o)}>
        <span className="collapsible-section__arrow">{open ? '▾' : '▸'}</span>
        <h2 className="tier-list__title" style={{ margin: 0 }}>Legend Tier List</h2>
      </button>
      {open && (
      <div className="tier-list__grid">
        {tiers.map((tier) => {
          const legends = grouped[tier]
          if (legends.length === 0) return null
          const colors = tierColors[tier]
          return (
            <div key={tier} className="tier-row" style={{ background: colors.bg, borderLeftColor: colors.border }}>
              <span className="tier-row__label" style={{ color: colors.text }}>{tier}</span>
              <div className="tier-row__legends">
                {legends.map((l) => (
                  <span key={l.legendId} className="tier-legend" title={`Play: ${l.playRate}% | Top Cut: ${l.topCutRate}% | Avg: ${l.averagePlacement ?? '—'}`}>
                    {l.legendName}
                  </span>
                ))}
              </div>
            </div>
          )
        })}
      </div>
      )}
    </div>
  )
}
