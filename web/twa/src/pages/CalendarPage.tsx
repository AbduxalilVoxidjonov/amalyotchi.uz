import { useSearchParams } from 'react-router-dom';
import { ErrorState, LoadingState } from '@/shared/ui';
import { errorMessage } from '@/shared/api/client';
import { MonthGrid } from '@/features/calendar/components/MonthGrid';
import { useCalendarMonthQuery } from '@/features/calendar/hooks';

const MONTH_RE = /^\d{4}-\d{2}$/;

/**
 * SPEC-SCREENS §10 talaba varianti `kalendarim`. Oy — `?month=YYYY-MM` (SPEC-NAV 4.4);
 * berilmasa server joriy amaliyot oyini qaytaradi.
 */
export function CalendarPage() {
  const [params, setParams] = useSearchParams();
  const raw = params.get('month');
  const month = raw && MONTH_RE.test(raw) ? raw : null;
  const cal = useCalendarMonthQuery(month);

  if (cal.isPending) return <LoadingState height={360} />;
  if (cal.isError) {
    return <ErrorState description={errorMessage(cal.error)} onRetry={() => void cal.refetch()} />;
  }
  return (
    <MonthGrid
      data={cal.data}
      fetching={cal.isFetching}
      onMonthChange={(m) => setParams({ month: m })}
    />
  );
}

export default CalendarPage;
