import { http, HttpResponse, type HttpHandler } from 'msw';
import { problem } from '@/mocks/data';
import { parseMonth, todayInTashkent } from '../format';
import { MOCK_TODAY_DATE } from '../today/mocks';
import type { CalendarDayStatus, CalendarResponse, CalendarRow } from './types';

/** SPEC-SCREENS §10 mock — dizayndagi 12 kunlik naqsh; oy kunlari 1..N ga davriy yoyiladi. */
const SPEC_ROWS: { studentId: string; name: string; days: string }[] = [
  { studentId: 's-341030', name: 'Aliyev Akmal', days: 'kkkldkkkakksd' },
  { studentId: 's-341031', name: 'Karimov Bekzod', days: 'klkldkkllkksd' },
  { studentId: 's-341032', name: 'Sobirov Diyor', days: 'kaaldakaaakad' },
  { studentId: 's-341033', name: 'Yusupova Nilufar', days: 'kkkkdkkkkkksd' },
  { studentId: 's-341034', name: 'Rahimov Sardor', days: 'kklsdkkalkkkd' },
  { studentId: 's-341035', name: 'Toshpulatova Zarina', days: 'kkksdkkkkskkd' },
];

const CODE_STATUS: Record<string, CalendarDayStatus> = {
  k: 'present',
  l: 'late',
  a: 'absent',
  s: 'excused',
  d: 'dayOff',
};

function toStatuses(pattern: string, daysInMonth: number, todayDay: number | null): CalendarDayStatus[] {
  const out: CalendarDayStatus[] = [];
  for (let i = 0; i < daysInMonth; i++) {
    const day = i + 1;
    if (todayDay !== null && day > todayDay) out.push('future');
    else if (todayDay !== null && day === todayDay) out.push('pending');
    else out.push(CODE_STATUS[pattern[i % pattern.length]!] ?? 'dayOff');
  }
  return out;
}

export function buildCalendarResponse(month: string): CalendarResponse {
  // Mock "bugun" — today/mocks bilan bir xil (2026-10-12): oktabr 12 — kutilmoqda, 13+ — kelgusi.
  const today = MOCK_TODAY_DATE;
  const parsed = parseMonth(month) ?? parseMonth(today.slice(0, 7))!;
  const daysInMonth = new Date(Date.UTC(parsed.year, parsed.month, 0)).getUTCDate();
  const monthKey = `${parsed.year}-${String(parsed.month).padStart(2, '0')}`;
  // O'tgan oy — to'liq; joriy oy — bugungacha; kelgusi oy — hammasi `future`.
  const todayDay =
    monthKey === today.slice(0, 7)
      ? Number(today.slice(8, 10))
      : monthKey > today.slice(0, 7)
        ? 0
        : null;
  const rows: CalendarRow[] = SPEC_ROWS.map((r) => ({
    studentId: r.studentId,
    name: r.name,
    days: toStatuses(r.days, daysInMonth, todayDay),
  }));
  return { month: monthKey, days: Array.from({ length: daysInMonth }, (_, i) => i + 1), rows };
}

export const calendarHandlers: HttpHandler[] = [
  http.get('/api/tutor/calendar', ({ request }) => {
    const month = new URL(request.url).searchParams.get('month');
    if (month !== null && !parseMonth(month))
      return HttpResponse.json(
        problem(400, "Ma'lumotlar noto'g'ri", 'month: YYYY-MM formatida bo‘lishi kerak.', {
          errors: { Month: ['Oy YYYY-MM formatida bo‘lishi kerak.'] },
        }),
        { status: 400 },
      );
    return HttpResponse.json(buildCalendarResponse(month ?? todayInTashkent().slice(0, 7)));
  }),
];
