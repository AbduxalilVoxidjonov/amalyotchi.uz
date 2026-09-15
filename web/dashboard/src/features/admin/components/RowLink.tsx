import type { ReactNode } from 'react';
import { Link } from 'react-router-dom';
import styles from './RowLink.module.css';

export interface RowLinkProps {
  to: string;
  children: ReactNode;
}

/**
 * Jadval katagida "ichiga kiradigan" havola (masalan fakultet nomi → kafedralar ro'yxati).
 * Rangi/qalinligi `[data-strong]` katagidan meros — faqat fokus/hover holatlari qo'shiladi.
 */
export function RowLink({ to, children }: RowLinkProps) {
  return (
    <Link to={to} className={styles.link}>
      {children}
    </Link>
  );
}
