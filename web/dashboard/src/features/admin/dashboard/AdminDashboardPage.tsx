import { ErrorState, LoadingState } from '../components/PageStatus';
import { DashboardView } from './components/DashboardView';
import { useAdminDashboardQuery } from './hooks';

/** Admin · Umumiy dashboard (SPEC-SCREENS §7). Container: query → DashboardView. */
export function AdminDashboardPage() {
  const query = useAdminDashboardQuery();

  if (query.isPending) return <LoadingState />;
  if (query.isError) return <ErrorState error={query.error} onRetry={() => void query.refetch()} />;
  return <DashboardView data={query.data} />;
}

export default AdminDashboardPage;
