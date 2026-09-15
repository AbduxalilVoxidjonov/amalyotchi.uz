import {
  Badge,
  Button,
  Card,
  CardBody,
  CardHeader,
  DataTable,
  EmptyState,
  FactGrid,
  type DataTableColumn,
} from '@/shared/ui';
import { formatDateTime, formatPhone, formatRelative } from '../../shared/format';
import type { TutorDetail, TutorGroup } from '../types';
import styles from './TutorDetailView.module.css';

const GROUP_COLUMNS: DataTableColumn<TutorGroup>[] = [
  { key: 'groupName', header: 'Guruh', width: 'minmax(110px,1fr)', mono: true, strong: true },
  {
    key: 'course',
    header: 'Kurs',
    width: 'minmax(80px,.7fr)',
    mono: true,
    render: (r) => `${r.course}-kurs`,
  },
  { key: 'directionName', header: "Yo'nalish", width: 'minmax(180px,1.8fr)', wrap: true },
  { key: 'students', header: 'Talaba', width: 'minmax(80px,.7fr)', mono: true },
  {
    key: 'academicYearName',
    header: "O'quv yili",
    width: 'minmax(110px,.9fr)',
    mono: true,
    dim: true,
  },
];

export interface TutorDetailViewProps {
  tutor: TutorDetail;
  /** Muvaffaqiyatli amal xabari ("Parol yangilandi." kabi) — `role="status"`. */
  notice?: string | null;
  onEdit: () => void;
  onResetPassword: () => void;
  onToggleStatus: () => void;
  onEditGroups: () => void;
}

/** Tyutor detail: sarlavha kartasi (faktlar + amallar) va biriktirilgan guruhlar jadvali. Presentation. */
export function TutorDetailView({
  tutor,
  notice,
  onEdit,
  onResetPassword,
  onToggleStatus,
  onEditGroups,
}: TutorDetailViewProps) {
  const students = tutor.groups.reduce((sum, g) => sum + g.students, 0);
  return (
    <div className={styles.page}>
      <Card aria-label="Tyutor ma'lumotlari">
        <CardHeader
          title={tutor.fullName}
          subtitle={
            <span className={styles.sub}>
              <span className={styles.mono}>{tutor.hemisId}</span> · {tutor.facultyName}
            </span>
          }
          actions={
            <>
              <Button size="xs" onClick={onEdit}>
                Tahrirlash
              </Button>
              <Button size="xs" onClick={onResetPassword}>
                Parolni tiklash
              </Button>
              <Button
                size="xs"
                variant={tutor.isActive ? 'danger' : 'primary'}
                onClick={onToggleStatus}
              >
                {tutor.isActive ? 'Faol emas qilish' : 'Faollashtirish'}
              </Button>
            </>
          }
        />
        <CardBody className={styles.body}>
          <FactGrid
            variant="detail"
            min={170}
            items={[
              { k: 'HEMIS ID', v: <span className={styles.mono}>{tutor.hemisId}</span> },
              { k: 'Telefon', v: <span className={styles.mono}>{formatPhone(tutor.phone)}</span> },
              { k: 'Fakultet', v: `${tutor.facultyCode} · ${tutor.facultyName}` },
              {
                k: 'Holat',
                v: tutor.isActive ? (
                  <Badge status="ok">Faol</Badge>
                ) : (
                  <Badge status="neu">Faol emas</Badge>
                ),
              },
              { k: 'Oxirgi kirish', v: formatRelative(tutor.lastLoginAt) },
              { k: 'Yaratilgan', v: formatDateTime(tutor.createdAt) },
            ]}
          />
          {notice && (
            <p role="status" className={styles.notice}>
              {notice}
            </p>
          )}
        </CardBody>
      </Card>

      <DataTable
        aria-label="Biriktirilgan guruhlar"
        columns={GROUP_COLUMNS}
        rows={tutor.groups}
        rowKey={(r) => r.assignmentId}
        minWidth="620px"
        toolbar={
          <>
            <div>
              <h2 className={styles.tableTitle}>Biriktirilgan guruhlar</h2>
              <p className={styles.tableSub}>
                {tutor.groups.length} guruh · {students} talaba
              </p>
            </div>
            <Button size="xs" onClick={onEditGroups}>
              Guruhlarni tahrirlash
            </Button>
          </>
        }
        emptyText={
          <EmptyState
            tone="plain"
            className={styles.empty}
            title="Guruhlar biriktirilmagan"
            description="«Guruhlarni tahrirlash» orqali tyutor fakultetidagi guruhlarni biriktiring."
          />
        }
      />
    </div>
  );
}
