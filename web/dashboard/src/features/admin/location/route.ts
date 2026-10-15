import type { LocationPing } from './types';

/**
 * Harakat yo'li geometriyasi (xaritadagi yo'nalish strelkalari uchun) — Leaflet'siz, sof funksiyalar.
 * Masofalar kichik (bir necha km) — tekis (equirectangular) yaqinlashuv yetarli.
 */

export interface LatLng {
  lat: number;
  lng: number;
}

export interface RouteArrow {
  lat: number;
  lng: number;
  /** Shimoldan soat yo'nalishida, gradus (0 = yuqoriga, 90 = o'ngga). */
  bearing: number;
}

const M_PER_DEG = 111_320;
const RAD = Math.PI / 180;

/** Ikki nuqta orasidagi taxminiy masofa (m). */
export function distanceM(a: LatLng, b: LatLng): number {
  const dy = (b.lat - a.lat) * M_PER_DEG;
  const dx = (b.lng - a.lng) * M_PER_DEG * Math.cos(((a.lat + b.lat) / 2) * RAD);
  return Math.hypot(dx, dy);
}

/**
 * Ekrandagi yo'nalish (Web Mercator'da kichik masshtabda tekis): shimoldan soat yo'nalishida 0..360.
 */
export function bearingDeg(a: LatLng, b: LatLng): number {
  const dy = b.lat - a.lat;
  const dx = (b.lng - a.lng) * Math.cos(((a.lat + b.lat) / 2) * RAD);
  const deg = Math.atan2(dx, dy) / RAD;
  return (deg + 360) % 360;
}

/** Ping'larni vaqt bo'yicha tartiblaydi (server eskidan yangiga beradi — baribir kafolat). */
export function sortByTime<T extends Pick<LocationPing, 'occurredAt'>>(pings: readonly T[]): T[] {
  return [...pings].sort((a, b) => Date.parse(a.occurredAt) - Date.parse(b.occurredAt));
}

export interface ArrowOptions {
  /** Strelkalar soni yuqori chegarasi (zich chiziqda xarita to'lib ketmasin). */
  maxArrows?: number;
  /** Bundan qisqa bo'laklar (GPS shovqini) yo'nalish ko'rsatmaydi, m. */
  minSegmentM?: number;
}

/**
 * Chiziq bo'ylab teng oraliqda yo'nalish strelkalari: umumiy uzunlik `maxArrows + 1` ga bo'linadi,
 * har belgi o'sha masofaga to'g'ri kelgan bo'lak ustida, bo'lak yo'nalishida. Juda qisqa (shovqin)
 * bo'laklar o'tkazib yuboriladi — joyida turgan talabaning "titrashi" strelka bilan to'lmaydi.
 */
export function routeArrows(points: readonly LatLng[], opts: ArrowOptions = {}): RouteArrow[] {
  const maxArrows = opts.maxArrows ?? 14;
  const minSegmentM = opts.minSegmentM ?? 8;
  if (points.length < 2 || maxArrows <= 0) return [];

  const segs: { a: LatLng; b: LatLng; len: number }[] = [];
  for (let i = 1; i < points.length; i++) {
    const a = points[i - 1]!;
    const b = points[i]!;
    const len = distanceM(a, b);
    if (len >= minSegmentM) segs.push({ a, b, len });
  }
  const total = segs.reduce((s, x) => s + x.len, 0);
  if (total === 0) return [];

  const count = Math.min(maxArrows, segs.length);
  const step = total / (count + 1);
  const out: RouteArrow[] = [];
  let walked = 0;
  let next = step;
  for (const s of segs) {
    while (next <= walked + s.len && out.length < count) {
      const t = (next - walked) / s.len;
      out.push({
        lat: s.a.lat + (s.b.lat - s.a.lat) * t,
        lng: s.a.lng + (s.b.lng - s.a.lng) * t,
        bearing: bearingDeg(s.a, s.b),
      });
      next += step;
    }
    walked += s.len;
  }
  return out;
}
