import type { CSSProperties } from 'react';
import styles from './PeriodMetrics.module.css';

/** Ko'rsatkich hali yuklanmoqda — kulrang "pulslanuvchi" chiziq (ekran o'quvchidan yashirin). */
export function MetricSkeleton({ width = 48, height = 12 }: { width?: number; height?: number }) {
  return (
    <span
      className={styles.skeleton}
      style={{ '--sk-w': `${width}px`, '--sk-h': `${height}px` } as CSSProperties}
      data-testid="metric-skeleton"
      aria-hidden
    />
  );
}
