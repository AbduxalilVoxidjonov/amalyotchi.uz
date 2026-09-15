import { Badge, Button, ProgressBar, type DataTableColumn } from '@/shared/ui';
import { AdminTable, type TableStateProps } from '../../../components/AdminTable';
import type { GroupRow } from '../types';

/**
 * Yo'nalish ichidagi guruhlar. Global `/admin/groups` jadvalidan farqli — "yo'nalish"/"fakultet"
 * ustunlari yo'q (breadcrumb orqali allaqachon ma'lum), o'rniga `isActive` va CRUD amallari bor.
 */
const COLUMNS: DataTableColumn<GroupRow>[] = [
  { key: 'code', header: 'Guruh', width: 'minmax(100px,.9fr)', mono: true, strong: true },
  { key: 'course', header: 'Kurs', width: 'minmax(70px,.6fr)', mono: true },
  {
    key: 'tutor',
    header: 'Tyutor',
    width: 'minmax(160px,1.4fr)',
    render: (r) => r.tutor ?? <span data-dim>—</span>,
  },
  { key: 'students', header: 'Talaba', width: 'minmax(80px,.7fr)', mono: true },
  {
    key: 'attendancePct',
    header: 'Davomat',
    width: 'minmax(150px,1.2fr)',
    render: (r) => (
      <ProgressBar
        value={r.attendancePct}
        showValue={r.period ? true : 'blank'}
        label={`${r.code} davomati`}
      />
    ),
  },
  {
    key: 'isActive',
    header: 'Holat',
    width: 'minmax(110px,.8fr)',
    render: (r) => (
      <Badge status={r.isActive ? 'ok' : 'neu'}>{r.isActive ? 'Faol' : 'Faol emas'}</Badge>
    ),
  },
];

export interface DirectionGroupsTableProps extends TableStateProps<GroupRow> {
  onCreate: () => void;
  onEdit: (group: GroupRow) => void;
  onToggleStatus: (group: GroupRow) => void;
  onDelete: (group: GroupRow) => void;
}

export function DirectionGroupsTable({
  onCreate,
  onEdit,
  onToggleStatus,
  onDelete,
  ...state
}: DirectionGroupsTableProps) {
  return (
    <AdminTable
      aria-label="Guruhlar"
      columns={COLUMNS}
      rowKey={(r) => r.id}
      minWidth="760px"
      emptyTitle="Guruhlar yo'q"
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
          Yangi guruh
        </Button>
      }
      {...state}
    />
  );
}
