import { QueryState } from '../components/QueryState';
import { StudentsTable } from './components/StudentsTable';
import { useMyStudentsQuery } from './hooks';

/** Tyutor · Talabalarim (SPEC-SCREENS §5) — container. */
export function MyStudentsPage() {
  const query = useMyStudentsQuery();
  return (
    <QueryState status={query.status} data={query.data} error={query.error} refetch={query.refetch}>
      {(rows) => <StudentsTable rows={rows} />}
    </QueryState>
  );
}

export default MyStudentsPage;
