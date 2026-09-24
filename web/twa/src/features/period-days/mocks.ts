import { http, HttpResponse, type HttpHandler } from 'msw';
import { STUDENT_ENDPOINTS } from '@/shared/api/endpoints';
import { problem, requireBearer } from '@/mocks/problem';
import { MOCK_AUTUMN_PERIOD, MOCK_SPRING_PERIOD } from '@/features/period/mocks';
import type { StudentPeriodOption } from '@/features/period/types';
import { mockToday } from '@/features/today/mocks';
import type { AttendanceStatus } from '@/features/today/types';
import { formatTime } from '@/shared/lib/format';
import type { PeriodDay, PeriodDayDiary, PeriodDayStatus, StudentPeriodDays } from './types';

/**
 * GET /api/student/period-days mock'i. Bosh ekran uchun kuzgi davr 31.08–14.10.2026 (kalendar mock'i kabi
 * 6 kunlik hafta — faqat yakshanba dam olish), 01.09 — "Mustaqillik kuni" bayrami.
 * "Bugun" — `mockToday.date` (sukut 12.10.2026); bugungi qator holati `mockToday.checkin` dan olinadi,
 * shuning uchun check-in'dan keyingi invalidate yangi holatni qaytaradi.
 */
export const MOCK_PD_AUTUMN_PERIOD: StudentPeriodOption = {
  ...MOCK_AUTUMN_PERIOD,
  startDate: '2026-08-31',
  endDate: '2026-10-14',
};

const HOLIDAYS: Record<string, string> = { '2026-09-01': 'Mustaqillik kuni' };

/** `two` — kuzgi (sukut) + bahorgi (rejalashtirilgan) · `single` — faqat kuzgi · `none` — davr yo'q. */
export type PeriodDaysVariant = 'two' | 'single' | 'none';

let variant: PeriodDaysVariant = 'two';

export function setPeriodDaysVariant(next: PeriodDaysVariant) {
  variant = next;
}

export function resetPeriodDaysMocks() {
  variant = 'two';
}

function mockPeriods(): StudentPeriodOption[] {
  if (variant === 'none') return [];
  // Tanaffusda (today.period = bahorgi) sukut — bahorgi davr (backend `isDefault` bilan bir xil).
  const springDefault = mockToday.period?.id === MOCK_SPRING_PERIOD.id;
  const autumn = { ...MOCK_PD_AUTUMN_PERIOD, isDefault: !springDefault };
  if (variant === 'single') return [autumn];
  // startDate kamayish tartibida.
  return [{ ...MOCK_SPRING_PERIOD, isDefault: springDefault }, autumn];
}

const pad2 = (n: number) => String(n).padStart(2, '0');

function* dateRange(from: string, to: string): Generator<{ date: string; weekday: number }> {
  const [fy, fm, fd] = from.split('-').map(Number) as [number, number, number];
  const cur = new Date(Date.UTC(fy, fm - 1, fd));
  for (;;) {
    const date = `${cur.getUTCFullYear()}-${pad2(cur.getUTCMonth() + 1)}-${pad2(cur.getUTCDate())}`;
    if (date > to) return;
    yield { date, weekday: cur.getUTCDay() === 0 ? 7 : cur.getUTCDay() };
    cur.setUTCDate(cur.getUTCDate() + 1);
  }
}

const EMPTY_DAY = {
  checkInAt: null,
  checkOutAt: null,
  autoClosed: false,
  suspicious: false,
  manual: false,
  diary: null,
} as const;

