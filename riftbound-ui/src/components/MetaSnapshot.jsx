import { useState, useRef, useEffect } from 'react'
import { fetchGeneratedDeck } from '../api'
import './MetaSnapshot.css'

function getCardImageUrl(cardId) {
  const baseId = cardId.replace(/-?A$/, '');
  return `https://static.dotgg.gg/riftbound/cards/${baseId}.webp`
}

function Tooltip({ text, children }) {
  return (
    <span className="tooltip-wrapper">
      {children}
      <span className="tooltip-icon">?</span>
      <span className="tooltip-bubble">{text}</span>
    </span>
  )
}

function CollapsibleSection({ title, open, onToggle, children }) {
  return (
    <div className={`collapsible-section ${open ? 'collapsible-section--open' : ''}`}>
      <button className="collapsible-section__toggle" onClick={onToggle}>
        <span className="collapsible-section__arrow">{open ? '▾' : '▸'}</span>
        <span className="collapsible-section__title">{title}</span>
      </button>
      {open && <div className="collapsible-section__body">{children}</div>}
    </div>
  )
}

function useSectionState(keys) {
  const ref = useRef(null)
  if (ref.current === null) {
    ref.current = Object.fromEntries(keys.map(k => [k, true]))
  }
  const [, rerender] = useState(0)
  const toggle = (key) => {
    ref.current[key] = !ref.current[key]
    rerender(n => n + 1)
  }
  return [ref.current, toggle]
}

const SECTION_KEYS = ['Stats', 'Charts', 'CardPairSynergies', 'PerformanceTrend', 'MatchupTable', 'TopSynergisticRunes', 'CardBreakdown', 'GeneratedDeck']

