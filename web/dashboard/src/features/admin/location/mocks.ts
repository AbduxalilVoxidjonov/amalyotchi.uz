import { http, HttpResponse, type HttpHandler } from 'msw';
import { problem } from '@/mocks/data';
import { mockTodayRows } from '../today/mocks';
import type {
  LocationPing,
  LocationPingsResponse,
  LocationPingsSummary,
  PingStatus,
  TrackPlace,
} from './types';

/** Mock kuzatuv oralig'i — kontrakt sukuti `trackingIntervalSec = 120`. */
const INTERVAL_MIN = 2;
const PROBLEM_HEADERS = { 'Content-Type': 'application/problem+json' };

/** "2026-10-14" + "09:05" (Toshkent, UTC+5) → UTC ISO. */
function at(date: string, hhmm: string): string {
  return new Date(`${date}T${hhmm}:00+05:00`).toISOString();
}

function addMin(hhmm: string, minutes: number): string {
  const [h, m] = hhmm.split(':').map(Number) as [number, number];
  const total = h * 60 + m + minutes;
  return `${String(Math.floor(total / 60)).padStart(2, '0')}:${String(total % 60).padStart(2, '0')}`;
}

/** 1° kenglik ≈ 111 km — mock uchun yetarli. */
const M_PER_DEG = 111_000;

interface PingSeed {
  time: string;
  status: PingStatus;
  isMocked?: boolean;
}

/** Sanadan barqaror "tasodifiy" son (0..1) — har kun yo'l boshqacha, lekin qayta yuklashda bir xil. */
function dateSeed(date: string): number {
  let h = 0;
  for (const ch of date) h = (h * 31 + ch.charCodeAt(0)) % 1_000_003;
  return (h % 997) / 997;
}

/**
 * Real yurishga o'xshash iz: ichkarida talaba korxona atrofida sekin aylanib yuradi (masofa va burchak
 * silliq o'zgaradi), tashqariga chiqqanda — bir yo'nalishda uzoqlashib, aylanib, qaytib keladi.
 * Shu sababli xaritada chiziq uzluksiz va yo'nalish strelkalari ma'noli.
 */
function buildTrack(date: string, place: TrackPlace, seeds: readonly PingSeed[]): LocationPing[] {
  const seed = dateSeed(date);
  const baseAngle = seed * 2 * Math.PI;
  const cosLat = Math.cos((place.lat * Math.PI) / 180);
  // Tashqarida ketma-ketliklar: har ping uchun [run ichidagi indeks, run uzunligi].
  const runPos: [number, number][] = [];
  for (let i = 0; i < seeds.length;) {
    if (seeds[i]!.status !== 'outside') {
      runPos.push([0, 0]);
      i++;
      continue;
    }
    let j = i;
    while (j < seeds.length && seeds[j]!.status === 'outside') j++;
    for (let k = i; k < j; k++) runPos.push([k - i, j - i]);
    i = j;
  }

  let angle = baseAngle;
  let exitAngle = baseAngle;
  return seeds.map((s, i) => {
    const [k, n] = runPos[i]!;
    let distanceM: number;
    if (s.status === 'outside') {
      if (k === 0) exitAngle = angle;
      // Chiqish → eng uzoq nuqta (~420 m) → qaytish; yo'l biroz egilib aylanadi.
      const t = (k + 1) / (n + 1);
      distanceM = Math.round(place.radiusM + 40 + 260 * Math.sin(Math.PI * t));
      angle = exitAngle + 0.9 * t;
    } else {
      distanceM = Math.round(25 + 45 * (0.5 - 0.5 * Math.cos(i / 6 + seed * 6)));
      angle += 0.09 + 0.05 * Math.sin(i / 11);
    }
    return {
      occurredAt: at(date, s.time),
      lat: place.lat + (Math.cos(angle) * distanceM) / M_PER_DEG,
      lng: place.lng + (Math.sin(angle) * distanceM) / (M_PER_DEG * cosLat),
      accuracyM: s.status === 'inaccurate' ? 160 : 8 + (i % 5) * 3,
      distanceM,
      status: s.status,
      isMocked: s.isMocked ?? false,
      source: i % 6 === 0 ? 'foreground' : 'background',
    };
  });
}

/** `from`..`to` oralig'ida har `INTERVAL_MIN` (2) daqiqada ping; `status(time)` holatni beradi. */
function series(
  from: string,
  to: string,
  status: (time: string) => PingStatus,
  mocked?: (time: string) => boolean,
): PingSeed[] {
  const out: PingSeed[] = [];
  for (let t = from; t <= to; t = addMin(t, INTERVAL_MIN)) {
    out.push({ time: t, status: status(t), isMocked: mocked?.(t) ?? false });
  }
  return out;
}

/** Kontrakt qoidalari bo'yicha summary: oraliq `2 × interval` da kesiladi. */
export function summarize(pings: readonly LocationPing[]): LocationPingsSummary {
  const cap = 2 * INTERVAL_MIN;
  let outsideMinutes = 0;
  let longestGapMinutes = 0;
  for (let i = 1; i < pings.length; i++) {
    const diff = (Date.parse(pings[i]!.occurredAt) - Date.parse(pings[i - 1]!.occurredAt)) / 60_000;
    longestGapMinutes = Math.max(longestGapMinutes, diff);
    if (pings[i - 1]!.status === 'outside') outsideMinutes += Math.min(diff, cap);
  }
  return {
    total: pings.length,
    inside: pings.filter((p) => p.status === 'inside').length,
    outside: pings.filter((p) => p.status === 'outside').length,
    inaccurate: pings.filter((p) => p.status === 'inaccurate').length,
    outsideMinutes: Math.round(outsideMinutes),
    longestGapMinutes: Math.round(longestGapMinutes),
    lastPingAt: pings.length ? pings[pings.length - 1]!.occurredAt : null,
  };
}

