import { Badge, Button, Select, type DataTableColumn, type SelectOption } from '@/shared/ui';
import { AdminTable, type TableStateProps } from '../../components/AdminTable';
import { RowLink } from '../../components/RowLink';
import { DASH, formatPhone, formatScope } from '../../shared/format';
import { TUTOR_STATUS_LABEL, type Tutor } from '../types';
import styles from './TutorsTable.module.css';

/**
 * SPEC §9.5 ustunlari. Kutayotgan — kechikayotgan tyutorda qizil (`fg #9c3227`). Ism — detail sahifasiga
 * havola. Fakultet — kodlar `", "` bilan (to'liq nomlar `title`da), guruhlar alohida ustunda.
 */
const COLUMNS: DataTableColumn<Tutor>[] = [
  {
    key: 'fullName',
    header: 'FISH',
    width: 'minmax(160px,1.4fr)',
    strong: true,
    render: (r) => <RowLink to={`/admin/tutors/${r.id}`}>{r.fullName}</RowLink>,
  },
  {
    key: 'phone',
    header: 'Telefon',
    width: 'minmax(160px,1.2fr)',
    mono: true,
    dim: true,
    render: (r) => formatPhone(r.phone),
  },
  {
    key: 'faculties',
    header: 'Fakultet',
    width: 'minmax(90px,.9fr)',
    mono: true,
    render: (r) =>
      r.faculties.length === 0 ? (
        DASH
      ) : (
        <span title={r.faculties.map((f) => f.name).join(', ')}>
          {r.faculties.map((f) => f.code).join(', ')}
        </span>
      ),
  },
  {
    key: 'groups',
    header: 'Guruhlar',
    width: 'minmax(150px,1.4fr)',
    render: (r) => formatScope(null, r.groups),
  },
  { key: 'students', header: 'Talaba', width: 'minmax(80px,.9fr)', mono: true },
  {
    key: 'pending',
    header: 'Kutayotgan',
    width: 'minmax(110px,.9fr)',
    mono: true,
    render: (r) => (
      <span className={styles.pending} data-late={r.status === 'late' || undefined}>
        {r.pending}
      </span>
    ),
  },
  {
    key: 'status',
    header: 'Holat',
    width: 'minmax(140px,.9fr)',
    render: (r) =>
      r.isActive ? (
        <Badge status={TUTOR_STATUS_LABEL[r.status].kind}>
          {TUTOR_STATUS_LABEL[r.status].label}
        </Badge>
      ) : (
        <Badge status="neu">Faol emas</Badge>
      ),
  },
];

export interface TutorsTableProps extends TableStateProps<Tutor> {
  /** Fakultet filtri: `''` — barchasi. */
  facultyId: string;
  facultyOptions: readonly SelectOption[];
  onFacultyChange: (facultyId: string) => void;
  onCreate: () => void;
  onEdit: (tutor: Tutor) => void;
  onToggleStatus: (tutor: Tutor) => void;
}

export function TutorsTable({
  facultyId,
  facultyOptions,
  onFacultyChange,
  onCreate,
  onEdit,
  onToggleStatus,
  ...state
}: TutorsTableProps) {
  return (
    <AdminTable
      aria-label="Tyutorlar"
      columns={COLUMNS}
      rowKey={(r) => r.id}
      minWidth="880px"
      emptyTitle="Tyutorlar yo'q"
      rowDim={(r) => !r.isActive}
      filters={
        <Select
          aria-label="Fakultet bo'yicha filtr"
          variant="search"
          wrapperClassName={styles.filter}
          value={facultyId}
          onChange={(e) => onFacultyChange(e.target.value)}
        >
          <option value="">Barcha fakultetlar</option>
          {facultyOptions.map((o) => (
            <option key={o.value} value={o.value}>
              {o.label}
            </option>
          ))}
        </Select>
      }
      rowActions={(r) => (
        <>
          <Button size="xs" onClick={() => onEdit(r)}>
            Tahrirlash
          </Button>
          <Button size="xs" onClick={() => onToggleStatus(r)}>
            {r.isActive ? 'Faol emas qilish' : 'Faollashtirish'}
          </Button>
        </>
      )}
      actions={
        <Button size="xs" onClick={onCreate}>
          Yangi tyutor
        </Button>
      }
      {...state}
    />
  );
}
