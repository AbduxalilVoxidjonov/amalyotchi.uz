import type { CSSProperties, HTMLAttributes, ReactNode } from 'react';
import { cn } from '@amaliyotchi/shared/ui';
import styles from './FactGrid.module.css';

export interface FactItem {
  k: ReactNode;
  v: ReactNode;
  /** Qiymat rangi (token): masalan `var(--color-late-fg)`. */
  tone?: 'default' | 'ok' | 'late' | 'bad';
}

export interface FactGridProps extends HTMLAttributes<HTMLDListElement> {
  items: readonly FactItem[];
  /** `2` → 1fr 1fr (checkinFacts); `auto` → auto-fit minmax(min). */
  columns?: 2 | 3 | 'auto';
  /** auto-fit uchun min kenglik: 120 (portfolio). */
  min?: number;
  /** mono (checkinFacts, default) · portfolio (mono 19px 600, k 11px). */
  variant?: 'mono' | 'portfolio';
}

/** 1px-gap "fakt" katakchalari (SPEC-TOKENS 4.7 FactGrid). Semantik: `<dl>`. */
export function FactGrid({
  items,
  columns = 'auto',
  min = 120,
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
          <dd className={styles.v} data-tone={it.tone ?? 'default'}>
            {it.v}
          </dd>
        </div>
      ))}
    </dl>
  );
}
