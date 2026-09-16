import type { BreadcrumbItem } from '@/shared/ui';
import { useParams } from 'react-router-dom';
import { StudentAttendanceSection } from '@/features/tutor/students/components/StudentAttendanceSection';
import { StudentDetailView } from '@/features/tutor/students/components/StudentDetailView';
import { LoadingState } from '../components/PageStatus';
import { HierarchyListPage } from '../faculties/components/HierarchyListPage';
import { AdminStudentMetaCard } from './components/AdminStudentMetaCard';
import { useStudentQuery } from './hooks';
import styles from './StudentDetailPage.module.css';

const ROOT: BreadcrumbItem[] = [{ label: 'Talabalar', to: '/admin/students' }];

/**
 * Admin · Talaba profili (`/admin/students/:studentId`): tyutor profilidagi bloklar
 * (sarlavha kartasi, korxona, ariza, davr, kundalik jadval) + tashkiliy
 * ma'lumot (tyutor, kafedra, Telegram). Davomat/kundalik `area="admin"` bilan
 * `/api/admin/students/:id/...` dan so'raladi.
 */
export function StudentDetailPage() {
  const { studentId = '' } = useParams<{ studentId: string }>();
  const query = useStudentQuery(studentId);
  const detail = query.data;

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
          <StudentDetailView detail={detail} />
          <AdminStudentMetaCard detail={detail} />
          <StudentAttendanceSection detail={detail} area="admin" />
        </div>
      ) : (
        <LoadingState />
      )}
    </HierarchyListPage>
  );
}

export default StudentDetailPage;
