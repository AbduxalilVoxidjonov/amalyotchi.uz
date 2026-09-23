import { Badge, Button, type DataTableColumn } from '@/shared/ui';
import { AdminTable, type TableStateProps } from '../../../components/AdminTable';
import { RowLink } from '../../../components/RowLink';
import type { DepartmentRow } from '../types';

/** Kafedra ichki sahifasi (yo'nalishlar ro'yxati) — nom havolasi va butun qator uchun. */
function departmentHref(facultyId: string, departmentId: string): string {
  return `/admin/faculties/${facultyId}/departments/${departmentId}`;
}

function columns(facultyId: string): DataTableColumn<DepartmentRow>[] {
  return [
    {
      key: 'name',
      header: 'Kafedra',
      width: 'minmax(200px,2fr)',
      strong: true,
      render: (r) => <RowLink to={departmentHref(facultyId, r.id)}>{r.name}</RowLink>,
    },
    { key: 'code', header: 'Kod', width: 'minmax(90px,.8fr)', mono: true },
    { key: 'directions', header: "Yo'nalish", width: 'minmax(100px,.9fr)', mono: true },
    { key: 'groups', header: 'Guruh', width: 'minmax(80px,.8fr)', mono: true },
    { key: 'students', header: 'Talaba', width: 'minmax(90px,.9fr)', mono: true },
    {
      key: 'isActive',
      header: 'Holat',
      width: 'minmax(110px,.8fr)',
      render: (r) => (
        <Badge status={r.isActive ? 'ok' : 'neu'}>{r.isActive ? 'Faol' : 'Faol emas'}</Badge>
      ),
    },
  ];
}

export interface DepartmentsTableProps extends TableStateProps<DepartmentRow> {
  facultyId: string;
  onCreate: () => void;
  onEdit: (department: DepartmentRow) => void;
  onToggleStatus: (department: DepartmentRow) => void;
  onDelete: (department: DepartmentRow) => void;
}

export function DepartmentsTable({
  facultyId,
  onCreate,
  onEdit,
  onToggleStatus,
  onDelete,
  ...state
}: DepartmentsTableProps) {
  return (
    <AdminTable
      aria-label="Kafedralar"
      columns={columns(facultyId)}
      rowKey={(r) => r.id}
      minWidth="760px"
      emptyTitle="Kafedralar yo'q"
      rowDim={(r) => !r.isActive}
      rowHref={(r) => departmentHref(facultyId, r.id)}
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
        <Button size="xs" onClick={onCreate}>
          Yangi kafedra
        </Button>
      }
      {...state}
    />
  );
}
