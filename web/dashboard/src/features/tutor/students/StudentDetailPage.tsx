import { Link, useParams } from 'react-router-dom';
import { usePageHeader } from '@/app/layout';
import { isApiError } from '@/shared/api';
import { Breadcrumb, Button, EmptyState, type BreadcrumbItem } from '@/shared/ui';
import { QueryState } from '../components/QueryState';
import { StudentProfile } from './components/StudentProfile';
import { useStudentQuery } from './hooks';
import { usePeriodParam } from './periods';
import styles from './StudentDetailPage.module.css';

const ROOT: BreadcrumbItem[] = [{ label: 'Talabalarim', to: '/tutor/students' }];

/** Tyutor · Talaba profili (`/tutor/students/:studentId`) — container (KONTRAKT §4.1). */
export function StudentDetailPage() {
  const { studentId = '' } = useParams<{ studentId: string }>();
  const [periodId, setPeriodId] = usePeriodParam();
  const query = useStudentQuery(studentId, periodId);
  usePageHeader({ title: query.data?.name });

  const is404 = isApiError(query.error) && query.error.status === 404;
  // URL'dagi davr talabaga tegishli emas (backend 404 "Amaliyot davri topilmadi.") — sukutga qaytarish.
  const periodNotFound = is404 && periodId !== null;

  return (
    <div className={styles.page}>
      <Breadcrumb
        items={query.data ? [...ROOT, { label: query.data.name }] : [...ROOT, { label: '…' }]}
      />

      {periodNotFound ? (
        <EmptyState
          tone="plain"
          title="Amaliyot davri topilmadi."
          description="Havoladagi davr bu talabaga tegishli emas yoki o'chirilgan."
          action={<Button onClick={() => setPeriodId(null)}>Joriy davrni ko'rsatish</Button>}
        />
      ) : is404 ? (
        <EmptyState
          tone="plain"
          title="Talaba topilmadi."
          description="Bu talaba sizning ko'lamingizda emas yoki o'chirilgan."
          action={
            <Button asChild>
              <Link to="/tutor/students">Talabalarimga qaytish</Link>
            </Button>
          }
        />
      ) : (
        <QueryState
          status={query.status}
          data={query.data}
          error={query.error}
          refetch={query.refetch}
        >
          {(detail) => (
            <StudentProfile
              detail={detail}
              requestedPeriodId={periodId}
              onSelectPeriod={setPeriodId}
              isPlaceholderData={query.isPlaceholderData}
              area="tutor"
            />
          )}
        </QueryState>
      )}
    </div>
  );
}

export default StudentDetailPage;
