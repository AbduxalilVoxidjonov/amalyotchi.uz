import { http, HttpResponse, type HttpHandler } from 'msw';
import { mockDepartments } from '../faculties/departments/mocks';
import { mockDirections } from '../faculties/directions/mocks';
import { mockGroups } from '../faculties/groups/mocks';
import { mockFaculties } from '../faculties/mocks';
import { problemResponse } from '../shared/mockProblem';
import { mockStudents as adminMockStudents } from '../students/mocks';
import { PRACTICE_PERIODS_ENDPOINT } from './api';
import { countWorkDays, normalizeWeekdays } from '../shared/weekdays';
import { isIsoDate, rangesOverlap, todayIso } from './dates';
import type {
  GradeDistribution,
  GroupMetrics,
  PeriodGroupStats,
  PeriodGroupStudent,
  PeriodGroupStudents,
  PracticePeriodCreate,
  PracticePeriodDetail,
  PracticePeriodGroup,
  PracticePeriodGroupsUpdate,
  PracticePeriodListItem,
  PracticePeriodSchedule,
  PracticePeriodStats,
  PracticePeriodStatus,
  PracticePeriodUpdate,
} from './types';
import { isPeriodStatus } from './types';

/**
 * In-memory amaliyot davrlari (kontraktdagi 409/400 qoidalari bilan).
 * Guruh ma'lumotlari fakultet/kafedra/yo'nalish/guruh mock URUG'laridan olinadi — urug'da yo'q
 * (ish vaqtida yaratilgan) guruh id'si 400 qaytaradi.
 *
 * ❓ `GET /directions/{id}/groups` dagi `period` maydoni groups mock'ida statik (g1, g2, g4 → p1);
 * shu yerdagi o'zgarishlar u yerga aks etmaydi — real backendda bir manba.
 */
interface PeriodSeed extends PracticePeriodSchedule {
  id: string;
  name: string;
  startDate: string;
  endDate: string;
  status: PracticePeriodStatus;
  createdAt: string;
  groupIds: string[];
  /** Shu davrda davomat yozuvi bor guruhlar (ajratish/o'chirish → 409). */
  attendanceGroupIds: string[];
}

/**
 * Global sozlamalar — settings mock'idagi demo qiymatlar. Jadval (vaqt, ish kunlari) so'rovda
 * kelmasa shular olinadi; kelsa — davrga o'zi saqlanadi.
 */
const GLOBAL_SCHEDULE: PracticePeriodSchedule = {
  dailyStart: '09:00',
  dailyEnd: '17:00',
  workDays: '1,2,3,4,5,6',
};
const GLOBAL_DAILY_REPORT_REQUIRED = true;

export const mockPracticePeriods: PeriodSeed[] = [
  {
    // groups mock'idagi `PERIOD` bilan bir xil (g1, g2, g4 shu davrda).
    id: 'p1',
    name: 'Ishlab chiqarish amaliyoti 2026',
    startDate: '2026-08-31',
    endDate: '2026-10-14',
    status: 'active',
    createdAt: '2026-08-01T09:00:00+05:00',
    groupIds: ['g1', 'g2', 'g4'],
    attendanceGroupIds: ['g1'],
    ...GLOBAL_SCHEDULE,
  },
  {
    id: 'p3',
    name: 'Qishki amaliyot 2027',
    startDate: '2027-01-11',
    endDate: '2027-02-20',
    status: 'planned',
    createdAt: '2026-09-10T10:00:00+05:00',
    groupIds: ['g3'],
    attendanceGroupIds: [],
    ...GLOBAL_SCHEDULE,
  },
  {
    id: 'p2',
    name: '2-kurs bahorgi amaliyot',
    startDate: '2026-02-10',
    endDate: '2026-04-10',
    status: 'closed',
    createdAt: '2026-01-20T09:00:00+05:00',
    groupIds: ['g4'],
    attendanceGroupIds: ['g4'],
    ...GLOBAL_SCHEDULE,
  },
];

