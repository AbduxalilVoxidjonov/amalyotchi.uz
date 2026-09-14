import type { HTMLAttributes, ReactNode } from 'react';
import { cn } from '@amaliyotchi/shared/ui';
import styles from './Alert.module.css';

export interface AlertProps extends Omit<HTMLAttributes<HTMLElement>, 'title'> {
  /** Uppercase sarlavha: "Diqqat talab qiladi". */
  title?: ReactNode;
  /** `AlertRow`lar yoki erkin matn. */
  children?: ReactNode;
}

/** Amber banner (SPEC-TOKENS 4.12). Faqat bitta rang varianti — dizaynda boshqasi yo'q. */
export function Alert({ title = 'Diqqat talab qiladi', children, className, ...rest }: AlertProps) {
  return (
    <section className={cn(styles.alert, className)} role="status" {...rest}>
      {title && <h2 className={styles.title}>{title}</h2>}
      {children}
    </section>
  );
}

export function AlertList({ className, ...rest }: HTMLAttributes<HTMLDivElement>) {
  return <div className={cn(styles.list, className)} {...rest} />;
}

export interface AlertRowProps extends HTMLAttributes<HTMLDivElement> {
  /** O'ngdagi tugma (Button variant="alert"). */
  action?: ReactNode;
}

export function AlertRow({ action, className, children, ...rest }: AlertRowProps) {
  return (
    <div className={cn(styles.row, className)} {...rest}>
      <div className={styles.text}>{children}</div>
      {action && <div className={styles.action}>{action}</div>}
    </div>
  );
}
