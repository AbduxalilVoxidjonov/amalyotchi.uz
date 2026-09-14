import { Badge, DataTable, PersonCell, ProgressBar, type DataTableColumn } from '@/shared/ui';
import { fmtDecimal } from '../../format';
import { studentStateLabel, type TutorStudent } from '../types';
import styles from './StudentsTable.module.css';

/** SPEC-SCREENS §5 — 1.7fr .6fr 1.5fr 1.5fr .8fr .8fr; toolbar/footer yo'q. */
const COLUMNS: DataTableColumn<TutorStudent>[] = [
  {
    key: 'name',
    header: 'Talaba',
    width: '1.7fr',
    render: (r) => <PersonCell name={r.name} sub={`HEMIS ${r.hemisId}`} />,
  },
  { key: 'group', header: 'Guruh', width: '.6fr', mono: true, dim: true },
  {
    key: 'company',
    header: 'Korxona',
    width: '1.5fr',
    render: (r) => <span className={styles.company}>{r.company ?? '—'}</span>,
  },
  {
    key: 'attendance',
    header: 'Davomat',
    width: '1.5fr',
    render: (r) => (
      <div>
        <ProgressBar value={r.attendancePct} label={`${r.name} davomati`} />
        <div className={styles.days}>
          {r.attendedDays}/{r.totalDays} kun
        </div>
      </div>
    ),
  },
  {
    key: 'diary',
    header: 'Kundalik',
    width: '.8fr',
    mono: true,
    render: (r) => (
      <span className={styles.diary}>
        {r.diaryCount} ta · {fmtDecimal(r.diaryAvg)}
      </span>
    ),
  },
  {
    key: 'state',
    header: 'Holat',
    width: '.8fr',
    render: (r) => {
      const s = studentStateLabel(r);
      return <Badge status={s.kind}>{s.label}</Badge>;
    },
  },
];

export function StudentsTable({ rows }: { rows: readonly TutorStudent[] }) {
  return (
    <DataTable
      aria-label="Talabalarim"
      columns={COLUMNS}
      rows={rows}
      rowKey={(r) => r.id}
      minWidth="760px"
      emptyText="Biriktirilgan talabalar yo'q"
    />
  );
}
