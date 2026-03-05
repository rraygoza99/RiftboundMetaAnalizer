import { useState, useRef, useEffect } from 'react'
import { getLegendPortraitUrl } from '../api'
import './LegendSlider.css'

export default function LegendSlider({ legends, selectedId, onSelect }) {
  const sliderRef = useRef(null)
  const [canScrollLeft, setCanScrollLeft] = useState(false)
  const [canScrollRight, setCanScrollRight] = useState(true)

  const updateScrollButtons = () => {
    const el = sliderRef.current
    if (!el) return
    setCanScrollLeft(el.scrollLeft > 0)
    setCanScrollRight(el.scrollLeft + el.clientWidth < el.scrollWidth - 1)
  }

  useEffect(() => {
    const el = sliderRef.current
    if (!el) return
    updateScrollButtons()
    el.addEventListener('scroll', updateScrollButtons)
    return () => el.removeEventListener('scroll', updateScrollButtons)
  }, [legends])

  const scroll = (direction) => {
    const el = sliderRef.current
    if (!el) return
    const scrollAmount = el.clientWidth * 0.6
    el.scrollBy({ left: direction * scrollAmount, behavior: 'smooth' })
  }

  return (
    <div className="legend-slider">
      <button
        className={`slider-arrow slider-arrow--left ${canScrollLeft ? '' : 'slider-arrow--hidden'}`}
        onClick={() => scroll(-1)}
        aria-label="Scroll left"
      >
        &#8249;
      </button>

      <div className="slider-track" ref={sliderRef}>
        {legends.map((legend) => (
          <button
            key={legend.id}
            className={`legend-portrait ${selectedId === legend.id ? 'legend-portrait--selected' : ''}`}
            onClick={() => onSelect(legend)}
            title={legend.name}
          >
            <img
              src={getLegendPortraitUrl(legend.id)}
              alt={legend.name}
              onError={(e) => { e.target.src = `https://placehold.co/64x64/1a1a2e/e0e0e0?text=${legend.name?.[0] || '?'}` }}
            />
            <span className="legend-portrait__ring" />
          </button>
        ))}
      </div>

      <button
        className={`slider-arrow slider-arrow--right ${canScrollRight ? '' : 'slider-arrow--hidden'}`}
        onClick={() => scroll(1)}
        aria-label="Scroll right"
      >
        &#8250;
      </button>
    </div>
  )
}
