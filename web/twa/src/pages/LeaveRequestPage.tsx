import { ErrorState, LoadingState } from '@/shared/ui';
import { errorMessage } from '@/shared/api/client';
import { LeaveRequestForm } from '@/features/leave/components/LeaveRequestForm';
import { LeaveRequestList } from '@/features/leave/components/LeaveRequestList';
import { useCreateLeaveRequest, useLeaveRequestsQuery } from '@/features/leave/hooks';
import styles from './pages.module.css';

/** SPEC-SCREENS §15 `isRuxsatForm` — Ruxsat so'rash. */
export function LeaveRequestPage() {
  const list = useLeaveRequestsQuery();
  const create = useCreateLeaveRequest();

  return (
    <div className={styles.stack}>
      <LeaveRequestForm
        pending={create.isPending}
        error={create.error}
        onSubmit={(input) => create.mutateAsync(input)}
      />
      {list.isPending && <LoadingState height={180} />}
      {list.isError && (
        <ErrorState description={errorMessage(list.error)} onRetry={() => void list.refetch()} />
      )}
      {list.data && <LeaveRequestList items={list.data} />}
    </div>
  );
}

export default LeaveRequestPage;
