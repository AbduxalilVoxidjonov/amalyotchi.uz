import { Badge, Button, type DataTableColumn } from '@/shared/ui';
import { AdminTable, type TableStateProps } from '../../components/AdminTable';
import { formatPhone, formatScope } from '../../shared/format';
import { TUTOR_STATUS_LABEL, type Tutor } from '../types';
import styles from './TutorsTable.module.css';

/** SPEC §9.5 ustunlari. Kutayotgan — kechikayotgan tyutorda qizil (`fg #9c3227`). Telefon/doira — v2 xom maydonlardan. */
const COLUMNS: DataTableColumn<Tutor>[] = [
  { key: 'fullName', header: 'FISH', width: 'minmax(160px,1.4fr)', strong: true },
  {
    key: 'phone',
    header: 'Telefon',
    width: 'minmax(160px,1.2fr)',
    mono: true,
    dim: true,
    render: (r) => formatPhone(r.phone),
  },
  {
    key: 'assigned',
    header: 'Biriktirilgan',
    width: 'minmax(180px,1.6fr)',
    render: (r) => formatScope(r.facultyCode, r.groups),
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
    render: (r) => (
      <Badge status={TUTOR_STATUS_LABEL[r.status].kind}>{TUTOR_STATUS_LABEL[r.status].label}</Badge>
    ),
  },
];

export interface TutorsTableProps extends TableStateProps<Tutor> {
  onCreate: () => void;
  onSendInvite: () => void;
}

export function TutorsTable({ onCreate, onSendInvite, ...state }: TutorsTableProps) {
  return (
    <AdminTable
      aria-label="Tyutorlar"
      columns={COLUMNS}
      rowKey={(r) => r.id}
      minWidth="880px"
      emptyTitle="Tyutorlar yo'q"
      actions={
        <>
          <Button size="xs" onClick={onCreate}>
            Tyutor qo'shish
          </Button>
          <Button size="xs" onClick={onSendInvite}>
            Kirish havolasini yuborish
          </Button>
        </>
      }
      {...state}
    />
  );
}
