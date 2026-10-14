import { http, HttpResponse, type HttpHandler } from 'msw';
import { problem } from '@/mocks/data';
import type {
  AttendanceFilter,
  AttendanceRow,
  AttendanceStatus,
  Presence,
  PresenceFilter,
  PresenceState,
  TodayAlert,
  TodayResponse,
  TodayStats,
} from './types';
import { isAttendanceFilter, isPresenceFilter } from './types';

export const MOCK_TODAY_DATE = '2026-10-12';

type RowSeed = Omit<AttendanceRow, 'suspicious' | 'manual' | 'autoClosed'> &
  Partial<Pick<AttendanceRow, 'suspicious' | 'manual' | 'autoClosed'>>;

const row = (seed: RowSeed): AttendanceRow => ({
  suspicious: false,
  manual: false,
  autoClosed: false,
  ...seed,
});

/** SPEC-SCREENS §3 mock — 6 ta qator aynan; qolgan 32 tasi generatsiya (38 talaba). */
const SPEC_ROWS: AttendanceRow[] = [
  row({
    studentId: 's-341030',
    name: 'Aliyev Akmal',
    group: '412-22',
    company: 'Tech Solutions MChJ',
    checkIn: '09:02',
    checkOut: null,
    diary: 'written',
    distanceM: 45,
    outOfRadius: false,
    status: 'present',
  }),
  row({
    studentId: 's-341031',
    name: 'Karimov Bekzod',
    group: '412-22',
    company: 'Agrobank ATB',
    checkIn: '09:41',
    checkOut: null,
    diary: 'pending',
    distanceM: 85,
    outOfRadius: false,
    status: 'late',
  }),
  row({
    studentId: 's-341032',
    name: 'Sobirov Diyor',
    group: '413-22',
    company: 'Uzinfocom',
    checkIn: null,
    checkOut: null,
    diary: null,
    distanceM: null,
    outOfRadius: false,
    status: 'absent',
  }),
  row({
    studentId: 's-341033',
    name: 'Yusupova Nilufar',
    group: '413-22',
    company: 'Mediapark',
    checkIn: '08:57',
    checkOut: '17:02',
    diary: 'written',
    distanceM: 30,
    outOfRadius: false,
    status: 'present',
  }),
  row({
    studentId: 's-341034',
    name: 'Rahimov Sardor',
    group: '412-22',
    company: 'Qurilish Trest 12',
    checkIn: '09:08',
    checkOut: null,
    diary: 'written',
    distanceM: 410,
    outOfRadius: true,
    status: 'present',
    suspicious: true,
  }),
  row({
    studentId: 's-341035',
    name: 'Toshpulatova Zarina',
    group: '413-22',
    company: 'Ipak Yuli Bank',
    checkIn: null,
    checkOut: null,
    diary: null,
    distanceM: null,
    outOfRadius: false,
    status: 'excused',
  }),
];

const EXTRA_NAMES = [
  'Abdullayev Jasur',
  'Boboyeva Madina',
  'Ergashev Sherzod',
  'Fayzullayeva Kamola',
  'Gʻaniyev Otabek',
  'Hasanova Dilnoza',
  'Ismoilov Javohir',
  'Jalilova Sevara',
  'Kamolov Bobur',
  'Latipova Gulnora',
  'Mahmudov Umid',
  'Nurmatova Feruza',
  'Olimov Doston',
  'Primova Nigora',
  'Qosimov Rustam',
  'Rasulova Shahnoza',
  'Saidov Alisher',
  'Tursunova Malika',
  'Umarov Farrux',
  'Valiyeva Zulfiya',
  'Xolmatov Sanjar',
  'Yoqubova Nilufar',
  'Zokirov Islom',
  'Axmedova Laylo',
  'Berdiyev Shohrux',
  'Choriyeva Mohira',
  'Davronov Temur',
  'Eshonova Dildora',
  'Farhodov Aziz',
  'Gulomova Sitora',
  'Hakimov Bekzod',
  'Ibrohimova Nodira',
];
const COMPANIES = [
  'Tech Solutions MChJ',
  'Agrobank ATB',
  'Uzinfocom',
  'Mediapark',
  'Ipak Yuli Bank',
];

/** Stat: Keldi 28 · Kech 3 · Kelmadi 5 · Sababli 2 = 38 (shubhali — "Keldi" ichida, bayroq bilan). */
function generateExtra(): AttendanceRow[] {
  const plan: AttendanceStatus[] = [
    ...Array<AttendanceStatus>(25).fill('present'),
    'late',
    'late',
    'absent',
    'absent',
    'absent',
    'absent',
    'excused',
  ];
  return EXTRA_NAMES.map((name, i) => {
    const status = plan[i] ?? 'present';
    const checkedIn = status === 'present' || status === 'late';
    const minute = String(10 + ((i * 7) % 45)).padStart(2, '0');
    const live = i === 4 || i === 7; // "Aniqlik past" / "Rozilik yo'q" — sessiya hali ochiq
    return row({
      studentId: `s-3410${40 + i}`,
      name,
      group: i % 2 === 0 ? '412-22' : '413-22',
      company: i === 5 ? null : COMPANIES[i % COMPANIES.length]!,
      checkIn: checkedIn ? `${status === 'late' ? '09' : '08'}:${minute}` : null,
      checkOut: checkedIn && !live && i % 3 !== 0 ? `17:${minute}` : null,
      diary: checkedIn ? (i % 4 === 0 ? 'pending' : 'written') : null,
      distanceM: checkedIn ? 20 + ((i * 13) % 110) : null,
      outOfRadius: false,
      status,
      autoClosed: checkedIn && !live && i % 3 === 0,
    });
  });
}

