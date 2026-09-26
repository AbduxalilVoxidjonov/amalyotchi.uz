import type { ReactNode } from 'react';
import {
  Badge,
  DataTable,
  EmptyState,
  PersonCell,
  ProgressBar,
  type DataTableColumn,
} from '@/shared/ui';
import { ErrorState, LoadingState } from '../../components/PageStatus';
import { DASH, formatCount } from '../../shared/format';
import { COMPANY_STUDENT_STATE_LABEL, type CompanyStudent } from '../types';
import styles from './CompanyStudentsTable.module.css';

/** Talaba profili — ism havolasi va butun qator bosilishi uchun bitta manba. */
const studentHref = (r: CompanyStudent) => `/admin/students/${r.studentId}`;

const COLUMNS: DataTableColumn<CompanyStudent>[] = [
  {
    key: 'name',
    header: 'FISH',
    width: 'minmax(190px,1.6fr)',
    // Ism — talaba profiliga havola (`/admin/students/:studentId`).
    render: (r) => <PersonCell name={r.name} sub={`HEMIS ${r.hemisId}`} to={studentHref(r)} />,
  },
  { key: 'group', header: 'Guruh', width: 'minmax(90px,.7fr)', mono: true, dim: true },
  {
    key: 'course',
    header: 'Kurs',
    width: 'minmax(80px,.6fr)',
    mono: true,
    render: (r) => `${r.course}-kurs`,
  },
  { key: 'faculty', header: 'Fakultet', width: 'minmax(160px,1.3fr)', wrap: true },
  {
    key: 'tutorName',
    header: 'Tyutor',
    width: 'minmax(140px,1.1fr)',
    render: (r) => r.tutorName ?? DASH,
  },
  // Ariza ustuni yo'q: endpoint faqat aktiv amaliyotchilarni qaytaradi — ariza doim `approved`.
  {
    key: 'periodName',
    header: 'Davr',
    width: 'minmax(150px,1.1fr)',
    wrap: true,
    render: (r) => r.periodName ?? DASH,
  },
  {
    key: 'attendance',
    header: 'Davomat',
    width: 'minmax(150px,1.3fr)',
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
    width: 'minmax(90px,.7fr)',
    mono: true,
    render: (r) => formatCount(r.diaryCount),
  },
  {
    key: 'state',
    header: 'Holat',
    width: 'minmax(120px,.9fr)',
    render: (r) => {
      const s = COMPANY_STUDENT_STATE_LABEL[r.state];
      const label = r.state === 'suspicious' ? `${r.suspiciousCount} shubhali` : s.label;
      return <Badge status={s.kind}>{label}</Badge>;
    },
  },
];

export interface CompanyStudentsTableProps {
  rows: readonly CompanyStudent[];
  isLoading: boolean;
  error: unknown;
  onRetry: () => void;
}

/**
 * Shu korxonadagi AKTIV amaliyotchilar jadvali: faqat ochiq, hozir davom etayotgan davrda
 * tasdiqlangan arizasi bor talabalar (loading / xato / bo'sh holatlari jadval ichida).
 */
export function CompanyStudentsTable({
  rows,
  isLoading,
  error,
  onRetry,
}: CompanyStudentsTableProps) {
  const state: ReactNode = isLoading ? (
    <LoadingState />
  ) : error ? (
    <ErrorState inline error={error} onRetry={onRetry} />
  ) : (
    <EmptyState
      tone="plain"
      className={styles.empty}
      title="Aktiv amaliyotchilar yo'q"
      description="Hozirda bu korxonada aktiv amaliyot o'tayotgan talaba yo'q."
    />
  );

  return (
    <DataTable
      aria-label="Aktiv amaliyotchilar"
      aria-busy={isLoading || undefined}
      columns={COLUMNS}
      rows={isLoading || error ? [] : rows}
      rowKey={(r) => r.studentId}
      rowHref={studentHref}
      minWidth="1040px"
      toolbar={
        <div>
          <h2 className={styles.tableTitle}>Aktiv amaliyotchilar</h2>
          <p className={styles.tableSub}>
            {isLoading || error
              ? 'Yuklanmoqda…'
              : `Hozir amaliyot o'tayotganlar: ${formatCount(rows.length)} ta talaba`}
          </p>
        </div>
      }
      emptyText={state}
    />
  );
}
