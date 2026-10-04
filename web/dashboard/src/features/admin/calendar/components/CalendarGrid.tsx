import type { CSSProperties } from 'react';
import { Button, Card } from '@/shared/ui';
import {
  DAY_CODES,
  DAY_CODE_META,
  DAY_STATUS_CODE,
  DAY_STATUS_LABEL,
  type CalendarRow,
} from '../types';
import styles from './CalendarGrid.module.css';

export interface CalendarGridProps {
  title: string;
  prevLabel: string;
  nextLabel: string;
  onPrev: () => void;
  onNext: () => void;
  days: readonly number[];
  rows: readonly CalendarRow[];
}

/** SPEC-SCREENS §10 — oy grid (200px + N ustun), katak kodlari SPEC-TOKENS 1.5, legend. */
export function CalendarGrid({
  title,
  prevLabel,
  nextLabel,
  onPrev,
  onNext,
  days,
  rows,
}: CalendarGridProps) {
  const gridStyle = { '--cal-cols': days.length } as CSSProperties;
  return (
    <Card aria-label="Davomat kalendari">
      <div className={styles.head}>
        <h2 className={styles.title}>{title}</h2>
        <div className={styles.nav}>
          <Button size="xs" onClick={onPrev}>
            ‹ {prevLabel}
          </Button>
          <Button size="xs" onClick={onNext}>
            {nextLabel} ›
          </Button>
        </div>
      </div>
      <div className={styles.scroll}>
        <div className={styles.dayHead} style={gridStyle} role="row">
          <div />
          {days.map((d) => (
            <div key={d} role="columnheader">
              {d}
            </div>
          ))}
        </div>
        {rows.length === 0 ? (
          <div className={styles.empty}>Bu oy uchun yozuvlar yo'q</div>
        ) : (
          rows.map((r) => (
            <div key={r.studentId} className={styles.row} style={gridStyle} role="row">
              <div className={styles.name} role="rowheader">
                {r.name}
              </div>
              {days.map((d, i) => {
                const status = r.days[i] ?? 'future';
                const code = DAY_STATUS_CODE[status];
                const label = DAY_STATUS_LABEL[status];
                return (
                  <div
                    key={d}
                    className={styles.cell}
                    data-code={code}
                    data-status={status}
                    role="cell"
                    title={`${d} — ${label}`}
                    aria-label={label}
                  >
                    {DAY_CODE_META[code].glyph}
                  </div>
                );
              })}
            </div>
          ))
        )}
      </div>
      <div className={styles.legend} aria-label="Belgilar">
        {DAY_CODES.map((c) => (
          <span key={c} className={styles.legendItem}>
            <span className={styles.legendSwatch} data-code={c} aria-hidden />
            {DAY_CODE_META[c].label}
          </span>
        ))}
      </div>
    </Card>
  );
}
