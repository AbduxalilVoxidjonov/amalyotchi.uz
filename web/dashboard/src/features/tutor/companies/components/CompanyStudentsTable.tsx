import { Badge, DataTable, PersonCell, ProgressBar, type DataTableColumn } from '@/shared/ui';
import {
  APPLICATION_STATUS_LABEL,
  COMPANY_STUDENT_STATE_LABEL,
  type CompanyStudent,
} from '../types';
import styles from './CompanyStudentsTable.module.css';

const COLUMNS: DataTableColumn<CompanyStudent>[] = [
  {
    key: 'name',
    header: 'Talaba',
    width: 'minmax(180px,1.6fr)',
    // Ism — talaba profiliga havola (`/tutor/students/:studentId`).
    render: (r) => (
      <PersonCell
        name={r.name}
        sub={`HEMIS ${r.hemisId}`}
        to={`/tutor/students/${r.studentId}`}
      />
    ),
  },
  { key: 'group', header: 'Guruh', width: 'minmax(90px,.6fr)', mono: true, dim: true },
  {
    key: 'course',
    header: 'Kurs',
    width: 'minmax(80px,.5fr)',
    mono: true,
    render: (r) => `${r.course}-kurs`,
  },
  {
    key: 'periodName',
    header: 'Davr',
    width: 'minmax(150px,1.2fr)',
    wrap: true,
    render: (r) => r.periodName ?? '—',
  },
  {
    key: 'applicationStatus',
    header: 'Ariza',
    width: 'minmax(120px,.9fr)',
    render: (r) => (
      <Badge status={APPLICATION_STATUS_LABEL[r.applicationStatus].kind}>
        {APPLICATION_STATUS_LABEL[r.applicationStatus].label}
      </Badge>
    ),
  },
  {
    key: 'attendance',
    header: 'Davomat',
    width: 'minmax(140px,1.3fr)',
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
    key: 'diaryCount',
    header: 'Kundalik',
    width: 'minmax(90px,.6fr)',
    mono: true,
    render: (r) => `${r.diaryCount} ta`,
  },
  {
    key: 'state',
    header: 'Holat',
    width: 'minmax(120px,.8fr)',
    render: (r) => {
      const s = COMPANY_STUDENT_STATE_LABEL[r.state];
      return (
        <Badge status={s.kind}>
          {r.state === 'suspicious' ? `${r.suspiciousCount} shubhali` : s.label}
        </Badge>
      );
    },
  },
];

/** Shu korxonadagi (ko'lamdagi) talabalar jadvali. */
export function CompanyStudentsTable({ rows }: { rows: readonly CompanyStudent[] }) {
  return (
    <DataTable
      aria-label="Korxona talabalari"
      columns={COLUMNS}
      rows={rows}
      rowKey={(r) => r.studentId}
      minWidth="940px"
      toolbar={
        <div>
          <h2 className={styles.tableTitle}>Korxonadagi talabalarim</h2>
          <p className={styles.tableSub}>{rows.length} ta talaba</p>
        </div>
      }
      emptyText="Talabalar yo'q"
    />
  );
}
