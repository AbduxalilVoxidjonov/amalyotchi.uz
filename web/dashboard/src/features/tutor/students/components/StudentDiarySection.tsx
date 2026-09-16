import { Card, CardBody, CardHeader } from '@/shared/ui';
import { QueryState } from '../../components/QueryState';
import { useStudentDiariesQuery } from '../hooks';
import { StudentDiaryList } from './StudentDiaryList';
import styles from './StudentAttendanceSection.module.css';

/** Kundaliklar bo'limi (KONTRAKT §2.3) — faqat o'qish. */
export function StudentDiarySection({ studentId }: { studentId: string }) {
  const query = useStudentDiariesQuery(studentId);
  return (
    <Card as="section" aria-label="Kundaliklar">
      <CardHeader
        title="Kundaliklar"
        subtitle={query.data ? `${query.data.length} ta hisobot` : undefined}
      />
      <CardBody>
        <QueryState
          status={query.status}
          data={query.data}
          error={query.error}
          refetch={query.refetch}
          isEmpty={(entries) => entries.length === 0}
          empty={
            <p className={styles.empty} role="status">
              Kundalik hisobotlari hali yo'q.
            </p>
          }
        >
          {(entries) => <StudentDiaryList entries={entries} />}
        </QueryState>
      </CardBody>
    </Card>
  );
}
