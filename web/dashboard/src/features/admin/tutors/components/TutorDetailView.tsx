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
import { SCOPE_LEVEL_LABEL, type TutorDetail, type TutorGroup, type TutorScope } from '../types';
import styles from './TutorDetailView.module.css';

const SCOPE_COLUMNS: DataTableColumn<TutorScope>[] = [
  {
    key: 'level',
    header: 'Daraja',
    width: 'minmax(100px,.8fr)',
    render: (r) => (
      <Badge status={r.level === 'faculty' ? 'info' : 'neu'}>{SCOPE_LEVEL_LABEL[r.level]}</Badge>
    ),
  },
  {
    key: 'name',
    header: 'Nomi',
    width: 'minmax(220px,2.2fr)',
    wrap: true,
    render: (r) => (
      <span className={styles.scopeName}>
        <span className={styles.scopeTitle} data-level={r.level}>
          {r.name}
        </span>
        {r.path && <span className={styles.scopePath}>{r.path}</span>}
      </span>
    ),
  },
  { key: 'groups', header: 'Guruhlar', width: 'minmax(90px,.7fr)', mono: true },
  { key: 'students', header: 'Talabalar', width: 'minmax(90px,.7fr)', mono: true },
];

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
  onEditScopes: () => void;
}

/**
 * Tyutor detail: sarlavha kartasi (faktlar + amallar), biriktirilgan ko'lam jadvali va ostida
 * yig'iladigan "Qamrab olingan guruhlar" (ko'lamlardan yoyilgan samarali guruhlar). Presentation.
 */
export function TutorDetailView({
  tutor,
  notice,
  onEdit,
  onResetPassword,
  onToggleStatus,
  onEditScopes,
}: TutorDetailViewProps) {
  const students = tutor.groups.reduce((sum, g) => sum + g.students, 0);
  return (
    <div className={styles.page}>
      <Card aria-label="Tyutor ma'lumotlari">
        <CardHeader
          title={tutor.fullName}
          subtitle={
            <span className={styles.sub}>
              <span className={styles.mono}>{tutor.hemisId}</span> ·{' '}
              {tutor.faculties.map((f) => f.name).join(', ')}
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
              {
                k: 'Fakultetlar',
                v: (
                  <ul className={styles.facultyList} aria-label="Fakultetlar">
                    {tutor.faculties.map((f) => (
                      <li key={f.id} className={styles.faculty}>
                        <Badge status="info" size="sm">
                          {f.code}
                        </Badge>
                        <span>{f.name}</span>
                      </li>
                    ))}
                  </ul>
                ),
              },
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
        aria-label="Biriktirilgan ko'lam"
        columns={SCOPE_COLUMNS}
        rows={tutor.scopes}
        rowKey={(r) => r.id}
        minWidth="560px"
        toolbar={
          <>
            <div>
              <h2 className={styles.tableTitle}>Biriktirilgan ko'lam</h2>
              <p className={styles.tableSub}>
                {tutor.scopes.length} ko'lam · {tutor.groups.length} guruh · {students} talaba
              </p>
            </div>
            <Button size="xs" onClick={onEditScopes}>
              Guruhlarni biriktirish
            </Button>
          </>
        }
        emptyText={
          <EmptyState
            tone="plain"
            className={styles.empty}
            title="Ko'lam biriktirilmagan"
            description="«Guruhlarni biriktirish» orqali fakultet, kafedra, yo'nalish yoki guruh biriktiring."
          />
        }
      />

      <details className={styles.details}>
        <summary className={styles.summary}>
          Qamrab olingan guruhlar ({tutor.groups.length})
        </summary>
        <DataTable
          aria-label="Qamrab olingan guruhlar"
          columns={GROUP_COLUMNS}
          rows={tutor.groups}
          rowKey={(r) => r.assignmentId}
          minWidth="620px"
          density="compact"
          emptyText={
            <EmptyState
              tone="plain"
              className={styles.empty}
              title="Qamrab olingan guruhlar yo'q"
              description="Ko'lam biriktirilganda ichidagi faol guruhlar shu yerda ko'rinadi."
            />
          }
        />
      </details>
    </div>
  );
}
