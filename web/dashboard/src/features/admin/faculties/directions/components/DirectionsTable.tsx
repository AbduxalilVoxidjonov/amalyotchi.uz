import { Badge, Button, type DataTableColumn } from '@/shared/ui';
import { AdminTable, type TableStateProps } from '../../../components/AdminTable';
import { RowLink } from '../../../components/RowLink';
import type { DirectionRow } from '../types';

function columns(facultyId: string, departmentId: string): DataTableColumn<DirectionRow>[] {
  return [
    {
      key: 'name',
      header: "Yo'nalish",
      width: 'minmax(200px,2fr)',
      strong: true,
      render: (r) => (
        <RowLink
          to={`/admin/faculties/${facultyId}/departments/${departmentId}/directions/${r.id}`}
        >
          {r.name}
        </RowLink>
      ),
    },
    { key: 'code', header: 'Kod', width: 'minmax(90px,.8fr)', mono: true },
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

export interface DirectionsTableProps extends TableStateProps<DirectionRow> {
  facultyId: string;
  departmentId: string;
  onCreate: () => void;
  onEdit: (direction: DirectionRow) => void;
  onToggleStatus: (direction: DirectionRow) => void;
  onDelete: (direction: DirectionRow) => void;
}

export function DirectionsTable({
  facultyId,
  departmentId,
  onCreate,
  onEdit,
  onToggleStatus,
  onDelete,
  ...state
}: DirectionsTableProps) {
  return (
    <AdminTable
      aria-label="Yo'nalishlar"
      columns={columns(facultyId, departmentId)}
      rowKey={(r) => r.id}
      minWidth="700px"
      emptyTitle="Yo'nalishlar yo'q"
      rowDim={(r) => !r.isActive}
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
          Yangi yo'nalish
        </Button>
      }
      {...state}
    />
  );
}
