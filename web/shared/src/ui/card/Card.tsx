import { forwardRef, type HTMLAttributes, type ReactNode } from 'react';
import { cn } from '../cn';
import styles from './Card.module.css';

export interface CardProps extends HTMLAttributes<HTMLElement> {
  /** `true` → 18px padding (article); `lg` → 22px + r12; `form` → 20px. */
  padded?: boolean | 'lg' | 'form';
  /** Semantik teg (default `section`). */
  as?: 'section' | 'article' | 'div';
}

export const Card = forwardRef<HTMLElement, CardProps>(function Card(
  { padded = false, as: Tag = 'section', className, ...rest },
  ref,
) {
  return (
    <Tag
      // forwardRef<HTMLElement> — barcha uch teg HTMLElement ga mos
      ref={ref as never}
      className={cn(styles.card, className)}
      data-padded={padded === true ? 'true' : padded || undefined}
      {...rest}
    />
  );
});

export interface CardHeaderProps extends Omit<HTMLAttributes<HTMLDivElement>, 'title'> {
  title: ReactNode;
  subtitle?: ReactNode;
  actions?: ReactNode;
  /** Sarlavha darajasi (a11y): default h2. */
  level?: 2 | 3 | 4;
}

export function CardHeader({
  title,
  subtitle,
  actions,
  level = 2,
  className,
  ...rest
}: CardHeaderProps) {
  const Heading = `h${level}` as const;
  return (
    <div className={cn(styles.head, className)} {...rest}>
      <div className={styles.headTitle}>
        <Heading className={styles.headTitle}>{title}</Heading>
        {subtitle && <div className={styles.headSub}>{subtitle}</div>}
      </div>
      {actions && <div className={styles.headActions}>{actions}</div>}
    </div>
  );
}

export function CardBody({ className, ...rest }: HTMLAttributes<HTMLDivElement>) {
  return <div className={cn(styles.body, className)} {...rest} />;
}

/** Ro'yxat qatori (settings, place fields): 12px 18px, pastki chiziq. */
export function CardRow({ className, ...rest }: HTMLAttributes<HTMLDivElement>) {
  return <div className={cn(styles.row, className)} {...rest} />;
}

export function CardFooter({ className, ...rest }: HTMLAttributes<HTMLDivElement>) {
  return <div className={cn(styles.foot, className)} {...rest} />;
}
