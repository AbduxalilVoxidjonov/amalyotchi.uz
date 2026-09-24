import { useMemo, useState, type ReactNode } from 'react';
import {
  Badge,
  DataTable,
  EmptyState,
  Input,
  ProgressBar,
  type DataTableColumn,
} from '@/shared/ui';
import { fmtDecimal } from '@/features/tutor/format';
import { gradeLabel } from '@/features/tutor/grading/types';
import { RowLink } from '../../components/RowLink';
import { APPLICATION_STATUS_LABEL } from '../../companies/types';
import {
  DEFAULT_STUDENT_SORT,
  filterStudents,
  sortStudents,
  type StudentSort,
  type StudentSortKey,
} from '../groupStudents';
import { periodStudentHref } from '../paths';
import type { PeriodGroupStudent } from '../types';
import styles from './PeriodGroup.module.css';

interface SortHeaderProps {
  label: string;
  /** Ekran o'quvchi uchun to'liq nom (masalan "Jami ball"). */
  fullLabel?: string;
  sortKey: StudentSortKey;
  sort: StudentSort;
  onSort: (key: StudentSortKey) => void;
  align?: 'right';
}

function SortHeader({ label, fullLabel, sortKey, sort, onSort, align }: SortHeaderProps) {
  const active = sort.key === sortKey;
  const state = active ? (sort.dir === 'asc' ? "o'sish tartibida" : 'kamayish tartibida') : null;
  return (
    <button
      type="button"
      className={styles.sortBtn}
      data-active={active || undefined}
      data-align={align}
      aria-label={`${fullLabel ?? label} bo'yicha saralash${state ? ` (${state})` : ''}`}
      onClick={() => onSort(sortKey)}
    >
      {label}
      <span className={styles.sortIcon} aria-hidden>
        {active ? (sort.dir === 'asc' ? '▲' : '▼') : '↕'}
      </span>
    </button>
  );
}

const dashOr = (v: number | null) => (v === null ? '—' : fmtDecimal(v));

function companyCell(r: PeriodGroupStudent): ReactNode {
  if (r.company) return r.company;
  if (r.applicationStatus) {
    const s = APPLICATION_STATUS_LABEL[r.applicationStatus];
    return (
      <span className={styles.appStatus}>
        <Badge status={s.kind}>{s.label}</Badge>
        <span className={styles.sub}>ariza</span>
      </span>
    );
  }
  return <span className={styles.sub}>—</span>;
}

function attendanceTitle(r: PeriodGroupStudent): string {
  return `Kelgan ${r.presentDays}, kechikkan ${r.lateDays}, kelmagan ${r.absentDays}, sababli ${r.excusedDays}`;
}

export interface PeriodGroupStudentsTableProps {
  periodId: string;
  students: readonly PeriodGroupStudent[];
  /** Davr boshlanmagan bo'lsa — davomat/ball ustunlari ko'rsatilmaydi. */
  started: boolean;
}

/**
 * Davr ichidagi guruh talabalari: FISH, HEMIS, korxona, davomat (kelgan/kech/kelmagan/sababli),
 * shubhali kunlar, kundaliklar, ball bo'laklari + jami, baho, yakunlangan belgisi.
 * Qidiruv (FISH/HEMIS) va saralash — client tomonda (guruh kichik, bitta so'rovda keladi).
 */