const PLACES: Record<string, TrackPlace> = {
  'Tech Solutions MChJ': { name: 'Tech Solutions MChJ', lat: 41.3111, lng: 69.2797, radiusM: 150 },
  'Agrobank ATB': { name: 'Agrobank ATB', lat: 41.3123, lng: 69.2787, radiusM: 120 },
  Mediapark: { name: 'Mediapark', lat: 41.3265, lng: 69.2285, radiusM: 150 },
  'Qurilish Trest 12': { name: 'Qurilish Trest 12', lat: 41.2995, lng: 69.2401, radiusM: 150 },
};
const FALLBACK_PLACE: TrackPlace = { name: 'Uzinfocom', lat: 41.3385, lng: 69.3346, radiusM: 150 };

interface Scenario {
  checkIn: string | null;
  checkOut: string | null;
  consentGiven: boolean;
  pings: PingSeed[];
}

/**
 * Ssenariylar (today/mocks bilan mos):
 * - Aliyev — 10:20–10:44 tashqariga chiqib qaytgan;
 * - Karimov — 11:30 dan keyin signal yo'q;
 * - Yusupova — 13:00–13:20 soxta (isMocked) ping'lar, 17:02 da ketgan;
 * - Rahimov — 11:40 dan beri tashqarida, bitta aniqligi past ping.
 */
const SCENARIOS: Record<string, Scenario> = {
  's-341030': {
    checkIn: '09:02',
    checkOut: null,
    consentGiven: true,
    pings: series('09:04', '13:00', (t) => (t >= '10:20' && t < '10:44' ? 'outside' : 'inside')),
  },
  's-341031': {
    checkIn: '09:41',
    checkOut: null,
    consentGiven: true,
    pings: series('09:45', '11:30', () => 'inside'),
  },
  's-341033': {
    checkIn: '08:57',
    checkOut: '17:02',
    consentGiven: true,
    pings: series(
      '09:00',
      '17:00',
      () => 'inside',
      (t) => t >= '13:00' && t <= '13:20',
    ),
  },
  's-341034': {
    checkIn: '09:08',
    checkOut: null,
    consentGiven: true,
    pings: series('09:10', '12:30', (t) =>
      t === '10:16' ? 'inaccurate' : t >= '11:40' ? 'outside' : 'inside',
    ),
  },
  's-341047': { checkIn: '08:45', checkOut: null, consentGiven: false, pings: [] },
};

/** Dam olish kuni (Sha/Yak) — ping yo'q (profil jadvalidagi bo'sh kunlar bilan mos). */
function noWorkDay(date: string): boolean {
  const dow = new Date(`${date}T00:00:00Z`).getUTCDay();
  return dow === 0 || dow === 6;
}

export function buildLocationPings(studentId: string, date: string): LocationPingsResponse | null {
  const row = mockTodayRows.find((r) => r.studentId === studentId);
  if (!row) return null;
  const place = row.company
    ? (PLACES[row.company] ?? { ...FALLBACK_PLACE, name: row.company })
    : null;
  const scenario: Scenario = noWorkDay(date)
    ? {
        checkIn: null,
        checkOut: null,
        consentGiven: SCENARIOS[studentId]?.consentGiven ?? true,
        pings: [],
      }
    : (SCENARIOS[studentId] ?? {
        checkIn: row.checkIn,
        checkOut: row.checkOut,
        consentGiven: true,
        pings: row.checkIn
          ? series(addMin(row.checkIn, 3), row.checkOut ?? '12:00', () => 'inside')
          : [],
      });
  const pings = place ? buildTrack(date, place, scenario.pings) : [];
  return {
    date,
    place,
    checkInAt: scenario.checkIn ? at(date, scenario.checkIn) : null,
    checkOutAt: scenario.checkOut ? at(date, scenario.checkOut) : null,
    consentGiven: scenario.consentGiven,
    pings,
    summary: summarize(pings),
  };
}

export const locationHandlers: HttpHandler[] = [
  http.get('/api/admin/students/:studentId/location-pings', ({ params, request }) => {
    const date = new URL(request.url).searchParams.get('date') ?? '';
    if (!/^\d{4}-\d{2}-\d{2}$/.test(date) || Number.isNaN(Date.parse(date))) {
      return HttpResponse.json(
        problem(400, "Ma'lumotlar noto'g'ri", 'Sana YYYY-MM-DD formatida bo‘lishi kerak.', {
          errors: { Date: ["Sana noto'g'ri."] },
        }),
        { status: 400, headers: PROBLEM_HEADERS },
      );
    }
    const data = buildLocationPings(String(params['studentId']), date);
    if (!data) {
      return HttpResponse.json(problem(404, 'Topilmadi', 'Talaba topilmadi.'), {
        status: 404,
        headers: PROBLEM_HEADERS,
      });
    }
    return HttpResponse.json(data);
  }),
];
