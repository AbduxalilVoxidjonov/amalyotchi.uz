import { forwardRef, type ButtonHTMLAttributes, type HTMLAttributes, type ReactNode } from 'react';
import { cn } from '@amaliyotchi/shared/ui';
import styles from './Pill.module.css';

export type PillShape = 'round' | 'square' | 'tab' | 'wide';

export interface PillProps extends ButtonHTMLAttributes<HTMLButtonElement> {
  /** Aktiv holat → `aria-pressed` (yoki `role="tab"` bo'lsa `aria-selected`). */
  active?: boolean;
  /** round (dayFilters, r20) · square (views, r6) · tab (appTabs, 13px) · wide (login rollar, flex:1). */
  shape?: PillShape;
  /** appTabs: o'ngdagi mono count. */
  count?: ReactNode;
}

export const Pill = forwardRef<HTMLButtonElement, PillProps>(function Pill(
  { active = false, shape = 'round', count, className, type = 'button', children, role, ...rest },
  ref,
) {
  const pressedProps = role === 'tab' ? { 'aria-selected': active } : { 'aria-pressed': active };
  return (
    <button
      ref={ref}
      type={type}
      role={role}
      className={cn(styles.pill, className)}
      data-shape={shape}
      {...pressedProps}
      {...rest}
    >
      {children}
      {count !== undefined && count !== null && count !== '' && (
        <span className={styles.count}>{count}</span>
      )}
    </button>
  );
});

export interface PillGroupProps extends HTMLAttributes<HTMLDivElement> {
  /** Har pill teng kenglikda (login rollar). */
  stretch?: boolean;
}

/** Pill'lar qatori: gap 6px, wrap. `role="tablist"` berish mumkin. */
export function PillGroup({ stretch = false, className, ...rest }: PillGroupProps) {
  return (
    <div className={cn(styles.group, className)} data-stretch={stretch || undefined} {...rest} />
  );
}
