import type { MapPoint } from './types';

/**
 * Manzil bo'yicha qidiruv (OpenStreetMap Nominatim). Foydalanish qoidasi: so'rov faqat
 * foydalanuvchi "Topish"ni bosganda yuboriladi (avtomatik to'ldirish yo'q) — 1 so'rov/s chegarasidan
 * oshmaydi.
 */
export const NOMINATIM_URL = 'https://nominatim.openstreetmap.org/search';

export interface GeocodeResult extends MapPoint {
  label: string;
}

interface NominatimItem {
  lat: string;
  lon: string;
  display_name: string;
}

export async function geocode(query: string, signal?: AbortSignal): Promise<GeocodeResult[]> {
  const sp = new URLSearchParams({
    q: query,
    format: 'jsonv2',
    countrycodes: 'uz',
    limit: '5',
    'accept-language': 'uz,ru',
  });
  const res = await fetch(`${NOMINATIM_URL}?${sp.toString()}`, { signal: signal ?? null });
  if (!res.ok) throw new Error(`geocode ${res.status}`);
  const items = (await res.json()) as NominatimItem[];
  return items
    .map((it) => ({ lat: Number(it.lat), lng: Number(it.lon), label: it.display_name }))
    .filter((r) => Number.isFinite(r.lat) && Number.isFinite(r.lng));
}
