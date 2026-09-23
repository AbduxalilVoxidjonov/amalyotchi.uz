import { Link } from 'react-router-dom';
import { Button, DataTable, EmptyState, Pill, PillGroup, type DataTableColumn } from '@/shared/ui';
import { ErrorState, LoadingState } from '../../components/PageStatus';
import { RowLink } from '../../components/RowLink';
import { formatCount } from '../../shared/format';
import { calendarDays, formatDays, formatRange } from '../dates';
import { NEW_PERIOD_HREF, periodHref } from '../paths';
import {
  PERIOD_STATUSES,
  PERIOD_STATUS_LABEL,
  type PracticePeriodListItem,
  type PracticePeriodStatus,
} from '../types';
import { PeriodStatusBadge } from './PeriodStatusBadge';
import styles from './PracticePeriodsTable.module.css';

const COLUMNS: DataTableColumn<PracticePeriodListItem>[] = [
  {
    key: 'name',
    header: 'Nomi',
    width: 'minmax(200px,1.8fr)',
    strong: true,
    render: (r) => <RowLink to={periodHref(r)}>{r.name}</RowLink>,
  },
  {
    key: 'dates',
    header: 'Sanalar',
    width: 'minmax(210px,1.4fr)',
    render: (r) => (
      <span className={styles.dates}>
        <span className={styles.range}>{formatRange(r.startDate, r.endDate)}</span>
        <span className={styles.days}>{formatDays(calendarDays(r.startDate, r.endDate))}</span>
      </span>
    ),
  },
  {
    key: 'status',
    header: 'Status',
    width: 'minmax(110px,.8fr)',
    render: (r) => <PeriodStatusBadge status={r.status} />,
  },
  {
    key: 'groupsCount',
    header: 'Guruhlar',
    width: 'minmax(90px,.6fr)',
    mono: true,
    render: (r) => formatCount(r.groupsCount),
  },
  {
    key: 'studentsCount',
    header: 'Talabalar',
    width: 'minmax(90px,.6fr)',
    mono: true,
    render: (r) => formatCount(r.studentsCount),
  },
];

export interface PracticePeriodsTableProps {
  rows: readonly PracticePeriodListItem[] | undefined;
  isLoading: boolean;
  error: unknown;
  onRetry: () => void;
  /** `null` — "Hammasi". */
  status: PracticePeriodStatus | null;
  onStatusChange: (status: PracticePeriodStatus | null) => void;
}

const FILTERS: { value: PracticePeriodStatus | null; label: string }[] = [
  { value: null, label: 'Hammasi' },
  ...PERIOD_STATUSES.map((s) => ({ value: s, label: PERIOD_STATUS_LABEL[s].label })),
];

/** Admin · Amaliyot davrlari ro'yxati: status chip filtri + jadval (qator → detail). */
export function PracticePeriodsTable({
  rows,
  isLoading,
  error,
  onRetry,
  status,
  onStatusChange,
}: PracticePeriodsTableProps) {
  const items = !isLoading && !error ? (rows ?? []) : [];

  const emptyState = isLoading ? (
    <LoadingState />
  ) : error ? (
    <ErrorState inline error={error} onRetry={onRetry} />
  ) : status ? (
    <EmptyState
      tone="plain"
      className={styles.empty}
      title={`"${PERIOD_STATUS_LABEL[status].label}" holatidagi davr yo'q`}
      description="Boshqa filtrni tanlang yoki yangi davr yarating."
    />
  ) : (
    <EmptyState
      tone="plain"
      className={styles.empty}
      title="Hali amaliyot davri yo'q"
      description="Amaliyot davri guruhlarga amaliyot qachon boshlanib, qachon tugashini belgilaydi. Davr yarating va unga fakultet, kafedra, yo'nalish bo'yicha guruhlarni biriktiring."
      action={
        <Button asChild variant="primary" size="sm">
          <Link to={NEW_PERIOD_HREF}>Yangi amaliyot davri</Link>
        </Button>
      }
    />
  );

  return (
    <DataTable
      aria-label="Amaliyot davrlari"
      aria-busy={isLoading || undefined}
      columns={COLUMNS}
      rows={items}
      rowKey={(r) => r.id}
      rowHref={periodHref}
      minWidth="760px"
      emptyText={emptyState}
      toolbar={
        <>
          <PillGroup role="group" aria-label="Status bo'yicha filtr">
            {FILTERS.map((f) => (
              <Pill
                key={f.label}
                active={status === f.value}
                onClick={() => onStatusChange(f.value)}
              >
                {f.label}
              </Pill>
            ))}
          </PillGroup>
          <Button asChild size="xs" variant="primary">
            <Link to={NEW_PERIOD_HREF}>Yangi amaliyot davri</Link>
          </Button>
        </>
      }
    />
  );
}
