import { useMemo, useState } from 'react';
import { Button, Card, CardBody, CardHeader, Input, Pill, PillGroup } from '@/shared/ui';
import { monthLabel, shiftMonth, todayInTashkent } from '../../format';
import { QueryState } from '../../components/QueryState';
import { useStudentAttendanceQuery } from '../hooks';
import type { AttendanceRange, TutorStudentDetail } from '../types';
import { AttendanceDayTable } from './AttendanceDayTable';
import styles from './StudentAttendanceSection.module.css';

type Mode = 'all' | 'month' | 'range';

interface MonthCursor {
  year: number;
  month: number;
}

/** "2026-10" oyining birinchi va oxirgi kuni (DateOnly). */
function monthBounds({ year, month }: MonthCursor): AttendanceRange {
  const mm = String(month).padStart(2, '0');
  const lastDay = new Date(Date.UTC(year, month, 0)).getUTCDate();
  return { from: `${year}-${mm}-01`, to: `${year}-${mm}-${String(lastDay).padStart(2, '0')}` };
}

function defaultCursor(detail: TutorStudentDetail): MonthCursor {
  const today = todayInTashkent();
  const end = detail.period && detail.period.endDate < today ? detail.period.endDate : today;
  return { year: Number(end.slice(0, 4)), month: Number(end.slice(5, 7)) };
}

/** Kun-bakun davomat bo'limi: oy bo'yicha yoki sana oralig'i bilan filtrlash + jadval. */
export function StudentAttendanceSection({ detail }: { detail: TutorStudentDetail }) {
  const [mode, setMode] = useState<Mode>('all');
  const [cursor, setCursor] = useState<MonthCursor>(() => defaultCursor(detail));
  const [from, setFrom] = useState('');
  const [to, setTo] = useState('');

  const range: AttendanceRange = useMemo(() => {
    if (mode === 'month') return monthBounds(cursor);
    if (mode === 'range') return { from: from || null, to: to || null };
    return { from: null, to: null };
  }, [mode, cursor, from, to]);

  const query = useStudentAttendanceQuery(detail.id, range);

  function stepMonth(delta: number) {
    setMode('month');
    setCursor((c) => shiftMonth(c.year, c.month, delta));
  }

  return (
    <Card as="section" aria-label="Kun-bakun davomat">
      <CardHeader
        title="Kun-bakun davomat"
        subtitle={
          mode === 'all'
            ? 'Butun amaliyot davri'
            : mode === 'month'
              ? monthLabel(cursor.year, cursor.month)
              : 'Tanlangan sana oralig‘i'
        }
      />
      <CardBody>
        <div className={styles.filters}>
          <PillGroup>
            <Pill active={mode === 'all'} onClick={() => setMode('all')}>
              Butun davr
            </Pill>
            <Pill active={mode === 'month'} onClick={() => setMode('month')}>
              {monthLabel(cursor.year, cursor.month)}
            </Pill>
          </PillGroup>
          <div className={styles.stepper}>
            <Button size="sm" aria-label="Oldingi oy" onClick={() => stepMonth(-1)}>
              ‹
            </Button>
            <Button size="sm" aria-label="Keyingi oy" onClick={() => stepMonth(1)}>
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
              wrapperClassName={styles.date}
              onChange={(e) => {
                setFrom(e.target.value);
                setMode('range');
              }}
            />
            <Input
              id={`att-to-${detail.id}`}
              type="date"
              variant="form"
              mono
              label="Gacha"
              value={to}
              wrapperClassName={styles.date}
              onChange={(e) => {
                setTo(e.target.value);
                setMode('range');
              }}
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
          {(days) => <AttendanceDayTable days={days} radiusM={detail.company?.radiusM ?? null} />}
        </QueryState>
      </CardBody>
    </Card>
  );
}
