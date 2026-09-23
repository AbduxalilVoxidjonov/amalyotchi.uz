import { http, HttpResponse, type HttpHandler } from 'msw';
import { mockDepartments } from '../faculties/departments/mocks';
import { mockDirections } from '../faculties/directions/mocks';
import { mockGroups } from '../faculties/groups/mocks';
import { mockFaculties } from '../faculties/mocks';
import { problemResponse } from '../shared/mockProblem';
import { PRACTICE_PERIODS_ENDPOINT } from './api';
import { isIsoDate, parseWorkDays, rangesOverlap, todayIso } from './dates';
import type {
  PracticePeriodCreate,
  PracticePeriodDetail,
  PracticePeriodGroup,
  PracticePeriodGroupsUpdate,
  PracticePeriodListItem,
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
interface PeriodSeed {
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

/** Global sozlamalar (yaratilishda nusxa olinadi) — settings mock'idagi demo qiymatlar. */
const GLOBAL = {
  dailyStart: '09:00',
  dailyEnd: '17:00',
  workDays: '1,2,3,4,5,6',
  dailyReportRequired: true,
};

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
  const days = parseWorkDays(csv);
  let n = 0;
  for (
    let t = Date.parse(`${start}T00:00:00Z`);
    t <= Date.parse(`${end}T00:00:00Z`);
    t += 86_400_000
  ) {
    const iso = ((new Date(t).getUTCDay() + 6) % 7) + 1;
    if (days.has(iso)) n++;
  }
  return n;
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
    ...GLOBAL,
    requiredDays: workDaysBetween(p.startDate, p.endDate, GLOBAL.workDays),
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

const notFound = () => problemResponse(404, 'Topilmadi', 'Amaliyot davri topilmadi.');
const closedConflict = () =>
  problemResponse(409, 'Ziddiyat', "Yopilgan davrni o'zgartirib bo'lmaydi.");

export const practicePeriodsHandlers: HttpHandler[] = [
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
