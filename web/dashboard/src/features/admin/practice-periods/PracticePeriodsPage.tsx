import { useSearchParams } from 'react-router-dom';
import { PracticePeriodsTable } from './components/PracticePeriodsTable';
import { usePracticePeriodsQuery } from './hooks';
import { isPeriodStatus, type PracticePeriodStatus } from './types';

/**
 * Admin · Amaliyot davrlari (`/admin/practice-periods`). Status filtri URL'da (`?status=`) —
 * havola ulashilsa/yangilansa ham saqlanadi.
 */
export function PracticePeriodsPage() {
  const [searchParams, setSearchParams] = useSearchParams();
  const raw = searchParams.get('status');
  const status: PracticePeriodStatus | null = isPeriodStatus(raw) ? raw : null;
  const query = usePracticePeriodsQuery(status);

  return (
    <PracticePeriodsTable
      rows={query.data}
      isLoading={query.isPending}
      error={query.error}
      onRetry={() => void query.refetch()}
      status={status}
      onStatusChange={(next) =>
        setSearchParams(
          (prev) => {
            const p = new URLSearchParams(prev);
            if (next) p.set('status', next);
            else p.delete('status');
            return p;
          },
          { replace: true },
        )
      }
    />
  );
}

export default PracticePeriodsPage;