let state: PeriodSeed[] = structuredClone(mockPracticePeriods);
let nextId = 100;

export function resetPracticePeriodsMock() {
  state = structuredClone(mockPracticePeriods);
  nextId = 100;
}

function groupInfo(id: string): PracticePeriodGroup | null {
  const g = mockGroups.find((x) => x.id === id);
  if (!g || !g.isActive) return null;
  const direction = mockDirections.find((d) => d.id === g.directionId);
  const department = mockDepartments.find((d) => d.id === direction?.departmentId);
  const faculty = mockFaculties.find((f) => f.id === department?.facultyId);
  return {
    id: g.id,
    code: g.code,
    course: g.course,
    studentsCount: g.students,
    facultyId: faculty?.id ?? '',
    facultyName: faculty?.name ?? '',
    departmentId: department?.id ?? '',
    departmentName: department?.name ?? '',
    directionId: direction?.id ?? '',
    directionName: direction?.name ?? '',
  };
}

/** Ish kunlari soni (bayramlarsiz — mock'da bayram hisobga olinmaydi). */
function workDaysBetween(start: string, end: string, csv: string): number {
  return countWorkDays(start, end, csv) ?? 0;
}

function toListItem(p: PeriodSeed): PracticePeriodListItem {
  const groups = p.groupIds.map(groupInfo).filter((g): g is PracticePeriodGroup => g !== null);
  return {
    id: p.id,
    name: p.name,
    startDate: p.startDate,
    endDate: p.endDate,
    status: p.status,
    groupsCount: groups.length,
    studentsCount: groups.reduce((s, g) => s + g.studentsCount, 0),
    createdAt: p.createdAt,
  };
}

export function mockPracticePeriodDetail(id: string): PracticePeriodDetail | null {
  const p = state.find((x) => x.id === id);
  if (!p) return null;
  return {
    ...toListItem(p),
    dailyStart: p.dailyStart,
    dailyEnd: p.dailyEnd,
    workDays: p.workDays,
    dailyReportRequired: GLOBAL_DAILY_REPORT_REQUIRED,
    requiredDays: workDaysBetween(p.startDate, p.endDate, p.workDays),
    groups: p.groupIds.map(groupInfo).filter((g): g is PracticePeriodGroup => g !== null),
  };
}

function validationProblem(errors: Record<string, string[]>) {
  return problemResponse(400, "Ma'lumotlar noto'g'ri", "Kiritilgan ma'lumotlarda xatolik bor.", {
    errors,
  });
}

function validateDates(body: Partial<PracticePeriodUpdate>): Record<string, string[]> {
  const errors: Record<string, string[]> = {};
  const name = typeof body.name === 'string' ? body.name.trim() : '';
  if (!name) errors['name'] = ['Davr nomini kiriting.'];
  else if (name.length > 200) errors['name'] = ['Nom 200 belgidan oshmasligi kerak.'];
  const start = body.startDate ?? '';
  const end = body.endDate ?? '';
  if (!isIsoDate(start)) errors['startDate'] = ['Boshlanish sanasini kiriting.'];
  if (!isIsoDate(end)) errors['endDate'] = ['Tugash sanasini kiriting.'];
  else if (isIsoDate(start) && end < start)
    errors['endDate'] = ["Tugash sanasi boshlanish sanasidan oldin bo'lishi mumkin emas."];
  return errors;
}

const HH_MM = /^([01]\d|2[0-3]):[0-5]\d$/;

/**
 * Jadval maydonlari (ixtiyoriy): kelmasa `fallback`; kelsa — "HH:mm", `dailyEnd > dailyStart`,
 * kamida bitta 1..7 kun. Natija: normallashtirilgan jadval yoki `errors` ga yozilgan xatolar.
 */
