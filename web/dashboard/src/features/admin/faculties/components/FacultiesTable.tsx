import { Badge, Button, ProgressBar, type DataTableColumn } from '@/shared/ui';
import { AdminTable, type TableStateProps } from '../../components/AdminTable';
import { RowLink } from '../../components/RowLink';
import { FACULTY_STATUS_LABEL, type Faculty } from '../types';

/** Fakultet ichki sahifasi (kafedralar ro'yxati) — nom havolasi va butun qator uchun. */
const facultyHref = (r: Faculty) => `/admin/faculties/${r.id}`;

/** SPEC §9.3 ustunlari (cols aynan) + `isActive` holat badge'i. Nom — kafedralarga kiradigan havola. */
const COLUMNS: DataTableColumn<Faculty>[] = [
  {
    key: 'name',
    header: 'Fakultet',
    width: 'minmax(200px,2fr)',
    strong: true,
    render: (r) => <RowLink to={facultyHref(r)}>{r.name}</RowLink>,
  },
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
    render: (r) =>
      r.isActive ? (
        <Badge status={FACULTY_STATUS_LABEL[r.status].kind}>
          {FACULTY_STATUS_LABEL[r.status].label}
        </Badge>
      ) : (
        <Badge status="neu">Faol emas</Badge>
      ),
  },
];

export interface FacultiesTableProps extends TableStateProps<Faculty> {
  onCreate: () => void;
  onExport: () => void;
  onEdit: (faculty: Faculty) => void;
  onToggleStatus: (faculty: Faculty) => void;
  onDelete: (faculty: Faculty) => void;
}

export function FacultiesTable({
  onCreate,
  onExport,
  onEdit,
  onToggleStatus,
  onDelete,
  ...state
}: FacultiesTableProps) {
  return (
    <AdminTable
      aria-label="Fakultetlar"
      columns={COLUMNS}
      rowKey={(r) => r.id}
      minWidth="820px"
      emptyTitle="Fakultetlar yo'q"
      rowDim={(r) => !r.isActive}
      rowHref={facultyHref}
      rowActions={(r) => (
        <>
          <Button size="xs" onClick={() => onEdit(r)}>
            Tahrirlash
          </Button>
          <Button size="xs" onClick={() => onToggleStatus(r)}>
            {r.isActive ? 'Faol emas qilish' : 'Faollashtirish'}
          </Button>
          <Button size="xs" variant="danger" onClick={() => onDelete(r)}>
            O'chirish
          </Button>
        </>
      )}
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
