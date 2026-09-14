import type { CSSProperties, HTMLAttributes, ReactNode } from 'react';
import { cn, STATUS, type StatusKind } from '@amaliyotchi/shared/ui';
import styles from './StatTile.module.css';

export interface StatTileProps extends HTMLAttributes<HTMLDivElement> {
  label: ReactNode;
  /** Mono 30px 600. */
  value: ReactNode;
  note?: ReactNode;
  /** Izoh rangi (default faint). */
  noteTone?: StatusKind;
  /** Label oldidagi 7px nuqta — status rangi (todayStats). */
  dot?: StatusKind;
}

export function StatTile({
  label,
  value,
  note,
  noteTone,
  dot,
  className,
  style,
  ...rest
}: StatTileProps) {
  const dotStyle = dot ? ({ '--dot': STATUS[dot].strong } as CSSProperties) : undefined;
  return (
    <div className={cn(styles.stat, className)} style={style} {...rest}>
      <div className={styles.label}>
        {dot && <span className={styles.dot} style={dotStyle} aria-hidden />}
        {label}
      </div>
      <div className={styles.value}>{value}</div>
      {note && (
        <div className={styles.note} data-tone={noteTone}>
          {note}
        </div>
      )}
    </div>
  );
}

export interface StatGridProps extends HTMLAttributes<HTMLDivElement> {
  /** minmax(min, 1fr): 170px (tyutor, 5 ta) · 180px (admin, 4 ta). */
  min?: 170 | 180 | number;
}

export function StatGrid({ min = 170, className, style, ...rest }: StatGridProps) {
  return (
    <div
      className={cn(styles.grid, className)}
      style={{ ...style, '--stat-min': `${min}px` } as CSSProperties}
      {...rest}
    />
  );
}
