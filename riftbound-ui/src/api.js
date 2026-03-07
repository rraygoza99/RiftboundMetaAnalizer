const API_BASE = import.meta.env.VITE_API_URL || '/api';

export async function fetchLegends() {
  const res = await fetch(`${API_BASE}/cards/legends`);
  if (!res.ok) throw new Error('Failed to fetch legends');
  return res.json();
}

function rangeQuery(range) {
  return range && range !== 'all' ? `?range=${encodeURIComponent(range)}` : '';
}

export async function fetchChampionSynergy(championId, range) {
  const res = await fetch(`${API_BASE}/meta/champion-synergy/${encodeURIComponent(championId)}${rangeQuery(range)}`);
  if (!res.ok) throw new Error('Failed to fetch synergy data');
  return res.json();
}

export async function fetchTrend(championId, range) {
  const res = await fetch(`${API_BASE}/meta/trend/${encodeURIComponent(championId)}${rangeQuery(range)}`);
  if (!res.ok) throw new Error('Failed to fetch trend data');
  return res.json();
}

export async function fetchMatchups(championId, range) {
  const res = await fetch(`${API_BASE}/meta/matchups/${encodeURIComponent(championId)}${rangeQuery(range)}`);
  if (!res.ok) throw new Error('Failed to fetch matchup data');
  return res.json();
}

export async function fetchTierList(range) {
  const res = await fetch(`${API_BASE}/meta/tier-list${rangeQuery(range)}`);
  if (!res.ok) throw new Error('Failed to fetch tier list');
  return res.json();
}

export function getLegendPortraitUrl(cardId) {
  const baseId = cardId.replace(/-?A$/, '');
  return `https://riftmana.com/wp-content/uploads/Legends/${baseId}.webp`;
}