function resolveSchedule(
  body: Partial<PracticePeriodSchedule>,
  fallback: PracticePeriodSchedule,
  errors: Record<string, string[]>,
): PracticePeriodSchedule {
  const dailyStart = body.dailyStart ?? fallback.dailyStart;
  const dailyEnd = body.dailyEnd ?? fallback.dailyEnd;
  const workDays =
    body.workDays === undefined ? fallback.workDays : normalizeWeekdays(body.workDays);
  if (!HH_MM.test(dailyStart))
    errors['DailyStart'] = ["Boshlanish vaqti HH:mm ko'rinishida bo'lsin."];
  if (!HH_MM.test(dailyEnd)) errors['DailyEnd'] = ["Tugash vaqti HH:mm ko'rinishida bo'lsin."];
  else if (HH_MM.test(dailyStart) && dailyEnd <= dailyStart)
    errors['DailyEnd'] = ["Ish tugash vaqti boshlanish vaqtidan keyin bo'lishi kerak."];
  if (!workDays) errors['WorkDays'] = ['Kamida bitta ish kunini tanlang.'];
  return { dailyStart, dailyEnd, workDays };
}

/** Ustma-ust: shu guruhlar sanalari kesishadigan boshqa yopilmagan davrdami. */
function overlapConflict(
  selfId: string | null,
  groupIds: readonly string[],
  start: string,
  end: string,
): string | null {
  const hits: string[] = [];
  for (const gid of groupIds) {
    for (const p of state) {
      if (p.id === selfId || p.status === 'closed') continue;
      if (!p.groupIds.includes(gid)) continue;
      if (!rangesOverlap(start, end, p.startDate, p.endDate)) continue;
      hits.push(`${groupInfo(gid)?.code ?? gid} (${p.name})`);
    }
  }
  return hits.length > 0
    ? `Quyidagi guruhlar shu sanalarda boshqa davrga biriktirilgan: ${hits.join(', ')}`
    : null;
}

function statusFor(start: string): PracticePeriodStatus {
  return start > todayIso() ? 'planned' : 'active';
}

/* ────────────────────────────────────────────────────────────────────────────
 * Ko'rsatkichlar (stats) — deterministik: bir xil davr+guruh uchun har safar bir xil talabalar.
 * Davr jami (`totals`) va guruh ko'rsatkichlari talabalar ro'yxatidan yig'iladi — raqamlar mos.
 * ──────────────────────────────────────────────────────────────────────────── */

const FIRST_NAMES = [
  'Akmal',
  'Dilnoza',
  'Jasur',
  'Madina',
  'Sardor',
  'Nigora',
  'Bekzod',
  'Shahzoda',
  'Otabek',
  'Zarina',
  'Javohir',
  'Malika',
  'Sherzod',
  'Gulnoza',
  'Doston',
  'Kamola',
  'Ulugbek',
  'Feruza',
  'Aziz',
  'Sevara',
  'Rustam',
  'Laylo',
  'Temur',
  'Munisa',
];
const LAST_NAMES = [
  'Karimov',
  'Yusupova',
  'Rahimov',
  'Tosheva',
  'Qodirov',
  'Ergasheva',
  'Nurmatov',
  'Sobirova',
  'Hamidov',
  'Aliyeva',
  'Xolmatov',
  'Umarova',
  'Mirzayev',
  'Saidova',
  'Normurodov',
  'Abdullayeva',
];
const COMPANIES = [
  'Tech Solutions MChJ',
  'Uzinfocom',
  'Agrobank ATB',
  'Mega Servis MChJ',
  "Ipak Yo'li Logistika",
  'Qurilish Trest 12',
  'EPAM Uzbekistan',
];

/** mulberry32 — urug'li PRNG. */
function seededRandom(seedText: string): () => number {
  let a = 0;
  for (const ch of seedText) a = (Math.imul(a, 31) + ch.charCodeAt(0)) | 0;
  return () => {
    a = (a + 0x6d2b79f5) | 0;
    let t = Math.imul(a ^ (a >>> 15), 1 | a);
    t = (t + Math.imul(t ^ (t >>> 7), 61 | t)) ^ t;
    return ((t ^ (t >>> 14)) >>> 0) / 4294967296;
  };
}

