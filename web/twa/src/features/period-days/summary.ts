import type { PeriodDay, PeriodSummary } from './types';

/**
 * Davr talaba uchun "tugagan"mi — bosh ekranda kunlar ro'yxati o'rniga yig'indi ko'rsatiladi.
 * Qoida: `status === 'closed'` (sanasi hali tugamagan bo'lsa ham — admin erta yopgan) YOKI
 * `endDate < today` (yopilmagan, lekin sanasi o'tgan). `today` — server sanasi (DateOnly, Toshkent);
 * ikkala sana ham "YYYY-MM-DD" bo'lgani uchun satr solishtiruvi sana tartibi bilan bir xil.
 */
export function isPeriodEnded(
  period: Pick<PeriodSummary, 'status' | 'endDate'>,
  today: string,
): boolean {
  return period.status === 'closed' || period.endDate < today;
}

export interface PeriodResultSummary {
  /** Vaqtida kelgan kunlar (`present`). */
  present: number;
  /** Kech kelgan kunlar (`late`). */
  late: number;
  /** Kelgan kunlar jami = present + late. */
  attended: number;
  absent: number;
  excused: number;
  /**
   * Davomat foizi (0–100, yaxlitlanmagan) — backend `AttendanceCalendar.ComputeAttendance` /
   * `PracticeCalendar.AttendancePct` bilan bir xil: kelgan / (hisobga olingan kunlar − sababli)
   * = (present + late) / (present + late + absent). Maxraj 0 → `null` (hali hisoblanadigan kun yo'q).
   */
  attendancePct: number | null;
}

/**
 * Davr kunlaridan yig'indi. `future` / `pending` / `dayOff` hisobga kirmaydi — faqat hal bo'lgan
 * ish kunlari (present, late, absent, excused) sanaladi.
 */
export function summarizePeriodDays(days: readonly PeriodDay[]): PeriodResultSummary {
  let present = 0;
  let late = 0;
  let absent = 0;
  let excused = 0;
  for (const d of days) {
    if (d.status === 'present') present++;
    else if (d.status === 'late') late++;
    else if (d.status === 'absent') absent++;
    else if (d.status === 'excused') excused++;
  }
  const attended = present + late;
  const denominator = attended + absent;
  return {
    present,
    late,
    attended,
    absent,
    excused,
    attendancePct: denominator > 0 ? (attended * 100) / denominator : null,
  };
}
