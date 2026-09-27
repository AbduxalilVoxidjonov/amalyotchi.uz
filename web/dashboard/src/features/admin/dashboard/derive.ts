import type { StatusKind } from '@/shared/ui';
import { DASH, formatCount, formatPct } from '../shared/format';
import type { DashboardStats, FacultyAttendance, TutorActivity } from './types';

/** v2 xom raqamlar → SPEC §7 stat kartalar va tyutor holati ranglari (View'dan ajratilgan — testlanadi). */
type NoteTone = Extract<StatusKind, 'ok' | 'late' | 'bad'> | undefined;

interface StatCard {
  label: string;
  value: string;
  note: string;
  tone: NoteTone;
}

/** Backend `AdminThresholds.PendingApplicationLateAfter` (48 soat) — kechikkan ariza chegarasi. */
export const OVERDUE_AFTER_HOURS = 48;

export const NO_ONGOING_PRACTICE = "Bugun davom etayotgan amaliyot yo'q";
export const NOT_A_WORK_DAY = 'Bugun ish kuni emas';

/**
 * Bugungi davomat nega ko'rsatilmaydi (nollar o'rniga tushunarli holat) yoki `null` — ko'rsatiladi.
 * Backend: `expectedToday` faqat bugun davom etayotgan (yopilmagan) davr ish kuni bo'lgan talabalar.
 */
export function attendanceEmptyReason(s: DashboardStats): string | null {
  if (s.ongoingPeriods === 0) return NO_ONGOING_PRACTICE;
  if (s.expectedToday === 0) return NOT_A_WORK_DAY;
  return null;
}

function yesterdayNote(s: DashboardStats): string {
  return s.expectedYesterday > 0
    ? `kecha ${formatPct(s.attendanceYesterdayPct)}`
    : 'kecha davomat kutilmagan';
}

/** SPEC §7 4 ta stat karta — v2 xom raqamlardan (matn/rang shu yerda). */
export function buildStatCards(s: DashboardStats): StatCard[] {
  const emptyReason = attendanceEmptyReason(s);
  return [
    {
      label: 'Jami amaliyotchi',
      value: formatCount(s.studentsTotal),
      note: `${s.faculties} fakultet · ${s.groups} guruh`,
      tone: undefined,
    },
    {
      // `studentsLinked` — Telegram hisobi bog'langan talabalar (faollik/aktiv korxona emas).
      label: 'Telegram ulangan',
      value: formatCount(s.studentsLinked),
      note:
        s.studentsUnlinked > 0
          ? `${formatCount(s.studentsUnlinked)} tasi hali ulanmagan`
          : 'hammasi ulangan',
      tone: s.studentsUnlinked > 0 ? 'late' : 'ok',
    },
    {
      // Faqat ochiq (yopilmagan, tugamagan) davrlardagi arizalar.
      label: 'Kutilayotgan ariza',
      value: formatCount(s.applicationsPending),
      note:
        s.applicationsOverdue > 0
          ? `${formatCount(s.applicationsOverdue)} tasi ${OVERDUE_AFTER_HOURS} soatdan ortiq`
          : "kechikkani yo'q",
      tone: s.applicationsOverdue > 0 ? 'bad' : undefined,
    },
    emptyReason
      ? { label: 'Bugungi davomat', value: DASH, note: emptyReason, tone: undefined }
      : {
          label: 'Bugungi davomat',
          value: formatPct(s.attendanceTodayPct),
          note: yesterdayNote(s),
          tone:
            s.expectedYesterday === 0
              ? undefined
              : s.attendanceTodayPct >= s.attendanceYesterdayPct
                ? 'ok'
                : 'late',
        },
  ];
}

/** Fakultet foizi matni: bugun kutilgan talaba bo'lmasa "—" (0% emas). */
export function facultyPctText(f: FacultyAttendance): string {
  return f.expectedToday > 0 ? formatPct(f.attendancePct) : DASH;
}

/** Kutayotgan arizalar rangi: kechikayotgan tyutor — bad, bor — late, yo'q — ok. */
export function pendingKind(t: TutorActivity): 'ok' | 'late' | 'bad' {
  if (t.status === 'late') return 'bad';
  return t.pendingCount > 0 ? 'late' : 'ok';
}