const round1 = (n: number) => Math.round(n * 10) / 10;

function elapsedWorkDaysOf(p: PeriodSeed): number {
  if (p.status === 'planned') return 0;
  const today = todayIso();
  const end = p.status === 'closed' || today > p.endDate ? p.endDate : today;
  return end < p.startDate ? 0 : workDaysBetween(p.startDate, end, p.workDays);
}

/** Tyutor baholash qoidasi: davomat < 70% yoki jami < 56 → qayta topshiradi (null). */
function gradeOf(attendancePct: number, total: number): number | null {
  if (attendancePct < 70 || total < 56) return null;
  if (total >= 86) return 5;
  if (total >= 71) return 4;
  return 3;
}

interface GeneratedStudent extends PeriodGroupStudent {
  /** Faqat yig'indi uchun (kontraktda talaba darajasida yo'q). */
  diaryApproved: number;
}

function generateStudents(p: PeriodSeed, groupId: string, count: number): GeneratedStudent[] {
  const rand = seededRandom(`${p.id}:${groupId}`);
  const pick = <T>(xs: readonly T[]) => xs[Math.floor(rand() * xs.length)] as T;
  const elapsed = elapsedWorkDaysOf(p);
  const closed = p.status === 'closed';
  const known = adminMockStudents.filter((s) => s.groupId === groupId);

  return Array.from({ length: count }, (_, i): GeneratedStudent => {
    const seedStudent = known[i];
    const fullName = seedStudent?.fullName ?? `${pick(LAST_NAMES)} ${pick(FIRST_NAMES)}`;
    const hemisId = seedStudent?.hemisId ?? String(340000 + Math.floor(rand() * 9000));
    const id = seedStudent?.id ?? `${groupId}-s${i + 1}`;

    const hasCompany = rand() < 0.85;
    const company = hasCompany ? (seedStudent?.company ?? pick(COMPANIES)) : null;
    const applicationStatus: PeriodGroupStudent['applicationStatus'] = hasCompany
      ? closed
        ? 'completed'
        : 'approved'
      : pick(['submitted', 'revisionNeeded', 'draft', null] as const);

    if (elapsed === 0) {
      return {
        id,
        fullName,
        hemisId,
        company,
        applicationStatus,
        attendancePct: 0,
        presentDays: 0,
        lateDays: 0,
        absentDays: 0,
        excusedDays: 0,
        suspiciousDays: 0,
        diaryCount: 0,
        diaryAvg: 0,
        diaryApproved: 0,
        attendancePoints: 0,
        reportPoints: 0,
        tutorPoints: null,
        referencePoints: null,
        total: 0,
        grade: null,
        finalized: false,
      };
    }

    const weak = rand() < 0.15 || !hasCompany;
    const targetPct = weak ? 45 + rand() * 24 : 76 + rand() * 24;
    const excusedDays = rand() < 0.25 ? 1 : 0;
    const attended = Math.min(elapsed - excusedDays, Math.round((elapsed * targetPct) / 100));
    const lateDays = Math.min(attended, Math.floor(rand() * 3));
    const presentDays = attended - lateDays;
    const absentDays = Math.max(0, elapsed - attended - excusedDays);
    const attendancePct = Math.round((attended / Math.max(1, elapsed - excusedDays)) * 100);
    const suspiciousDays = rand() < 0.12 ? 1 + Math.floor(rand() * 3) : 0;

    const diaryCount = hasCompany ? Math.round(attended * (0.6 + rand() * 0.4)) : 0;
    const diaryAvg = diaryCount > 0 ? round1(3.2 + rand() * 1.8) : 0;
    const diaryApproved = Math.floor(diaryCount * (0.7 + rand() * 0.3));

    const attendancePoints = round1(attendancePct * 0.4);
    const reportPoints = round1((diaryAvg / 5) * 30 * Math.min(1, diaryCount / elapsed));
    const scored = closed || rand() < 0.5;
    const tutorPoints = scored ? 12 + Math.floor(rand() * 9) : null;
    const referencePoints = scored ? 5 + Math.floor(rand() * 6) : null;
    const total = round1(
      attendancePoints + reportPoints + (tutorPoints ?? 0) + (referencePoints ?? 0),
    );

    return {
      id,
      fullName,
      hemisId,
      company,
      applicationStatus,
      attendancePct,
      presentDays,
      lateDays,
      absentDays,
      excusedDays,
      suspiciousDays,
      diaryCount,
      diaryAvg,
      diaryApproved,
      attendancePoints,
      reportPoints,
      tutorPoints,
      referencePoints,
      total,
      grade: gradeOf(attendancePct, total),
      finalized: closed || (scored && rand() < 0.3),
    };
  });
}

