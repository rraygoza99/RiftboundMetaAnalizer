import { useState } from 'react'
import './MetaSnapshot.css'

function getCardImageUrl(cardId) {
  const baseId = cardId.replace(/-?A$/, '');
  return `https://static.dotgg.gg/riftbound/cards/${baseId}.webp`
}

export default function MetaSnapshot({ data, loading }) {
  const [selectedCard, setSelectedCard] = useState(null)
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
          <span className="meta-placement meta-placement--best">Best: {metaSnapshot.bestPlacement}</span>
        )}
        {metaSnapshot.worstPlacement != null && (
          <span className="meta-placement meta-placement--worst">Worst: {metaSnapshot.worstPlacement}</span>
        )}
        {metaSnapshot.averagePlacement != null && (
          <span className="meta-placement">Avg Placement: {metaSnapshot.averagePlacement}</span>
        )}
      </div>

      {synergisticCards && synergisticCards.length > 0 && (
        <div className="synergy-section">
          <h3>Top Synergistic Runes</h3>
          <div className="synergy-list">
            {synergisticCards.map((card) => (
              <span key={card.id} className="synergy-chip">{card.name}</span>
            ))}
          </div>
        </div>
      )}

      <div className="category-groups">
        {sortedCategories.map(([category, cards]) => (
          <CategoryGroup key={category} category={category} cards={cards} selectedCard={selectedCard} onSelectCard={setSelectedCard} />
        ))}
      </div>
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

function CategoryGroup({ category, cards, selectedCard, onSelectCard }) {
  return (
    <div className="category-group">
      <h3 className="category-title">{category}</h3>
      <div className="card-table">
        <div className="card-table__header">
          <span>Card</span>
          <span>Rate</span>
          <span>Avg Qty</span>
          <span>Tier</span>
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
