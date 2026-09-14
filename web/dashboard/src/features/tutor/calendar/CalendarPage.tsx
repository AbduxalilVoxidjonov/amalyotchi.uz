import { useSearchParams } from 'react-router-dom';
import { QueryState } from '../components/QueryState';
import {
  MONTHS_UZ,
  monthLabel,
  parseMonth,
  shiftMonth,
  toMonthParam,
  todayInTashkent,
} from '../format';
import { CalendarGrid } from './components/CalendarGrid';
import { useCalendarQuery } from './hooks';

/** `?month=` bo'lmasa — joriy oy (Toshkent; backend ham shunday). */
function currentMonth(): { year: number; month: number } {
  const [y, m] = todayInTashkent().split('-').map(Number);
  return { year: y!, month: m! };
}

/** Tyutor · Kalendar (SPEC-SCREENS §10) — container. URL: `?month=YYYY-MM`. */
export function CalendarPage() {
  const [params, setParams] = useSearchParams();
  const current = parseMonth(params.get('month')) ?? currentMonth();
  const month = toMonthParam(current.year, current.month);
  const query = useCalendarQuery({ month });

  const go = (delta: number) => {
    const next = shiftMonth(current.year, current.month, delta);
    const sp = new URLSearchParams(params);
    sp.set('month', toMonthParam(next.year, next.month));
    setParams(sp);
  };
  const prev = shiftMonth(current.year, current.month, -1);
  const next = shiftMonth(current.year, current.month, 1);

  return (
    <QueryState status={query.status} data={query.data} error={query.error} refetch={query.refetch}>
      {(data) => {
        const first = data.days[0] ?? 1;
        const last = data.days[data.days.length - 1] ?? first;
        const pad = (n: number) => String(n).padStart(2, '0');
        return (
          <CalendarGrid
            title={`${monthLabel(current.year, current.month)} · ${pad(first)}–${pad(last)} kunlar`}
            prevLabel={MONTHS_UZ[prev.month - 1]!}
            nextLabel={MONTHS_UZ[next.month - 1]!}
            onPrev={() => go(-1)}
            onNext={() => go(1)}
            days={data.days}
            rows={data.rows}
          />
        );
      }}
    </QueryState>
  );
}

export default CalendarPage;
