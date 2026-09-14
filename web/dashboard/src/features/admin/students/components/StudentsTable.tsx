import { Badge, Button, ProgressBar, type DataTableColumn } from '@/shared/ui';
import { AdminTable, type TableStateProps } from '../../components/AdminTable';
import { STUDENT_STATUS_LABEL, type Student } from '../types';

/** SPEC §9.6 ustunlari. ❓ `bar:0` — dizaynda foiz bo'sh; matn o'rni (38px) saqlanadi — track'lar teng. */
const COLUMNS: DataTableColumn<Student>[] = [
  { key: 'fullName', header: 'Talaba', width: 'minmax(180px,1.6fr)', strong: true },
  { key: 'group', header: 'Guruh', width: 'minmax(90px,.7fr)', mono: true, dim: true },
  { key: 'faculty', header: 'Fakultet', width: 'minmax(170px,1.3fr)' },
  {
    key: 'company',
    header: 'Korxona',
    width: 'minmax(160px,1.4fr)',
    render: (r) => r.company ?? <span data-dim>—</span>,
  },
  {
    key: 'attendancePct',
    header: 'Davomat',
    width: 'minmax(140px,1fr)',
    render: (r) => (
      <ProgressBar
        value={r.attendancePct}
        showValue={r.attendancePct > 0 ? true : 'blank'}
        label={`${r.fullName} davomati`}
      />
    ),
  },
  {
    key: 'status',
    header: 'Holat',
    width: 'minmax(140px,.9fr)',
    render: (r) => (
      <Badge status={STUDENT_STATUS_LABEL[r.status].kind}>
        {STUDENT_STATUS_LABEL[r.status].label}
      </Badge>
    ),
  },
];

export interface StudentsTableProps extends TableStateProps<Student> {
  onImportExcel: () => void;
  onPullHemis: () => void;
  onExport: () => void;
}

export function StudentsTable({
  onImportExcel,
  onPullHemis,
  onExport,
  ...state
}: StudentsTableProps) {
  return (
    <AdminTable
      aria-label="Talabalar"
      columns={COLUMNS}
      rowKey={(r) => r.id}
      minWidth="900px"
      emptyTitle="Talabalar yo'q"
      actions={
        <>
          <Button size="xs" onClick={onImportExcel}>
            Excel import
          </Button>
          <Button size="xs" onClick={onPullHemis}>
            HEMIS dan tortish
          </Button>
          <Button size="xs" onClick={onExport}>
            Excel
          </Button>
        </>
      }
      {...state}
    />
  );
}
