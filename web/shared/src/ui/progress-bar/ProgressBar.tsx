import type { HTMLAttributes } from 'react';
import { cn } from '../cn';
import { pctColor, pctKind } from './pct-color';
import styles from './ProgressBar.module.css';

export interface ProgressBarProps extends HTMLAttributes<HTMLDivElement> {
  /** 0–100. */
  value: number;
  /** O'ngda mono `NN%` (default true). `'blank'` — matn yo'q, lekin o'rni (38px) saqlanadi (track'lar teng). */
  showValue?: boolean | 'blank';
  /** Rang qoidasini o'chirib, aniq token berish (masalan `var(--color-info-fg)`). */
  color?: string;
  /** a11y nomi. */
  label?: string;
}

export function ProgressBar({
  value,
  showValue = true,
  color,
  label,
  className,
  style,
  ...rest
}: ProgressBarProps) {
  const pct = Math.max(0, Math.min(100, Math.round(value)));
  return (
    <div
      className={cn(styles.bar, className)}
      role="progressbar"
      aria-valuemin={0}
      aria-valuemax={100}
      aria-valuenow={pct}
      aria-label={label}
      data-kind={pctKind(pct)}
      style={{ ...style, '--pct': `${pct}%`, '--pct-color': color ?? pctColor(pct) } as never}
      {...rest}
    >
      <div className={styles.track}>
        <div className={styles.fill} />
      </div>
      {showValue === 'blank' ? (
        <span className={styles.val} aria-hidden />
      ) : (
        showValue && <span className={styles.val}>{pct}%</span>
      )}
    </div>
  );
}
