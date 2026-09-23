import type { GroupRow } from '../faculties/groups/types';
import { calendarDays, formatShortRange, rangesOverlap } from './dates';

export interface GroupAvailability {
  /** Tanlangan oraliq bilan kesishadigan boshqa davrda — tanlab bo'lmaydi. */
  busy: boolean;
  note: string | null;
}

/** Guruhning joriy davri tanlangan oraliq bilan kesishadimi. */
export function groupAvailability(
  g: Pick<GroupRow, 'period' | 'isActive'>,
  startDate: string,
  endDate: string,
  currentPeriodId?: string,
): GroupAvailability {
  if (!g.isActive) return { busy: true, note: 'Guruh faol emas' };
  const p = g.period;
  if (!p || p.id === currentPeriodId || p.status === 'closed') return { busy: false, note: null };
  const label = `${p.name} (${formatShortRange(p.startDate, p.endDate)})`;
  const hasRange = calendarDays(startDate, endDate) !== null;
  if (hasRange && rangesOverlap(startDate, endDate, p.startDate, p.endDate)) {
    return { busy: true, note: `Band: ${label}` };
  }
  return { busy: false, note: `Boshqa davrda: ${label}` };
}
