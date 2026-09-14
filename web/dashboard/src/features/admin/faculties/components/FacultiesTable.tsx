import { Badge, Button, ProgressBar, type DataTableColumn } from '@/shared/ui';
import { AdminTable, type TableStateProps } from '../../components/AdminTable';
import { FACULTY_STATUS_LABEL, type Faculty } from '../types';

/** SPEC §9.3 ustunlari (cols aynan). */
const COLUMNS: DataTableColumn<Faculty>[] = [
  { key: 'name', header: 'Fakultet', width: 'minmax(200px,2fr)', strong: true },
  { key: 'directions', header: "Yo'nalish", width: 'minmax(100px,.9fr)', mono: true },
  { key: 'groups', header: 'Guruh', width: 'minmax(80px,.8fr)', mono: true },
  { key: 'students', header: 'Talaba', width: 'minmax(90px,.9fr)', mono: true },
  {
    key: 'attendancePct',
    header: 'Davomat',
    width: 'minmax(150px,1.4fr)',
    render: (r) => <ProgressBar value={r.attendancePct} label={`${r.name} davomati`} />,
  },
  {
    key: 'status',
    header: 'Holat',
    width: 'minmax(120px,.9fr)',
    render: (r) => (
      <Badge status={FACULTY_STATUS_LABEL[r.status].kind}>
        {FACULTY_STATUS_LABEL[r.status].label}
      </Badge>
    ),
  },
];

export interface FacultiesTableProps extends TableStateProps<Faculty> {
  onCreate: () => void;
  onExport: () => void;
}

export function FacultiesTable({ onCreate, onExport, ...state }: FacultiesTableProps) {
  return (
    <AdminTable
      aria-label="Fakultetlar"
      columns={COLUMNS}
      rowKey={(r) => r.id}
      minWidth="760px"
      emptyTitle="Fakultetlar yo'q"
      actions={
        <>
          <Button size="xs" onClick={onCreate}>
            Yangi fakultet
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