const EMPTY_GRADES: GradeDistribution = {
  excellent: 0,
  good: 0,
  satisfactory: 0,
  unsatisfactory: 0,
  retake: 0,
};

function aggregate(rows: readonly GeneratedStudent[], started: boolean): GroupMetrics {
  const n = rows.length;
  const sum = (f: (s: GeneratedStudent) => number) => rows.reduce((acc, s) => acc + f(s), 0);
  const diaryCount = sum((s) => s.diaryCount);
  const grades = { ...EMPTY_GRADES };
  if (started) {
    for (const s of rows) {
      if (s.grade === 5) grades.excellent++;
      else if (s.grade === 4) grades.good++;
      else if (s.grade === 3) grades.satisfactory++;
      else if (s.grade === 2) grades.unsatisfactory++;
      else grades.retake++;
    }
  }
  return {
    studentsCount: n,
    attendancePct: started && n > 0 ? Math.round(sum((s) => s.attendancePct) / n) : 0,
    lowAttendanceCount: started ? rows.filter((s) => s.attendancePct < 70).length : 0,
    suspiciousDays: sum((s) => s.suspiciousDays),
    withCompanyCount: rows.filter((s) => s.company !== null).length,
    pendingApplicationsCount: rows.filter(
      (s) => s.applicationStatus === 'submitted' || s.applicationStatus === 'revisionNeeded',
    ).length,
    diaryCount,
    diaryApprovedCount: sum((s) => s.diaryApproved),
    diaryAvgScore: diaryCount > 0 ? round1(sum((s) => s.diaryAvg * s.diaryCount) / diaryCount) : 0,
    avgTotal: started && n > 0 ? round1(sum((s) => s.total) / n) : null,
    finalizedCount: rows.filter((s) => s.finalized).length,
    grades,
  };
}

function studentsOf(p: PeriodSeed, groupId: string): GeneratedStudent[] {
  const info = groupInfo(groupId);
  return info ? generateStudents(p, groupId, info.studentsCount) : [];
}

const strip = ({ diaryApproved: _unused, ...s }: GeneratedStudent): PeriodGroupStudent => s;

export function mockPracticePeriodStats(id: string): PracticePeriodStats | null {
  const p = state.find((x) => x.id === id);
  if (!p) return null;
  const elapsed = elapsedWorkDaysOf(p);
  const started = elapsed > 0;
  const all: GeneratedStudent[] = [];
  const groups: PeriodGroupStats[] = [];
  for (const gid of p.groupIds) {
    const info = groupInfo(gid);
    if (!info) continue;
    const rows = studentsOf(p, gid);
    all.push(...rows);
    groups.push({
      groupId: info.id,
      code: info.code,
      course: info.course,
      directionName: info.directionName,
      ...aggregate(rows, started),
    });
  }
  return {
    periodId: p.id,
    elapsedWorkDays: elapsed,
    requiredDays: workDaysBetween(p.startDate, p.endDate, p.workDays),
    totals: aggregate(all, started),
    groups,
  };
}