export default function MetaSnapshot({ data, loading, trendData, matchupData, archetypes = [], selectedLegend, dateRange }) {
  const [selectedCard, setSelectedCard] = useState(null)
  const [sections, toggleSection] = useSectionState(SECTION_KEYS)
  const [selectedArchetype, setSelectedArchetype] = useState('')
  const [generatedDeck, setGeneratedDeck] = useState(null)
  const [generating, setGenerating] = useState(false)

  useEffect(() => { setSelectedCard(null) }, [data])
  useEffect(() => {
    setSelectedArchetype(archetypes.length > 0 ? archetypes[0] : '')
    setGeneratedDeck(null)
  }, [archetypes])

  if (loading) {
    return (
      <div className="meta-snapshot meta-snapshot--loading">
        <div className="spinner" />
        <p>Loading meta data...</p>
      </div>
    )
  }

  if (!data) {
    return (
      <div className="meta-snapshot meta-snapshot--empty">
        <p>Select a legend to view meta analysis</p>
      </div>
    )
  }

  const { metaSnapshot, synergisticCards } = data

  if (!metaSnapshot || metaSnapshot.sampleSize === 0) {
    return (
      <div className="meta-snapshot meta-snapshot--empty">
        <p>No tournament data available for this legend yet.</p>
      </div>
    )
  }

  const handleGenerate = async () => {
    if (!selectedLegend || !selectedArchetype) return
    setGenerating(true)
    try {
      const deck = await fetchGeneratedDeck(selectedLegend.id, selectedArchetype, dateRange)
      setGeneratedDeck(deck)
    } catch {
      setGeneratedDeck(null)
    } finally {
      setGenerating(false)
    }
  }

  // Merge core + tech into one list, sorted by appearance rate
  const allCards = [
    ...(metaSnapshot.coreCards || []).map(c => ({ ...c, tier: 'Core' })),
    ...(metaSnapshot.techChoices || []).map(c => ({ ...c, tier: 'Tech' }))
  ].sort((a, b) => b.appearanceRate - a.appearanceRate)

  // Group by category
  const grouped = {}
  for (const card of allCards) {
    const cat = card.category || 'Unknown'
    if (!grouped[cat]) grouped[cat] = []
    grouped[cat].push(card)
  }

  // Sort categories by their best card's appearance rate
  const sortedCategories = Object.entries(grouped).sort(
    (a, b) => b[1][0].appearanceRate - a[1][0].appearanceRate
  )

  return (
    <div className="meta-snapshot">
      <div className="meta-body">
        <div className="meta-content">
      <div className="meta-header">
        <h2>{metaSnapshot.legendName}</h2>
        <span className="meta-sample">Based on {metaSnapshot.sampleSize} deck{metaSnapshot.sampleSize !== 1 ? 's' : ''}</span>
        {metaSnapshot.bestPlacement != null && (
          <Tooltip text="Highest tournament finish achieved by any deck using this legend">
            <span className="meta-placement meta-placement--best">Best: {metaSnapshot.bestPlacement}</span>
          </Tooltip>
        )}
        {metaSnapshot.worstPlacement != null && (
          <Tooltip text="Lowest tournament finish among all decks using this legend">
            <span className="meta-placement meta-placement--worst">Worst: {metaSnapshot.worstPlacement}</span>
          </Tooltip>
        )}
        {metaSnapshot.averagePlacement != null && (
          <Tooltip text="Average tournament placement, excluding the single best and worst results">
            <span className="meta-placement">Avg Placement: {metaSnapshot.averagePlacement}</span>
          </Tooltip>
        )}
      </div>

      {archetypes.length > 0 && (
        <div className="deck-generator-bar">
          <label className="deck-generator-bar__label">Archetype</label>
          <select
            className="deck-generator-bar__select"
            value={selectedArchetype}
            onChange={(e) => setSelectedArchetype(e.target.value)}
          >
            {archetypes.map((a) => (
              <option key={a} value={a}>{a}</option>
            ))}
          </select>
          <button
            className="deck-generator-bar__btn"
            onClick={handleGenerate}
            disabled={generating || !selectedArchetype}
          >
            {generating ? 'Generating...' : 'Generate Deck'}
          </button>
        </div>
      )}

      <CollapsibleSection title="Stats" open={sections.Stats} onToggle={() => toggleSection('Stats')}>
      <div className="stats-row">
        {metaSnapshot.playRate != null && (
          <div className="stat-card">
            <span className="stat-card__value">{metaSnapshot.playRate}%</span>
            <Tooltip text="Percentage of all tournament decks that use this legend">
              <span className="stat-card__label">Play Rate</span>
            </Tooltip>
          </div>
        )}
        {metaSnapshot.topCutRate != null && (
          <div className="stat-card">
            <span className="stat-card__value">{metaSnapshot.topCutRate}%</span>
            <Tooltip text="Percentage of this legend's decks that finished in the top 4">
              <span className="stat-card__label">Top Cut Rate</span>
            </Tooltip>
          </div>
        )}
      </div>
      </CollapsibleSection>

      <CollapsibleSection title="Card Breakdown" open={sections.CardBreakdown} onToggle={() => toggleSection('CardBreakdown')}>
      <div className="category-groups">
        {sortedCategories.map(([category, cards]) => (
          <CategoryGroup key={category} category={category} cards={cards} selectedCard={selectedCard} onSelectCard={setSelectedCard} />
        ))}
      </div>
      </CollapsibleSection>

      <CollapsibleSection title="Charts" open={sections.Charts} onToggle={() => toggleSection('Charts')}>
      <div className="charts-row">
        {metaSnapshot.energyCurve && Object.keys(metaSnapshot.energyCurve).length > 0 && (
          <EnergyCurve data={metaSnapshot.energyCurve} />
        )}
        {metaSnapshot.domainDistribution && Object.keys(metaSnapshot.domainDistribution).length > 0 && (
          <DomainDistribution data={metaSnapshot.domainDistribution} />
        )}
      </div>
      </CollapsibleSection>

      {metaSnapshot.cardPairSynergies && metaSnapshot.cardPairSynergies.length > 0 && (
        <CollapsibleSection title="Card Pair Synergies" open={sections.CardPairSynergies} onToggle={() => toggleSection('CardPairSynergies')}>
          <CardPairSynergies pairs={metaSnapshot.cardPairSynergies} />
        </CollapsibleSection>
      )}

      {trendData && trendData.length > 0 && (
        <CollapsibleSection title="Performance Trend" open={sections.PerformanceTrend} onToggle={() => toggleSection('PerformanceTrend')}>
          <TrendChart data={trendData} />
        </CollapsibleSection>
      )}

      {matchupData && matchupData.length > 0 && (
        <CollapsibleSection title="Matchup Table" open={sections.MatchupTable} onToggle={() => toggleSection('MatchupTable')}>
          <MatchupTable data={matchupData} />
        </CollapsibleSection>
      )}

      {synergisticCards && synergisticCards.length > 0 && (
        <CollapsibleSection title="Top Synergistic Runes" open={sections.TopSynergisticRunes} onToggle={() => toggleSection('TopSynergisticRunes')}>
          <div className="synergy-section">
            <div className="synergy-list">
              {synergisticCards.map((card) => (
                <span key={card.id} className="synergy-chip">{card.name}</span>
              ))}
            </div>
          </div>
        </CollapsibleSection>
      )}

      {generatedDeck && (
        <CollapsibleSection title={`Generated Deck — ${generatedDeck.archetype}`} open={sections.GeneratedDeck} onToggle={() => toggleSection('GeneratedDeck')}>
          <GeneratedDeckView deck={generatedDeck} onSelectCard={setSelectedCard} selectedCard={selectedCard} />
        </CollapsibleSection>
      )}

        </div>

        {selectedCard && (
          <div className="card-preview">
            <img
              src={getCardImageUrl(selectedCard.cardId)}
              alt={selectedCard.cardName}
              className="card-preview__image"
              onError={(e) => { e.target.style.display = 'none' }}
            />
            <p className="card-preview__name">{selectedCard.cardName}</p>
          </div>
        )}
      </div>
    </div>
  )
}

