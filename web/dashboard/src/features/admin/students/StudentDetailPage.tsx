import { Breadcrumb, Button, EmptyState, type BreadcrumbItem } from '@/shared/ui';
import { useParams } from 'react-router-dom';
import { StudentPasswordAction } from '@/features/shared/student-password';
import { isApiError } from '@/shared/api';
import { StudentProfile } from '@/features/tutor/students/components/StudentProfile';
import { usePeriodParam } from '@/features/tutor/students/periods';
import { LoadingState } from '../components/PageStatus';
import { HierarchyListPage } from '../faculties/components/HierarchyListPage';
import { AdminStudentMetaCard } from './components/AdminStudentMetaCard';
import { useStudentQuery } from './hooks';
import styles from './StudentDetailPage.module.css';

const ROOT: BreadcrumbItem[] = [{ label: 'Talabalar', to: '/admin/students' }];

/**
 * Admin · Talaba profili (`/admin/students/:studentId`): tyutor profilidagi bloklar
 * (davr tanlagichi, sarlavha kartasi, korxona, ariza, davr, kundalik jadval) + tashkiliy
 * ma'lumot (tyutor, kafedra, Telegram). Davomat/kundalik `area="admin"` bilan
 * `/api/admin/students/:id/...` dan so'raladi.
 */
export function StudentDetailPage() {
  const { studentId = '' } = useParams<{ studentId: string }>();
  const [periodId, setPeriodId] = usePeriodParam();
  const query = useStudentQuery(studentId, periodId);
  const detail = query.data;

  // URL'dagi davr talabaga tegishli emas (backend 404 "Amaliyot davri topilmadi.").
  if (periodId !== null && isApiError(query.error) && query.error.status === 404) {
    return (
      <div className={styles.stack}>
        <Breadcrumb items={ROOT} />
        <EmptyState
          tone="plain"
          title="Amaliyot davri topilmadi."
          description="Havoladagi davr bu talabaga tegishli emas yoki o'chirilgan."
          action={<Button onClick={() => setPeriodId(null)}>Joriy davrni ko'rsatish</Button>}
        />
      </div>
    );
  }

  return (
    <HierarchyListPage
      detailQuery={query}
      rootBreadcrumb={ROOT}
      buildBreadcrumb={(s) => [...ROOT, { label: s.name }]}
      loadingBreadcrumb={[...ROOT, { label: '…' }]}
      notFoundTitle="Talaba topilmadi."
      backTo="/admin/students"
      backLabel="Talabalarga qaytish"
      pageTitle={(s) => s.name}
    >
      {detail ? (
        <div className={styles.stack}>
          <StudentProfile
            detail={detail}
            requestedPeriodId={periodId}
            onSelectPeriod={setPeriodId}
            isPlaceholderData={query.isPlaceholderData}
            area="admin"
            headerActions={<StudentPasswordAction area="admin" student={detail} />}
          >
            <AdminStudentMetaCard detail={detail} />
          </StudentProfile>
        </div>
      ) : (
        <LoadingState />
      )}
    </HierarchyListPage>
  );
}

export default StudentDetailPage;
