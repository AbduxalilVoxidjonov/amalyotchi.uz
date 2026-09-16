import { useNavigate } from 'react-router-dom';
import { EmptyState } from '@/shared/ui';
import { QueryState } from '../components/QueryState';
import { CompaniesTable } from './components/CompaniesTable';
import { useTutorCompaniesQuery } from './hooks';

/** Tyutor · Korxonalar (`/tutor/companies`) — container. Qator bosilsa korxona detaliga o'tadi. */
export function CompaniesPage() {
  const query = useTutorCompaniesQuery();
  const navigate = useNavigate();
  return (
    <QueryState
      status={query.status}
      data={query.data}
      error={query.error}
      refetch={query.refetch}
      isEmpty={(rows) => rows.length === 0}
      empty={
        <EmptyState
          title="Korxonalar yo'q"
          description="Ko'lamingizdagi talabalar hali birorta korxonaga biriktirilmagan."
        />
      }
    >
      {(rows) => (
        <CompaniesTable rows={rows} onOpen={(c) => void navigate(`/tutor/companies/${c.id}`)} />
      )}
    </QueryState>
  );
}

export default CompaniesPage;