function EnergyCurve({ data }) {
  const entries = Object.entries(data).map(([cost, avg]) => ({ cost: Number(cost), avg }))
  const maxAvg = Math.max(...entries.map(e => e.avg))

  return (
    <div className="chart-card">
      <Tooltip text="Average number of cards at each energy cost across this legend's decks">
        <h3 className="chart-card__title">Energy Curve</h3>
      </Tooltip>
      <div className="energy-chart">
        {entries.map(({ cost, avg }) => (
          <div key={cost} className="energy-bar-col">
            <div className="energy-bar-wrapper">
              <div
                className="energy-bar"
                style={{ height: `${maxAvg > 0 ? (avg / maxAvg) * 100 : 0}%` }}
              />
            </div>
            <span className="energy-bar-label">{cost}</span>
            <span className="energy-bar-value">{avg}</span>
          </div>
        ))}
      </div>
    </div>
  )
}

function DomainDistribution({ data }) {
  const entries = Object.entries(data)

  const domainColors = {
    Fire: '#e57373',
    Water: '#64b5f6',
    Earth: '#a1887f',
    Air: '#b0bec5',
    Light: '#fff176',
    Shadow: '#9575cd',
    Neutral: '#90a4ae',
  }

  return (
    <div className="chart-card">
      <Tooltip text="How the cards in this legend's decks are distributed across domains (colors)">
        <h3 className="chart-card__title">Domain Distribution</h3>
      </Tooltip>
      <div className="domain-bars">
        {entries.map(([domain, pct]) => (
          <div key={domain} className="domain-row">
            <span className="domain-row__label">{domain}</span>
            <div className="domain-row__track">
              <div
                className="domain-row__fill"
                style={{ width: `${pct}%`, background: domainColors[domain] || '#6c63ff' }}
              />
            </div>
            <span className="domain-row__pct">{pct}%</span>
          </div>
        ))}
      </div>
    </div>
  )
}

function CategoryGroup({ category, cards, selectedCard, onSelectCard }) {
  return (
    <div className="category-group">
      <h3 className="category-title">{category}</h3>
      <div className="card-table">
        <div className="card-table__header">
          <span>Card</span>
          <Tooltip text="How often this card appears across all decks for this legend">
            <span>Rate</span>
          </Tooltip>
          <Tooltip text="Average number of copies included when the card is played">
            <span>Avg Qty</span>
          </Tooltip>
          <Tooltip text="Core (>60% inclusion) or Tech (15-60% inclusion)">
            <span>Tier</span>
          </Tooltip>
        </div>
        {cards.map((card) => (
          <div
            key={card.cardName}
            className={`card-row ${selectedCard?.cardId === card.cardId ? 'card-row--selected' : ''}`}
            onClick={() => onSelectCard(card)}
          >
            <span className="card-row__name">{card.cardName}</span>
            <span className="card-row__rate">
              <span className="rate-bar" style={{ width: `${Math.round(card.appearanceRate * 100)}%` }} />
              {Math.round(card.appearanceRate * 100)}%
            </span>
            <span className="card-row__qty">{card.avgQuantity.toFixed(1)}</span>
            <span className={`card-row__tier card-row__tier--${card.tier.toLowerCase()}`}>
              {card.tier}
            </span>
          </div>
        ))}
      </div>
    </div>
  )
}

