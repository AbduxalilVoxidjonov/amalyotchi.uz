import { Button, ProgressBar, type DataTableColumn } from '@/shared/ui';
import { AdminTable, type TableStateProps } from '../../components/AdminTable';
import type { Group } from '../types';

/** SPEC §9.4 ustunlari. Faol davri yo'q guruh — davomat foizi bo'sh (bar 0). */
const COLUMNS: DataTableColumn<Group>[] = [
  { key: 'code', header: 'Guruh', width: 'minmax(100px,.9fr)', mono: true, strong: true },
  { key: 'course', header: 'Kurs', width: 'minmax(70px,.6fr)', mono: true },
  { key: 'direction', header: "Yo'nalish", width: 'minmax(180px,1.6fr)' },
  {
    key: 'tutor',
    header: 'Tyutor',
    width: 'minmax(140px,1.3fr)',
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
];

export interface GroupsTableProps extends TableStateProps<Group> {
  onCreate: () => void;
  onMoveCourse: () => void;
  onArchive: () => void;
}

export function GroupsTable({ onCreate, onMoveCourse, onArchive, ...state }: GroupsTableProps) {
  return (
    <AdminTable
      aria-label="Guruhlar"
      columns={COLUMNS}
      rowKey={(r) => r.id}
      minWidth="760px"
      emptyTitle="Guruhlar yo'q"
      actions={
        <>
          <Button size="xs" onClick={onCreate}>
            Yangi guruh
          </Button>
          <Button size="xs" onClick={onMoveCourse}>
            Kursga ko'chirish
          </Button>
          <Button size="xs" onClick={onArchive}>
            Arxivlash
          </Button>
        </>
      }
      {...state}
    />
  );
}
