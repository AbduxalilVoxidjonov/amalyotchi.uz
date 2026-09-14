import { forwardRef, type HTMLAttributes } from 'react';
import { cn } from '../cn';
import styles from './Badge.module.css';
import type { StatusKind } from './status';

export interface BadgeProps extends HTMLAttributes<HTMLSpanElement> {
  /** ok · late · bad · neu · info (SPEC-TOKENS 1.3). */
  status?: StatusKind;
  /** default 12px/3×9 · sm 11.5px · md 4×10 · lg 13px 600. */
  size?: 'default' | 'sm' | 'md' | 'lg';
}

export const Badge = forwardRef<HTMLSpanElement, BadgeProps>(function Badge(
  { status = 'neu', size = 'default', className, ...rest },
  ref,
) {
  return (
    <span
      ref={ref}
      className={cn(styles.badge, className)}
      data-status={status}
      data-size={size}
      {...rest}
    />
  );
});