export function PeriodGroupStudentsTable({
  periodId,
  students,
  started,
}: PeriodGroupStudentsTableProps) {
  const [search, setSearch] = useState('');
  const [sort, setSort] = useState<StudentSort>(DEFAULT_STUDENT_SORT);

  const rows = useMemo(
    () => sortStudents(filterStudents(students, search), sort),
    [students, search, sort],
  );

  const onSort = (key: StudentSortKey) =>
    setSort((prev) =>
      prev.key === key
        ? { key, dir: prev.dir === 'asc' ? 'desc' : 'asc' }
        : // Raqamli ustunlar birinchi bosishda kamayish tartibida (eng yuqorisi tepada).
          { key, dir: key === 'fullName' ? 'asc' : 'desc' },
    );

  const sh = (label: string, key: StudentSortKey, fullLabel?: string, align?: 'right') => (
    <SortHeader
      label={label}
      sortKey={key}
      sort={sort}
      onSort={onSort}
      {...(fullLabel ? { fullLabel } : {})}
      {...(align ? { align } : {})}
    />
  );

  const identity: DataTableColumn<PeriodGroupStudent>[] = [
    {
      key: 'index',
      header: '№',
      width: '40px',
      mono: true,
      dim: true,
      align: 'right',
      render: (_r, i) => i + 1,
    },
    {
      key: 'fullName',
      header: sh('FISH', 'fullName'),
      width: 'minmax(170px,1.5fr)',
      strong: true,
      wrap: true,
      render: (r) => <RowLink to={periodStudentHref(periodId, r.id)}>{r.fullName}</RowLink>,
    },
    { key: 'hemisId', header: 'HEMIS ID', width: 'minmax(80px,.6fr)', mono: true, dim: true },
    {
      key: 'company',
      header: 'Korxona',
      width: 'minmax(150px,1.2fr)',
      wrap: true,
      render: companyCell,
    },
  ];

  const metrics: DataTableColumn<PeriodGroupStudent>[] = [
    {
      key: 'attendancePct',
      header: sh('Davomat', 'attendancePct'),
      width: 'minmax(130px,1fr)',
      render: (r) => (
        <div className={styles.twoLine} title={attendanceTitle(r)}>
          <ProgressBar value={r.attendancePct} label={`${r.fullName} davomati`} />
          <span className={styles.sub} aria-label={attendanceTitle(r)}>
            <span data-tone="ok">{r.presentDays}</span>
            {' · '}
            <span data-tone="late">{r.lateDays}</span>
            {' · '}
            <span data-tone="bad">{r.absentDays}</span>
            {' · '}
            <span>{r.excusedDays}</span>
          </span>
        </div>
      ),
    },
    {
      key: 'suspiciousDays',
      header: sh('Shubhali', 'suspiciousDays', 'Shubhali kunlar', 'right'),
      width: 'minmax(84px,.6fr)',
      mono: true,
      align: 'right',
      render: (r) =>
        r.suspiciousDays > 0 ? (
          <Badge status="bad">{r.suspiciousDays}</Badge>
        ) : (
          <span className={styles.sub}>0</span>
        ),
    },
    {
      key: 'diaryCount',
      header: sh('Kundalik', 'diaryCount', 'Kundaliklar soni'),
      width: 'minmax(90px,.6fr)',
      mono: true,
      render: (r) => (
        <div className={styles.twoLine}>
          <span>{r.diaryCount} ta</span>
          <span className={styles.sub}>
            {r.diaryCount > 0 ? `o'rt. ${fmtDecimal(r.diaryAvg)}` : '—'}
          </span>
        </div>
      ),
    },
    {
      key: 'attendancePoints',
      header: <span title="Davomat balli (40 dan)">Dav. /40</span>,
      width: 'minmax(64px,.45fr)',
      mono: true,
      align: 'right',
      render: (r) => fmtDecimal(r.attendancePoints),
    },
    {
      key: 'reportPoints',
      header: <span title="Hisobot balli (30 dan)">His. /30</span>,
      width: 'minmax(64px,.45fr)',
      mono: true,
      align: 'right',
      render: (r) => fmtDecimal(r.reportPoints),
    },
    {
      key: 'tutorPoints',
      header: <span title="Tyutor balli (20 dan)">Tyut. /20</span>,
      width: 'minmax(64px,.45fr)',
      mono: true,
      align: 'right',
      render: (r) => dashOr(r.tutorPoints),
    },
    {
      key: 'referencePoints',
      header: <span title="Tavsifnoma balli (10 dan)">Tavs. /10</span>,
      width: 'minmax(64px,.45fr)',
      mono: true,
      align: 'right',
      render: (r) => dashOr(r.referencePoints),
    },
    {
      key: 'total',
      header: sh('Jami', 'total', 'Jami ball', 'right'),
      width: 'minmax(70px,.5fr)',
      mono: true,
      strong: true,
      align: 'right',
      render: (r) => fmtDecimal(r.total),
    },
    {
      key: 'grade',
      header: 'Baho',
      width: 'minmax(130px,.8fr)',
      render: (r) => {
        const g = gradeLabel(r);
        return <Badge status={g.kind}>{g.label}</Badge>;
      },
    },
    {
      key: 'finalized',
      header: 'Holat',
      width: 'minmax(104px,.7fr)',
      render: (r) =>
        r.finalized ? (
          <Badge status="ok">Yakunlangan</Badge>
        ) : (
          <span className={styles.sub}>Jarayonda</span>
        ),
    },
  ];

  const columns = started ? [...identity, ...metrics] : identity;

  return (
    <DataTable
      aria-label="Guruh talabalari"
      columns={columns}
      rows={rows}
      rowKey={(r) => r.id}
      rowHref={(r) => periodStudentHref(periodId, r.id)}
      minWidth={started ? '1480px' : '640px'}
      emptyText={
        <EmptyState
          tone="plain"
          className={styles.empty}
          title={students.length === 0 ? "Guruhda talaba yo'q" : 'Talaba topilmadi'}
          description={students.length > 0 ? "Qidiruv bo'yicha hech narsa topilmadi." : undefined}
        />
      }
      toolbar={
        <>
          <h2 className={styles.tableTitle}>
            Talabalar (
            {rows.length === students.length
              ? students.length
              : `${rows.length} / ${students.length}`}
            )
          </h2>
          <Input
            variant="search"
            type="search"
            placeholder="FISH yoki HEMIS ID…"
            aria-label="Talabani qidirish"
            value={search}
            onChange={(e) => setSearch(e.target.value)}
          />
        </>
      }
    />
  );
}
