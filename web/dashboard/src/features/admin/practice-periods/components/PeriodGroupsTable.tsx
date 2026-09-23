import { Link } from 'react-router-dom';
import { Button, DataTable, EmptyState, type DataTableColumn } from '@/shared/ui';
import { formatCount } from '../../shared/format';
import type { PracticePeriodGroup } from '../types';
import styles from './PeriodDetail.module.css';

/** Yo'nalish guruhlari sahifasi (alohida guruh sahifasi yo'q — qator `rowHref` siz). */
const directionHref = (g: PracticePeriodGroup) =>
  `/admin/faculties/${g.facultyId}/departments/${g.departmentId}/directions/${g.directionId}`;

const COLUMNS: DataTableColumn<PracticePeriodGroup>[] = [
  { key: 'code', header: 'Guruh', width: 'minmax(90px,.7fr)', strong: true, mono: true },
  {
    key: 'course',
    header: 'Kurs',
    width: 'minmax(60px,.4fr)',
    mono: true,
    render: (g) => `${g.course}-kurs`,
  },
  { key: 'facultyName', header: 'Fakultet', width: 'minmax(150px,1.2fr)', wrap: true },
  { key: 'departmentName', header: 'Kafedra', width: 'minmax(150px,1.2fr)', dim: true, wrap: true },
  {
    key: 'directionName',
    header: "Yo'nalish",
    width: 'minmax(150px,1.2fr)',
    wrap: true,
    render: (g) => (
      <Link className={styles.link} to={directionHref(g)}>
        {g.directionName}
      </Link>
    ),
  },
  {
    key: 'studentsCount',
    header: 'Talabalar',
    width: 'minmax(80px,.5fr)',
    mono: true,
    render: (g) => formatCount(g.studentsCount),
  },
];

export interface PeriodGroupsTableProps {
  groups: readonly PracticePeriodGroup[];
  /** Yopilgan davrda `false` — qo'shish/ajratish yashiriladi. */
  editable: boolean;
  onAdd: () => void;
  onDetach: (group: PracticePeriodGroup) => void;
}

export function PeriodGroupsTable({ groups, editable, onAdd, onDetach }: PeriodGroupsTableProps) {
  return (
    <DataTable
      aria-label="Biriktirilgan guruhlar"
      columns={COLUMNS}
      rows={groups}
      rowKey={(g) => g.id}
      minWidth="860px"
      {...(editable
        ? {
            actions: (g: PracticePeriodGroup) => (
              <Button
                size="xs"
                variant="danger"
                aria-label={`${g.code} guruhini ajratish`}
                onClick={() => onDetach(g)}
              >
                Ajratish
              </Button>
            ),
          }
        : {})}
      emptyText={
        <EmptyState
          tone="plain"
          className={styles.empty}
          title="Guruh biriktirilmagan"
          description={editable ? '"Guruh qo\'shish" orqali guruhlarni biriktiring.' : undefined}
        />
      }
      toolbar={
        <>
          <h2 className={styles.tableTitle}>Biriktirilgan guruhlar ({groups.length})</h2>
          {editable && (
            <Button size="xs" variant="primary" onClick={onAdd}>
              Guruh qo'shish
            </Button>
          )}
        </>
      }
    />
  );
}