/** O'tgan ish kuni — `n`-chi (0 dan) ish kuni uchun deterministik, turli-tuman holatlar. */
function pastDay(date: string, weekday: number, n: number, recent: boolean): PeriodDay {
  let status: PeriodDayStatus = 'present';
  if (n === 7) status = 'absent';
  else if (n === 12) status = 'excused';
  else if (n % 11 === 4) status = 'late';

  const attended = status === 'present' || status === 'late';
  const autoClosed = attended && n === 3;
  let diary: PeriodDayDiary | null = null;
  if (attended && n % 6 !== 5) {
    const id = `dddddddd-0000-4000-8000-${String(n).padStart(12, '0')}`;
    if (recent) diary = { id, status: 'submitted', score: null };
    else if (n === 10) diary = { id, status: 'rewrite', score: null };
    else if (n % 3 === 0) diary = { id, status: 'seen', score: null };
    else diary = { id, status: 'approved', score: n % 2 === 0 ? 5 : 4 };
  }
  return {
    date,
    weekday,
    isWorkDay: true,
    holiday: null,
    status,
    checkInAt: attended ? (status === 'late' ? '09:24' : `08:${pad2(48 + (n % 10))}`) : null,
    // Avto-yopilganda backend yopilish vaqtini qaytaradi.
    checkOutAt: attended ? (autoClosed ? '18:00' : `17:0${n % 6}`) : null,
    autoClosed,
    suspicious: attended && n === 9,
    manual: n === 15 || status === 'excused',
    diary,
  };
}

const TODAY_STATUS: Record<AttendanceStatus, PeriodDayStatus> = {
  pending: 'pending',
  present: 'present',
  late: 'late',
  absent: 'absent',
  excused: 'excused',
  dayOff: 'dayOff',
};

function todayDay(date: string, weekday: number): PeriodDay {
  const { checkin, diary } = mockToday;
  return {
    date,
    weekday,
    isWorkDay: true,
    holiday: null,
    status: TODAY_STATUS[checkin.status],
    checkInAt: checkin.checkInAt ? formatTime(checkin.checkInAt) : null,
    checkOutAt: checkin.checkOutAt ? formatTime(checkin.checkOutAt) : null,
    autoClosed: checkin.autoClosed,
    suspicious: checkin.suspicious,
    manual: false,
    diary: diary.submittedToday
      ? { id: 'dddddddd-0000-4000-8000-ffffffffffff', status: 'submitted', score: null }
      : null,
  };
}

export function buildPeriodDays(period: StudentPeriodOption, today: string): PeriodDay[] {
  const all = [...dateRange(period.startDate, period.endDate)];
  const pastWork = all.filter((d) => d.weekday !== 7 && !HOLIDAYS[d.date] && d.date < today);
  let n = 0;
  return all.map(({ date, weekday }) => {
    const holiday = HOLIDAYS[date] ?? null;
    if (weekday === 7 || holiday) {
      return { date, weekday, isWorkDay: false, holiday, status: 'dayOff', ...EMPTY_DAY };
    }
    if (date === today) return todayDay(date, weekday);
    if (date > today)
      return { date, weekday, isWorkDay: true, holiday, status: 'future', ...EMPTY_DAY };
    const index = n++;
    return pastDay(date, weekday, index, index >= pastWork.length - 2);
  });
}

export function mockPeriodDays(periodId: string | null): StudentPeriodDays | null {
  const today = mockToday.date;
  const periods = mockPeriods();
  const option = periodId
    ? periods.find((p) => p.id === periodId)
    : (periods.find((p) => p.isDefault) ?? periods[0]);
  if (periodId && !option) return null;
  if (!option) return { today, period: null, periods, days: [] };
  const days = buildPeriodDays(option, today);
  const work = days.filter((d) => d.isWorkDay);
  return {
    today,
    period: {
      id: option.id,
      name: option.name,
      status: option.status,
      startDate: option.startDate,
      endDate: option.endDate,
      requiredDays: work.length,
      // Backend (§6.17): tyutor statistikasi maxraji — o'tgan ish kunlari, sababli kunlarsiz;
      // bugun — belgilangan yoki oyna yopilgan bo'lsa (holat `pending` emas).
      elapsedWorkDays: work.filter(
        (d) =>
          d.status !== 'excused' &&
          (d.date < today || (d.date === today && d.status !== 'pending')),
      ).length,
    },
    periods,
    days,
  };
}

export const periodDaysHandlers: HttpHandler[] = [
  http.get(STUDENT_ENDPOINTS.periodDays, ({ request }) => {
    const denied = requireBearer(request);
    if (denied) return denied;
    const periodId = new URL(request.url).searchParams.get('periodId');
    const data = mockPeriodDays(periodId);
    if (!data) return problem(404, 'Topilmadi', 'Amaliyot davri topilmadi.');
    return HttpResponse.json(data);
  }),
];
