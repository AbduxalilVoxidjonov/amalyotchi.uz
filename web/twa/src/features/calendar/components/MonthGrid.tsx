import { Button, Card } from '@/shared/ui';
import {
  formatMonthLabel,
  parseDateOnly,
  shiftMonth,
  WEEKDAYS_SHORT_UZ,
} from '@/shared/lib/format';
import { DAY_STATUS, DAY_STATUS_ORDER, type CalendarMonthDto } from '../types';
import styles from './MonthGrid.module.css';

export interface MonthGridProps {
  data: CalendarMonthDto;
  /** Oy almashtirilmoqda (keepPreviousData) — grid xiraroq. */
  fetching?: boolean;
  onMonthChange: (month: string) => void;
}

/**
 * SPEC-SCREENS §10 talaba varianti — ❓ mobil uchun 12 ustunli "qator" o'rniga 7 ustunli oy gridi
 * (Du–Ya); katak rang/harfi SPEC-TOKENS 1.5 bilan bir xil, kun raqami katak ustida.
 */
export function MonthGrid({ data, fetching = false, onMonthChange }: MonthGridProps) {
  const prev = shiftMonth(data.month, -1);
  const next = shiftMonth(data.month, 1);
  const first = data.days[0] ? parseDateOnly(data.days[0].date) : null;
  // Dushanbadan boshlanadigan hafta: 0 = Du … 6 = Ya
  const offset = first
    ? (new Date(Date.UTC(first.y, first.m - 1, first.d)).getUTCDay() + 6) % 7
    : 0;

  return (
    <Card aria-label="Kalendar" data-fetching={fetching || undefined} className={styles.card}>
      <div className={styles.head}>
        <div className={styles.headText}>
          <h2 className={styles.title}>{formatMonthLabel(data.month)}</h2>
          <div className={styles.sub}>
            {data.studentName} · {data.groupName}
          </div>
        </div>
        <div className={styles.nav}>
          <Button size="xs" onClick={() => onMonthChange(prev)}>
            ‹ {formatMonthLabel(prev).split(' ')[0]}
          </Button>
          <Button size="xs" onClick={() => onMonthChange(next)}>
            {formatMonthLabel(next).split(' ')[0]} ›
          </Button>
        </div>
      </div>

      <div className={styles.weekdays} aria-hidden="true">
        {WEEKDAYS_SHORT_UZ.map((d) => (
          <div key={d}>{d}</div>
        ))}
      </div>
      <ol className={styles.grid} aria-busy={fetching || undefined}>
        {Array.from({ length: offset }, (_, i) => (
          <li key={`pad-${i}`} className={styles.pad} aria-hidden="true" />
        ))}
        {data.days.map((day) => {
          const p = parseDateOnly(day.date);
          const meta = DAY_STATUS[day.status];
          return (
            <li
              key={day.date}
              className={styles.cell}
              data-status={day.status}
              aria-label={`${p?.d ?? ''} · ${meta.label}`}
              title={meta.label}
            >
              <span className={styles.dayNum}>{p?.d}</span>
              <span className={styles.glyph}>{meta.glyph}</span>
            </li>
          );
        })}
      </ol>

      <ul className={styles.legend} aria-label="Belgilar">
        {DAY_STATUS_ORDER.map((status) => (
          <li key={status} className={styles.legendItem}>
            <span className={styles.swatch} data-status={status} aria-hidden="true" />
            {DAY_STATUS[status].label}
          </li>
        ))}
      </ul>
    </Card>
  );
}
