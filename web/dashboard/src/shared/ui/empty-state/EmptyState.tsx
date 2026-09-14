import type { HTMLAttributes, ReactNode } from 'react';
import { cn } from '@amaliyotchi/shared/ui';
import styles from './EmptyState.module.css';

export interface EmptyStateProps extends Omit<HTMLAttributes<HTMLDivElement>, 'title'> {
  title: ReactNode;
  description?: ReactNode;
  action?: ReactNode;
  /** dashed (default) · plain (oq kartochka). */
  tone?: 'dashed' | 'plain';
}

export function EmptyState({
  title,
  description,
  action,
  tone = 'dashed',
  className,
  ...rest
}: EmptyStateProps) {
  return (
    <div className={cn(styles.empty, className)} data-tone={tone} {...rest}>
      <div className={styles.title}>{title}</div>
      {description && <div className={styles.desc}>{description}</div>}
      {action && <div className={styles.action}>{action}</div>}
    </div>
  );
}
