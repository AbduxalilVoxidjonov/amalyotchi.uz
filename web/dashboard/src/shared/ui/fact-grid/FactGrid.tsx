import type { CSSProperties, HTMLAttributes, ReactNode } from 'react';
import { cn } from '@amaliyotchi/shared/ui';
import styles from './FactGrid.module.css';

export interface FactItem {
  k: ReactNode;
  v: ReactNode;
}

export interface FactGridProps extends HTMLAttributes<HTMLDListElement> {
  items: readonly FactItem[];
  /** `2` → 1fr 1fr; `auto` (default) → auto-fit minmax(min). */
  columns?: 2 | 'auto';
  /** auto-fit uchun min kenglik: 180 (detail) · 120 (portfolio). */
  min?: number;
  /** mono (checkinFacts, default) · detail (sans 13.5) · portfolio (mono 19px 600). */
  variant?: 'mono' | 'detail' | 'portfolio';
}

/** 1px-gap "fakt" katakchalari (SPEC-TOKENS 4.7 FactGrid). Semantik: `<dl>`. */
export function FactGrid({
  items,
  columns = 'auto',
  min = 180,
  variant = 'mono',
  className,
  style,
  ...rest
}: FactGridProps) {
  return (
    <dl
      className={cn(styles.facts, className)}
      data-columns={columns}
      data-variant={variant}
      style={{ ...style, '--fact-min': `${min}px` } as CSSProperties}
      {...rest}
    >
      {items.map((it, i) => (
        <div key={i} className={styles.item}>
          <dt className={styles.k}>{it.k}</dt>
          <dd className={styles.v}>{it.v}</dd>
        </div>
      ))}
    </dl>
  );
}
