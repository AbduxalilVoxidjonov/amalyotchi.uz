import { Link, useParams } from 'react-router-dom';
import { usePageHeader } from '@/app/layout';
import { isApiError } from '@/shared/api';
import { Breadcrumb, Button, EmptyState, type BreadcrumbItem } from '@/shared/ui';
import { QueryState } from '../components/QueryState';
import { StudentAttendanceSection } from './components/StudentAttendanceSection';
import { StudentDetailView } from './components/StudentDetailView';
import { StudentDiarySection } from './components/StudentDiarySection';
import { useStudentQuery } from './hooks';
import styles from './StudentDetailPage.module.css';

const ROOT: BreadcrumbItem[] = [{ label: 'Talabalarim', to: '/tutor/students' }];

/** Tyutor · Talaba profili (`/tutor/students/:studentId`) — container (KONTRAKT §4.1). */
export function StudentDetailPage() {
  const { studentId = '' } = useParams<{ studentId: string }>();
  const query = useStudentQuery(studentId);
  usePageHeader({ title: query.data?.name });

  const is404 = isApiError(query.error) && query.error.status === 404;

  return (
    <div className={styles.page}>
      <Breadcrumb
        items={query.data ? [...ROOT, { label: query.data.name }] : [...ROOT, { label: '…' }]}
      />

      {is404 ? (
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
            <>
              <StudentDetailView detail={detail} />
              <StudentAttendanceSection detail={detail} />
              <StudentDiarySection studentId={detail.id} />
            </>
          )}
        </QueryState>
      )}
    </div>
  );
}

export default StudentDetailPage;