export const mockTodayRows: AttendanceRow[] = [...SPEC_ROWS, ...generateExtra()];

/**
 * Jonli joylashuv (LIVE-JOYLASHUV.md) — `lastPingAt` so'rov paytiga nisbatan hisoblanadi (`agoMin`).
 * Aliyev — tashqariga chiqib qaytgan (hozir ichkarida, 25 daq tashqarida bo'lgan) · Karimov — signal yo'q ·
 * Rahimov — hozir tashqarida · Yusupova — ketgan, kunda soxta ping bo'lgan.
 */
interface PresenceSeed {
  state: PresenceState;
  agoMin: number | null;
  distanceM: number | null;
  outsideMinutes?: number;
  mocked?: boolean;
}

const PRESENCE_SEEDS: Record<string, PresenceSeed> = {
  's-341030': { state: 'inside', agoMin: 3, distanceM: 42, outsideMinutes: 25 },
  's-341031': { state: 'signalLost', agoMin: 47, distanceM: 60 },
  's-341033': { state: 'stopped', agoMin: 190, distanceM: 30, mocked: true },
  's-341034': { state: 'outside', agoMin: 2, distanceM: 410, outsideMinutes: 18 },
  's-341044': { state: 'inaccurate', agoMin: 4, distanceM: 130 },
  's-341047': { state: 'noConsent', agoMin: null, distanceM: null },
};

function presenceSeed(r: AttendanceRow): PresenceSeed | null {
  const seed = PRESENCE_SEEDS[r.studentId];
  if (seed) return seed;
  if (r.checkIn === null) return null;
  if (r.checkOut !== null || r.autoClosed) return { state: 'stopped', agoMin: 240, distanceM: 35 };
  return { state: 'inside', agoMin: 5, distanceM: r.distanceM };
}

function withPresence(r: AttendanceRow, now: number): AttendanceRow {
  const seed = presenceSeed(r);
  const presence: Presence | null = seed
    ? {
        state: seed.state,
        lastPingAt:
          seed.agoMin === null ? null : new Date(now - seed.agoMin * 60_000).toISOString(),
        distanceM: seed.distanceM,
        outsideMinutes: seed.outsideMinutes ?? 0,
        mocked: seed.mocked ?? false,
      }
    : null;
  return { ...r, presence };
}

const PAGE_SIZE = 6;

function matchesFilter(r: AttendanceRow, filter: AttendanceFilter): boolean {
  if (filter === 'all') return true;
  if (filter === 'suspicious') return r.suspicious || r.outOfRadius;
  return r.status === filter;
}

function stats(rows: readonly AttendanceRow[]): TodayStats {
  const count = (s: AttendanceStatus) => rows.filter((r) => r.status === s).length;
  return {
    present: count('present'),
    late: count('late'),
    absent: count('absent'),
    excused: count('excused'),
    pending: count('pending'),
    diaries: rows.filter((r) => r.diary === 'written').length,
    total: rows.length,
  };
}

export function buildTodayResponse(
  filter: AttendanceFilter,
  page: number,
  q = '',
  presence: PresenceFilter = 'all',
  now: number = Date.now(),
): TodayResponse {
  const needle = q.trim().toLowerCase();
  const filtered = mockTodayRows
    .map((r) => withPresence(r, now))
    .filter(
      (r) =>
        matchesFilter(r, filter) &&
        (!needle || r.name.toLowerCase().includes(needle)) &&
        (presence === 'all' || r.presence?.state === presence),
    );
  const safePage = Math.max(1, page);
  const start = (safePage - 1) * PAGE_SIZE;
  const items = filtered.slice(start, start + PAGE_SIZE);
  const s = stats(mockTodayRows);
  const alerts: TodayAlert[] = [
    { kind: 'outOfRadius', count: 3, href: '/admin/map', maxDistanceM: 3400 },
    {
      kind: 'notCheckedIn',
      count: s.absent,
      href: '/admin/today?status=absent',
      maxDistanceM: null,
    },
    { kind: 'newApplications', count: 7, href: '/admin/applications', maxDistanceM: null },
  ];
  return {
    date: MOCK_TODAY_DATE,
    stats: s,
    alerts,
    rows: { items, page: safePage, pageSize: PAGE_SIZE, total: filtered.length },
  };
}

export const todayHandlers: HttpHandler[] = [
  http.get('/api/admin/today', ({ request }) => {
    const url = new URL(request.url);
    const status = url.searchParams.get('status');
    const page = Number(url.searchParams.get('page') ?? '1') || 1;
    const q = url.searchParams.get('q') ?? '';
    if (status !== null && (!isAttendanceFilter(status) || status === 'all')) {
      return HttpResponse.json(
        problem(400, 'One or more validation errors occurred.', '', {
          errors: { Status: [`The value '${status}' is not valid for Status.`] },
        }),
        { status: 400 },
      );
    }
    // `presence` — backend `TodayPresenceFilter` (outside | signalLost); noma'lum qiymat e'tiborsiz.
    const presence = url.searchParams.get('presence');
    return HttpResponse.json(
      buildTodayResponse(status ?? 'all', page, q, isPresenceFilter(presence) ? presence : 'all'),
    );
  }),
];
