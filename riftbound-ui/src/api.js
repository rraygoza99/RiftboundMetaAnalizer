const API_BASE = '/api';

export async function fetchLegends() {
  const res = await fetch(`${API_BASE}/cards/legends`);
  if (!res.ok) throw new Error('Failed to fetch legends');
  return res.json();
}

export async function fetchChampionSynergy(championId) {
  const res = await fetch(`${API_BASE}/meta/champion-synergy/${encodeURIComponent(championId)}`);
  if (!res.ok) throw new Error('Failed to fetch synergy data');
  return res.json();
}

export function getLegendPortraitUrl(cardId) {
  return `https://riftmana.com/wp-content/uploads/Legends/${cardId}.webp`;
}
