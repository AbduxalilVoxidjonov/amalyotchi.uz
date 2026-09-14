import type { HTMLAttributes, ReactNode } from 'react';
import { Button, cn } from '@amaliyotchi/shared/ui';
import styles from './States.module.css';

interface StateBoxProps extends Omit<HTMLAttributes<HTMLDivElement>, 'title'> {
  title: ReactNode;
  description?: ReactNode;
  action?: ReactNode;
}

function StateBox({ title, description, action, className, ...rest }: StateBoxProps) {
  return (
    <div className={cn(styles.box, className)} {...rest}>
      <div className={styles.title}>{title}</div>
      {description && <div className={styles.desc}>{description}</div>}
      {action && <div className={styles.action}>{action}</div>}
    </div>
  );
}

export type EmptyStateProps = StateBoxProps;

/** ❓ SPEC-TOKENS 4.18 — dizaynda yo'q; map-ph uslubida (dashed). */
export function EmptyState(props: EmptyStateProps) {
  return <StateBox data-tone="empty" {...props} />;
}

export interface ErrorStateProps extends Omit<StateBoxProps, 'title' | 'action'> {
  title?: ReactNode;
  /** Qayta urinish tugmasi. */
  onRetry?: () => void;
}

export function ErrorState({ title = 'Yuklab bo‘lmadi', onRetry, ...rest }: ErrorStateProps) {
  return (
    <StateBox
      role="alert"
      data-tone="error"
      title={title}
      action={
        onRetry && (
          <Button size="sm" onClick={onRetry}>
            Qayta urinish
          </Button>
        )
      }
      {...rest}
    />
  );
}

export interface LoadingStateProps extends HTMLAttributes<HTMLDivElement> {
  label?: string;
  /** Skelet balandligi (px). */
  height?: number;
}

/** Yuklanish holati: skelet blok + a11y matni. */
export function LoadingState({
  label = 'Yuklanmoqda…',
  height = 160,
  className,
  ...rest
}: LoadingStateProps) {
  return (
    <div
      className={cn(styles.loading, className)}
      role="status"
      aria-live="polite"
      aria-label={label}
      style={{ minHeight: height }}
      {...rest}
    >
      <span className={styles.srOnly}>{label}</span>
    </div>
  );
}