function CardPairSynergies({ pairs }) {
  return (
    <div className="chart-card pair-synergies">
      <Tooltip text="Card pairs that most frequently appear together in this legend's decks">
        <h3 className="chart-card__title">Card Pair Synergies</h3>
      </Tooltip>
      <div className="pair-list">
        {pairs.map((p, i) => (
          <div key={i} className="pair-row">
            <span className="pair-row__cards">{p.card1Name} + {p.card2Name}</span>
            <div className="pair-row__track">
              <div className="pair-row__fill" style={{ width: `${Math.round(p.coOccurrenceRate * 100)}%` }} />
            </div>
            <span className="pair-row__pct">{Math.round(p.coOccurrenceRate * 100)}%</span>
          </div>
        ))}
      </div>
    </div>
  )
}

function TrendChart({ data }) {
  const maxDecks = Math.max(...data.map(t => t.decksEntered), 1)
  // For placement axis, lower is better so we invert: bar height = 1 - (avg / maxPlacement)
  const maxPlacement = Math.max(...data.map(t => t.averagePlacement ?? 0), 1)

  return (
    <div className="chart-card trend-chart">
      <Tooltip text="How this legend performed across different tournaments over time">
        <h3 className="chart-card__title">Performance Trend</h3>
      </Tooltip>
      <div className="trend-grid">
        <div className="trend-header">
          <span>Tournament</span>
          <span>Decks</span>
          <span>Avg Place</span>
          <span>Best</span>
        </div>
        {data.map((t) => (
          <div key={t.tournamentId} className="trend-row">
            <span className="trend-row__name" title={t.tournamentName}>
              {t.tournamentName.length > 30 ? t.tournamentName.slice(0, 28) + '…' : t.tournamentName}
            </span>
            <span className="trend-row__decks">{t.decksEntered}</span>
            <span className="trend-row__avg">{t.averagePlacement ?? '—'}</span>
            <span className="trend-row__best">{t.bestPlacement ?? '—'}</span>
          </div>
        ))}
      </div>
    </div>
  )
}

function MatchupTable({ data }) {
  return (
    <div className="chart-card matchup-table">
      <Tooltip text="How this legend's average placement compares to other legends in shared tournaments. Negative (green) = you place better.">
        <h3 className="chart-card__title">Matchup Table</h3>
      </Tooltip>
      <div className="matchup-grid">
        <div className="matchup-header">
          <span>Opponent</span>
          <span>Δ Placement</span>
          <span>Shared</span>
        </div>
        {data.map((m) => (
          <div key={m.legendId} className="matchup-row">
            <span className="matchup-row__name">{m.legendName}</span>
            <span className={`matchup-row__delta ${m.placementDelta < 0 ? 'matchup-row__delta--good' : m.placementDelta > 0 ? 'matchup-row__delta--bad' : ''}`}>
              {m.placementDelta > 0 ? '+' : ''}{m.placementDelta}
            </span>
            <span className="matchup-row__shared">{m.sharedTournaments}</span>
          </div>
        ))}
      </div>
    </div>
  )
}

function GeneratedDeckView({ deck, onSelectCard, selectedCard }) {
  const renderSection = (title, cards) => {
    if (!cards || cards.length === 0) return null
    const total = cards.reduce((sum, c) => sum + c.quantity, 0)
    return (
      <div className="gen-deck-section">
        <h4 className="gen-deck-section__title">{title} ({total})</h4>
        <div className="gen-deck-list">
          {cards.map((card) => (
            <div
              key={card.cardId}
              className={`gen-deck-row ${selectedCard?.cardId === card.cardId ? 'gen-deck-row--selected' : ''}`}
              onClick={() => onSelectCard(card)}
            >
              <span className="gen-deck-row__qty">{card.quantity}x</span>
              <span className="gen-deck-row__name">{card.cardName}</span>
              <span className="gen-deck-row__cat">{card.category}</span>
              <span className="gen-deck-row__rate">{Math.round(card.appearanceRate * 100)}%</span>
            </div>
          ))}
        </div>
      </div>
    )
  }

  return (
    <div className="gen-deck">
      {renderSection('Main Deck', deck.mainDeck)}
      {renderSection('Battlefields', deck.battlefields)}
      {renderSection('Side Deck (Runes)', deck.sideDeck)}
    </div>
  )
}
