import { useMemo, useState } from 'react';
import {
  Button,
  Card,
  CardBody,
  CardHeader,
  EmptyState,
  Input,
  Pill,
  PillGroup,
} from '@/shared/ui';
import { monthLabel, todayInTashkent } from '../../format';
import { QueryState } from '../../components/QueryState';
import { useStudentAttendanceQuery, useStudentDiariesQuery } from '../hooks';
import {
  compareMonth,
  defaultPeriodMonth,
  periodMonthRange,
  periodPhase,
  plannedPeriodText,
  stepMonthWithin,
  type MonthCursor,
} from '../periods';
import type {
  AttendanceRange,
  StudentApiArea,
  StudentPeriodOption,
  TutorStudentDetail,
} from '../types';
import { AttendanceDayTable } from './AttendanceDayTable';
import styles from './StudentAttendanceSection.module.css';

type Mode = 'all' | 'month' | 'range';

/** "2026-10" oyining birinchi va oxirgi kuni (DateOnly). */
function monthBounds({ year, month }: MonthCursor): AttendanceRange {
  const mm = String(month).padStart(2, '0');
  const lastDay = new Date(Date.UTC(year, month, 0)).getUTCDate();
  return { from: `${year}-${mm}-01`, to: `${year}-${mm}-${String(lastDay).padStart(2, '0')}` };
}

interface FilterState {
  /** Holat qaysi davr uchun — davr almashsa filtrlar shu davrga qayta o'rnatiladi. */
  periodId: string | null;
  mode: Mode;
  cursor: MonthCursor;
  from: string;
  to: string;
}

function initialFilters(period: StudentPeriodOption | null, today: string): FilterState {
  const cursor = period
    ? defaultPeriodMonth(period, today)
    : { year: Number(today.slice(0, 4)), month: Number(today.slice(5, 7)) };
  return { periodId: period?.id ?? null, mode: 'all', cursor, from: '', to: '' };
}

/**
 * "Kundalik jadval" bo'limi: oy bo'yicha yoki sana oralig'i bilan filtrlash + kunlar jadvali.
 * Qator bosilsa — o'sha kun tafsiloti (lokatsiya, rasm, yuborgan kundaligi).
 * `area` — profil qaysi rol endpoint'idan o'qiyotgani (tyutor yoki admin).
 * `period` — tanlangan davr (v3.5): so'rovlar `periodId` bilan ketadi, oy navigatsiyasi
 * davr chegarasidan chiqmaydi, davr almashsa kalendar davrning oxirgi faol oyiga o'tadi.
 */
export function StudentAttendanceSection({
  detail,
  period,
  area = 'tutor',
}: {
  detail: TutorStudentDetail;
  period: StudentPeriodOption | null;
  area?: StudentApiArea;
}) {
  const today = todayInTashkent();
  const phase = period ? periodPhase(period, today) : null;

  if (!period || phase === 'planned') {
    return (
      <Card as="section" aria-label="Kundalik jadval">
        <CardHeader title="Kundalik jadval" subtitle={period?.name} />
        <CardBody>
          <EmptyState
            title={period ? plannedPeriodText(period) : "Amaliyot davri yo'q"}
            description={
              period
                ? "Davomat va kundaliklar davr boshlangach shu yerda ko'rinadi."
                : 'Talabaga hali amaliyot davri biriktirilmagan.'
            }
          />
        </CardBody>
      </Card>
    );
  }

  return <AttendanceFilters detail={detail} period={period} area={area} today={today} />;
}

function AttendanceFilters({
  detail,
  period,
  area,
  today,
}: {
  detail: TutorStudentDetail;
  period: StudentPeriodOption;
  area: StudentApiArea;
  today: string;
}) {
  const [filters, setFilters] = useState<FilterState>(() => initialFilters(period, today));
  // Davr almashdi — render paytida qayta o'rnatamiz (effect'siz, eski davr oyi bir kadr ham so'ralmaydi).
  const current = filters.periodId === period.id ? filters : initialFilters(period, today);
  if (current !== filters) setFilters(current);
  const { mode, cursor, from, to } = current;
  const update = (patch: Partial<FilterState>) => setFilters((f) => ({ ...f, ...patch }));

  const range: AttendanceRange = useMemo(() => {
    if (mode === 'month') return monthBounds(cursor);
    if (mode === 'range') return { from: from || null, to: to || null };
    return { from: null, to: null };
  }, [mode, cursor, from, to]);

  const query = useStudentAttendanceQuery(detail.id, range, area, period.id);
  // Kundaliklar bo'limi bilan bir xil kalit — TanStack so'rovni takrorlamaydi.
  const diaries = useStudentDiariesQuery(detail.id, area, period.id);

  const { first, last } = periodMonthRange(period);
  const atFirst = compareMonth(cursor, first) <= 0;
  const atLast = compareMonth(cursor, last) >= 0;

  function stepMonth(delta: number) {
    setFilters((f) => ({ ...f, mode: 'month', cursor: stepMonthWithin(f.cursor, delta, period) }));
  }

  return (
    <Card as="section" aria-label="Kundalik jadval">
      <CardHeader
        title="Kundalik jadval"
        subtitle={
          mode === 'all'
            ? 'Butun amaliyot davri'
            : mode === 'month'
              ? monthLabel(cursor.year, cursor.month)
              : 'Tanlangan sana oralig‘i'
        }
      />
      <CardBody aria-busy={query.isPlaceholderData || undefined}>
        <div className={styles.filters}>
          <PillGroup>
            <Pill active={mode === 'all'} onClick={() => update({ mode: 'all' })}>
              Butun davr
            </Pill>
            <Pill active={mode === 'month'} onClick={() => update({ mode: 'month' })}>
              {monthLabel(cursor.year, cursor.month)}
            </Pill>
          </PillGroup>
          <div className={styles.stepper}>
            <Button
              size="sm"
              aria-label="Oldingi oy"
              disabled={atFirst}
              onClick={() => stepMonth(-1)}
            >
              ‹
            </Button>
            <Button
              size="sm"
              aria-label="Keyingi oy"
              disabled={atLast}
              onClick={() => stepMonth(1)}
            >
              ›
            </Button>
          </div>
          <div className={styles.dates}>
            <Input
              id={`att-from-${detail.id}`}
              type="date"
              variant="form"
              mono
              label="Dan"
              value={from}
              min={period.startDate}
              max={period.endDate}
              wrapperClassName={styles.date}
              onChange={(e) => update({ from: e.target.value, mode: 'range' })}
            />
            <Input
              id={`att-to-${detail.id}`}
              type="date"
              variant="form"
              mono
              label="Gacha"
              value={to}
              min={period.startDate}
              max={period.endDate}
              wrapperClassName={styles.date}
              onChange={(e) => update({ to: e.target.value, mode: 'range' })}
            />
          </div>
        </div>

        <QueryState
          status={query.status}
          data={query.data}
          error={query.error}
          refetch={query.refetch}
          isEmpty={(days) => days.length === 0}
          empty={
            <p className={styles.empty} role="status">
              Tanlangan oraliqda davomat yozuvi yo'q.
            </p>
          }
        >
          {(days) => (
            <div className={query.isPlaceholderData ? styles.stale : undefined}>
              <AttendanceDayTable
                days={days}
                radiusM={detail.company?.radiusM ?? null}
                diaries={diaries.data ?? []}
                area={area}
              />
            </div>
          )}
        </QueryState>
      </CardBody>
    </Card>
  );
}
