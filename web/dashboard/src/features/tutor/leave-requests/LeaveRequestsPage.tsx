import { useMemo, useState } from 'react';
import { QueryState } from '../components/QueryState';
import { mutationErrorMessage } from '../errors';
import { LeaveRequestsTable } from './components/LeaveRequestsTable';
import { useLeaveDecision, useLeaveRequestsQuery } from './hooks';

/** Tyutor · Ruxsat so'rovlari (SPEC-SCREENS §9.1) — container. */
export function LeaveRequestsPage() {
  const query = useLeaveRequestsQuery();
  const decision = useLeaveDecision();
  const [search, setSearch] = useState('');

  const rows = useMemo(() => {
    const q = search.trim().toLowerCase();
    const all = query.data ?? [];
    return q
      ? all.filter((r) => `${r.studentName} ${r.group} ${r.reason}`.toLowerCase().includes(q))
      : all;
  }, [query.data, search]);

  const pendingIds = useMemo(
    () => new Set(decision.isPending ? (decision.variables?.ids ?? []) : []),
    [decision.isPending, decision.variables],
  );
  const openIds = rows.filter((r) => r.status === 'pending').map((r) => r.id);

  return (
    <QueryState status={query.status} data={query.data} error={query.error} refetch={query.refetch}>
      {() => (
        <LeaveRequestsTable
          rows={rows}
          search={search}
          onSearch={setSearch}
          pendingIds={pendingIds}
          onDecide={(ids, d) => decision.mutate({ ids, decision: d })}
          onApproveAll={() => decision.mutate({ ids: openIds, decision: 'approve' })}
          canApproveAll={openIds.length > 0 && !decision.isPending}
          error={decision.isError ? mutationErrorMessage(decision.error) : undefined}
        />
      )}
    </QueryState>
  );
}

export default LeaveRequestsPage;
