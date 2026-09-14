import type { StatusKind } from '@/shared/ui';
import { formatCount, formatPct } from '../shared/format';
import type { DashboardStats, TutorActivity } from './types';

/** v2 xom raqamlar → SPEC §7 stat kartalar va tyutor holati ranglari (View'dan ajratilgan — testlanadi). */
type NoteTone = Extract<StatusKind, 'ok' | 'late' | 'bad'> | undefined;

interface StatCard {
  label: string;
  value: string;
  note: string;
  tone: NoteTone;
}

/** SPEC §7 4 ta stat karta — v2 xom raqamlardan (matn/rang shu yerda). */
export function buildStatCards(s: DashboardStats): StatCard[] {
  return [
    {
      label: 'Jami amaliyotchi',
      value: formatCount(s.studentsTotal),
      note: `${s.faculties} fakultet · ${s.groups} guruh`,
      tone: undefined,
    },
    {
      label: 'Faol',
      value: formatCount(s.studentsLinked),
      note:
        s.studentsUnlinked > 0
          ? `${formatCount(s.studentsUnlinked)} tasi hali ulanmagan`
          : 'hammasi ulangan',
      tone: s.studentsUnlinked > 0 ? 'late' : 'ok',
    },
    {
      label: 'Kutilayotgan ariza',
      value: formatCount(s.applicationsPending),
      note:
        s.applicationsOverdue > 0
          ? `${formatCount(s.applicationsOverdue)} tasi 3 kundan ortiq`
          : "kechikkani yo'q",
      tone: s.applicationsOverdue > 0 ? 'bad' : undefined,
    },
    {
      label: 'Bugungi davomat',
      value: formatPct(s.attendanceTodayPct),
      note: `kecha ${formatPct(s.attendanceYesterdayPct)}`,
      tone: s.attendanceTodayPct >= s.attendanceYesterdayPct ? 'ok' : 'late',
    },
  ];
}

/** Kutayotgan arizalar rangi: kechikayotgan tyutor — bad, bor — late, yo'q — ok. */
export function pendingKind(t: TutorActivity): 'ok' | 'late' | 'bad' {
  if (t.status === 'late') return 'bad';
  return t.pendingCount > 0 ? 'late' : 'ok';
}
