import type { ReactNode } from 'react';
import { Link } from 'react-router-dom';
import { Button, DataTable, EmptyState, ProgressBar, type DataTableColumn } from '@/shared/ui';
import { fmtDecimal } from '@/features/tutor/format';
import { RowLink } from '../../components/RowLink';
import { formatCount } from '../../shared/format';
import { periodGroupHref } from '../paths';
import type { PeriodGroupStats, PracticePeriodGroup } from '../types';
import { GradeDistributionView } from './GradeDistributionView';
import { MetricSkeleton } from './MetricSkeleton';
import styles from './PeriodDetail.module.css';

/** Yo'nalish guruhlari sahifasi (ierarxiya). */
const directionHref = (g: PracticePeriodGroup) =>
  `/admin/faculties/${g.facultyId}/departments/${g.departmentId}/directions/${g.directionId}`;

/**
 * Guruh ko'rsatkichlari holati:
 * `ready` — `stats` bor; `loading` — skeleton; `unavailable` — xato/guruh statistikada yo'q ("—");
 * `hidden` — ustunlar umuman ko'rsatilmaydi (rejalashtirilgan davr).
 */
export type GroupMetricsState = 'ready' | 'loading' | 'unavailable' | 'hidden';

function baseColumns(periodId: string): DataTableColumn<PracticePeriodGroup>[] {
  return [
    {
      key: 'code',
      header: 'Guruh',
      width: 'minmax(80px,.6fr)',
      strong: true,
      mono: true,
      render: (g) => <RowLink to={periodGroupHref(periodId, g.id)}>{g.code}</RowLink>,
    },
    {
      key: 'course',
      header: 'Kurs',
      width: 'minmax(60px,.4fr)',
      mono: true,
      render: (g) => `${g.course}-kurs`,
    },
    {
      key: 'facultyName',
      header: 'Fakultet / kafedra',
      width: 'minmax(170px,1.3fr)',
      wrap: true,
      render: (g) => (
        <div className={styles.twoLine}>
          <span>{g.facultyName}</span>
          <span className={styles.subLine}>{g.departmentName}</span>
        </div>
      ),
    },
    {
      key: 'directionName',
      header: "Yo'nalish",
      width: 'minmax(140px,1.1fr)',
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
      width: 'minmax(70px,.5fr)',
      mono: true,
      align: 'right',
      render: (g) => formatCount(g.studentsCount),
    },
  ];
}

interface MetricColumnSpec {
  key: string;
  header: string;
  width: string;
  mono?: boolean;
  align?: 'right';
  skeleton: number;
  render: (s: PeriodGroupStats) => ReactNode;
}

const METRIC_COLUMNS: readonly MetricColumnSpec[] = [
  {
    key: 'attendancePct',
    header: 'Davomat',
    width: 'minmax(120px,.9fr)',
    skeleton: 90,
    render: (s) => (
      <div className={styles.twoLine}>
        <ProgressBar value={s.attendancePct} label={`${s.code} o'rtacha davomati`} />
        {s.lowAttendanceCount > 0 && (
          <span className={styles.subLine} data-tone="bad">
            {s.lowAttendanceCount} talaba &lt;70%
          </span>
        )}
      </div>
    ),
  },
  {
    key: 'withCompanyCount',
    header: 'Korxona',
    width: 'minmax(78px,.55fr)',
    mono: true,
    skeleton: 40,
    render: (s) => (
      <span
        title={`${s.withCompanyCount} talaba korxonaga biriktirilgan, ${s.pendingApplicationsCount} ariza kutilmoqda`}
        data-tone={s.withCompanyCount < s.studentsCount ? 'late' : undefined}
        className={styles.tone}
      >
        {s.withCompanyCount}/{s.studentsCount}
      </span>
    ),
  },
  {
    key: 'diaryAvgScore',
    header: 'Kundalik',
    width: 'minmax(78px,.55fr)',
    mono: true,
    skeleton: 36,
    render: (s) => (
      <div className={styles.twoLine}>
        <span>{s.diaryCount > 0 ? fmtDecimal(s.diaryAvgScore) : '—'}</span>
        <span className={styles.subLine}>{formatCount(s.diaryCount)} ta</span>
      </div>
    ),
  },
  {
    key: 'avgTotal',
    header: "O'rt. ball",
    width: 'minmax(72px,.5fr)',
    mono: true,
    align: 'right',
    skeleton: 36,
    render: (s) => (s.avgTotal === null ? '—' : fmtDecimal(s.avgTotal)),
  },
  {
    key: 'grades',
    header: '5·4·3·2·Q',
    width: 'minmax(104px,.7fr)',
    skeleton: 80,
    render: (s) => <GradeDistributionView grades={s.grades} />,
  },
  {
    key: 'retake',
    header: 'Qayta',
    width: 'minmax(56px,.4fr)',
    mono: true,
    align: 'right',
    skeleton: 20,
    render: (s) => (
      <span className={styles.tone} data-tone={s.grades.retake > 0 ? 'bad' : undefined}>
        {s.grades.retake}
      </span>
    ),
  },
];

function metricColumns(
  stats: ReadonlyMap<string, PeriodGroupStats>,
  state: Exclude<GroupMetricsState, 'hidden'>,
): DataTableColumn<PracticePeriodGroup>[] {
  return METRIC_COLUMNS.map((c) => ({
    key: c.key,
    header: c.header,
    width: c.width,
    ...(c.mono ? { mono: true } : {}),
    ...(c.align ? { align: c.align } : {}),
    render: (g) => {
      if (state === 'loading') return <MetricSkeleton width={c.skeleton} />;
      const s = stats.get(g.id);
      return s ? c.render(s) : <span className={styles.subLine}>—</span>;
    },
  }));
}

export interface PeriodGroupsTableProps {
  periodId: string;
  groups: readonly PracticePeriodGroup[];
  /** Guruh ko'rsatkichlari (`groupId` bo'yicha). */
  stats?: ReadonlyMap<string, PeriodGroupStats> | undefined;
  metricsState?: GroupMetricsState;
  /** Yopilgan davrda `false` — qo'shish/ajratish yashiriladi. */
  editable: boolean;
  onAdd: () => void;
  onDetach: (group: PracticePeriodGroup) => void;
}

const EMPTY_STATS: ReadonlyMap<string, PeriodGroupStats> = new Map();

/**
 * Davrga biriktirilgan guruhlar + ko'rsatkich ustunlari. Guruh kodi (yoki qatorning istalgan joyi)
 * davr ichidagi guruh sahifasiga olib boradi. Jadval `period.groups` dan chiziladi — statistika
 * kechiksa ham guruhlar darhol ko'rinadi, metrik kataklarda skeleton.
 */
export function PeriodGroupsTable({
  periodId,
  groups,
  stats = EMPTY_STATS,
  metricsState = 'hidden',
  editable,
  onAdd,
  onDetach,
}: PeriodGroupsTableProps) {
  const columns =
    metricsState === 'hidden'
      ? baseColumns(periodId)
      : [...baseColumns(periodId), ...metricColumns(stats, metricsState)];

  return (
    <DataTable
      aria-label="Biriktirilgan guruhlar"
      aria-busy={metricsState === 'loading' || undefined}
      columns={columns}
      rows={groups}
      rowKey={(g) => g.id}
      rowHref={(g) => periodGroupHref(periodId, g.id)}
      minWidth={metricsState === 'hidden' ? '760px' : '1320px'}
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