export function mockPeriodGroupStudents(id: string, groupId: string): PeriodGroupStudents | null {
  const p = state.find((x) => x.id === id);
  const info = groupInfo(groupId);
  if (!p || !info || !p.groupIds.includes(groupId)) return null;
  const elapsed = elapsedWorkDaysOf(p);
  const rows = studentsOf(p, groupId);
  return {
    period: {
      id: p.id,
      name: p.name,
      status: p.status,
      startDate: p.startDate,
      endDate: p.endDate,
    },
    group: {
      id: info.id,
      code: info.code,
      course: info.course,
      facultyName: info.facultyName,
      directionName: info.directionName,
    },
    elapsedWorkDays: elapsed,
    metrics: aggregate(rows, elapsed > 0),
    students: rows.map(strip),
  };
}

const notFound = () => problemResponse(404, 'Topilmadi', 'Amaliyot davri topilmadi.');
const closedConflict = () =>
  problemResponse(409, 'Ziddiyat', "Yopilgan davrni o'zgartirib bo'lmaydi.");

export const practicePeriodsHandlers: HttpHandler[] = [
  http.get(`${PRACTICE_PERIODS_ENDPOINT}/:id/stats`, ({ params }) => {
    const stats = mockPracticePeriodStats(String(params['id']));
    return stats ? HttpResponse.json(stats) : notFound();
  }),

  http.get(`${PRACTICE_PERIODS_ENDPOINT}/:id/groups/:groupId/students`, ({ params }) => {
    const id = String(params['id']);
    if (!state.some((p) => p.id === id)) return notFound();
    const data = mockPeriodGroupStudents(id, String(params['groupId']));
    return data
      ? HttpResponse.json(data)
      : problemResponse(404, 'Topilmadi', 'Guruh bu amaliyot davriga biriktirilmagan.');
  }),

  http.get(PRACTICE_PERIODS_ENDPOINT, ({ request }) => {
    const status = new URL(request.url).searchParams.get('status');
    const rows = state
      .filter((p) => !isPeriodStatus(status) || p.status === status)
      .sort((a, b) => b.startDate.localeCompare(a.startDate))
      .map(toListItem);
    return HttpResponse.json(rows);
  }),

  http.get(`${PRACTICE_PERIODS_ENDPOINT}/:id`, ({ params }) => {
    const detail = mockPracticePeriodDetail(String(params['id']));
    return detail ? HttpResponse.json(detail) : notFound();
  }),

  http.post(PRACTICE_PERIODS_ENDPOINT, async ({ request }) => {
    const body = (await request.json().catch(() => ({}))) as Partial<PracticePeriodCreate>;
    const errors = validateDates(body);
    const schedule = resolveSchedule(body, GLOBAL_SCHEDULE, errors);
    const groupIds = Array.isArray(body.groupIds) ? [...new Set(body.groupIds)] : [];
    if (groupIds.length === 0) errors['groupIds'] = ['Kamida bitta guruh tanlang.'];
    else if (groupIds.some((id) => !groupInfo(id)))
      errors['groupIds'] = ['Tanlangan guruhlardan biri topilmadi yoki faol emas.'];
    if (Object.keys(errors).length > 0) return validationProblem(errors);

    const input = body as PracticePeriodCreate;
    const conflict = overlapConflict(null, groupIds, input.startDate, input.endDate);
    if (conflict) return problemResponse(409, 'Ziddiyat', conflict);

    const id = `p${nextId++}`;
    state = [
      ...state,
      {
        id,
        name: input.name.trim(),
        startDate: input.startDate,
        endDate: input.endDate,
        status: statusFor(input.startDate),
        createdAt: new Date().toISOString(),
        groupIds,
        attendanceGroupIds: [],
        ...schedule,
      },
    ];
    return HttpResponse.json(mockPracticePeriodDetail(id), { status: 201 });
  }),

  http.put(`${PRACTICE_PERIODS_ENDPOINT}/:id/groups`, async ({ request, params }) => {
    const id = String(params['id']);
    const period = state.find((p) => p.id === id);
    if (!period) return notFound();
    if (period.status === 'closed') return closedConflict();

    const body = (await request.json().catch(() => ({}))) as Partial<PracticePeriodGroupsUpdate>;
    const groupIds = Array.isArray(body.groupIds) ? [...new Set(body.groupIds)] : [];
    if (groupIds.some((g) => !groupInfo(g))) {
      return validationProblem({
        groupIds: ['Tanlangan guruhlardan biri topilmadi yoki faol emas.'],
      });
    }
    const removedWithAttendance = period.groupIds
      .filter((g) => !groupIds.includes(g) && period.attendanceGroupIds.includes(g))
      .map((g) => groupInfo(g)?.code ?? g);
    if (removedWithAttendance.length > 0) {
      return problemResponse(
        409,
        'Ziddiyat',
        `${removedWithAttendance.join(', ')} guruhi talabalarining shu davrda davomat yozuvlari bor — ajratib bo'lmaydi.`,
      );
    }
    const added = groupIds.filter((g) => !period.groupIds.includes(g));
    const conflict = overlapConflict(id, added, period.startDate, period.endDate);
    if (conflict) return problemResponse(409, 'Ziddiyat', conflict);

    state = state.map((p) => (p.id === id ? { ...p, groupIds } : p));
    return HttpResponse.json(mockPracticePeriodDetail(id));
  }),

  http.post(`${PRACTICE_PERIODS_ENDPOINT}/:id/close`, ({ params }) => {
    const id = String(params['id']);
    const period = state.find((p) => p.id === id);
    if (!period) return notFound();
    if (period.status === 'closed') {
      return problemResponse(409, 'Ziddiyat', 'Davr allaqachon yopilgan.');
    }
    state = state.map((p) => (p.id === id ? { ...p, status: 'closed' } : p));
    return HttpResponse.json(mockPracticePeriodDetail(id));
  }),

  http.put(`${PRACTICE_PERIODS_ENDPOINT}/:id`, async ({ request, params }) => {
    const id = String(params['id']);
    const period = state.find((p) => p.id === id);
    if (!period) return notFound();
    if (period.status === 'closed') return closedConflict();

    const body = (await request.json().catch(() => ({}))) as Partial<PracticePeriodUpdate>;
    const errors = validateDates(body);
    const schedule = resolveSchedule(body, period, errors);
    if (period.status === 'active' && body.startDate !== period.startDate)
      errors['startDate'] = ["Faol davrning boshlanish sanasini o'zgartirib bo'lmaydi."];
    if (
      period.status === 'active' &&
      !errors['endDate'] &&
      body.endDate !== period.endDate &&
      (body.endDate ?? '') < todayIso()
    )
      errors['endDate'] = ["Faol davrning tugash sanasi bugundan oldin bo'lishi mumkin emas."];
    if (Object.keys(errors).length > 0) return validationProblem(errors);

    const input = body as PracticePeriodUpdate;
    if (input.startDate !== period.startDate || input.endDate !== period.endDate) {
      const conflict = overlapConflict(id, period.groupIds, input.startDate, input.endDate);
      if (conflict) return problemResponse(409, 'Ziddiyat', conflict);
    }
    state = state.map((p) =>
      p.id === id
        ? {
            ...p,
            name: input.name.trim(),
            startDate: input.startDate,
            endDate: input.endDate,
            ...schedule,
            status: p.status === 'planned' ? statusFor(input.startDate) : p.status,
          }
        : p,
    );
    return HttpResponse.json(mockPracticePeriodDetail(id));
  }),

  http.delete(`${PRACTICE_PERIODS_ENDPOINT}/:id`, ({ params }) => {
    const id = String(params['id']);
    const period = state.find((p) => p.id === id);
    if (!period) return notFound();
    if (period.attendanceGroupIds.length > 0) {
      return problemResponse(
        409,
        'Ziddiyat',
        'Davrda davomat yozuvlari bor — uni o\'chirib bo\'lmaydi. "Yopish" amalidan foydalaning.',
      );
    }
    state = state.filter((p) => p.id !== id);
    return new HttpResponse(null, { status: 204 });
  }),
];
