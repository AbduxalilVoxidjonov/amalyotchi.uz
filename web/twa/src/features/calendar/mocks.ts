import { http, HttpResponse, type HttpHandler } from 'msw';
import { STUDENT_ENDPOINTS } from '@/shared/api/endpoints';
import { problem, requireBearer } from '@/mocks/problem';
import type { CalendarDayDto, CalendarDayStatus, CalendarMonthDto } from './types';

/**
 * SPEC-SCREENS §10 `calRowsPersonal` (`kkkldkkkakks`) — 2026-10 ning real haftasiga moslangan:
 * 01.10.2026 — payshanba; yakshanbalar (4, 11) dam olish, dushanbalar (5, 12) ish kuni. 13.10 — "bugun" (pending).
 */
const OCT_2026: CalendarDayStatus[] = [
  'present',
  'present',
  'present',
  'dayOff',
  'late',
  'present',
  'present',
  'present',
  'absent',
  'present',
  'dayOff',
  'excused',
  'pending',
];

const pad2 = (n: number) => String(n).padStart(2, '0');

function buildMonth(month: string): CalendarMonthDto | null {
  const m = /^(\d{4})-(\d{2})$/.exec(month);
  if (!m) return null;
  const y = Number(m[1]);
  const mo = Number(m[2]);
  if (mo < 1 || mo > 12) return null;
  const count = new Date(Date.UTC(y, mo, 0)).getUTCDate();
  const days: CalendarDayDto[] = [];
  for (let d = 1; d <= count; d++) {
    const weekday = new Date(Date.UTC(y, mo - 1, d)).getUTCDay(); // 0 = yakshanba
    // Amaliyot 6 kunlik: faqat yakshanba dam olish; qolgan kunlar (kelajak) — future.
    let status: CalendarDayStatus = weekday === 0 ? 'dayOff' : 'future';
    if (month === '2026-10' && d <= OCT_2026.length) status = OCT_2026[d - 1]!;
    days.push({ date: `${y}-${pad2(mo)}-${pad2(d)}`, status });
  }
  return { month, studentName: 'Aliyev Akmal', groupName: '412-22', days };
}

export const calendarHandlers: HttpHandler[] = [
  http.get(STUDENT_ENDPOINTS.calendar, ({ request }) => {
    const denied = requireBearer(request);
    if (denied) return denied;
    const month = new URL(request.url).searchParams.get('month') ?? '2026-10';
    const data = buildMonth(month);
    if (!data) {
      return problem(400, "Ma'lumotlar noto'g'ri", "Kiritilgan ma'lumotlarda xatolik bor.", {
        errors: { Month: ["Oy YYYY-MM ko'rinishida bo'lishi kerak (masalan, 2026-10)."] },
      });
    }
    return HttpResponse.json(data);
  }),
];
