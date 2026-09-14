import type { HTMLAttributes, ReactNode } from 'react';
import { NavLink } from 'react-router-dom';
import { cn } from '@amaliyotchi/shared/ui';
import styles from './SidebarNav.module.css';

export interface SidebarNavItem {
  label: string;
  /** Marshrut (`NavLink to`). */
  to: string;
  /** Mono count chip; bo'sh/undefined → ko'rinmaydi. */
  badge?: string | number | undefined;
  /** `NavLink end` — faqat aynan shu yo'lda aktiv (index sahifalar uchun). */
  end?: boolean;
  icon?: ReactNode;
}

export interface SidebarNavProps extends Omit<HTMLAttributes<HTMLElement>, 'children'> {
  items: readonly SidebarNavItem[];
  /** Har havola bosilganda (drawer'ni yopish uchun). */
  onNavigate?: (() => void) | undefined;
}

/** Sidebar nav ro'yxati. Aktiv holat — `NavLink` → `aria-current="page"`. */
export function SidebarNav({ items, onNavigate, className, ...rest }: SidebarNavProps) {
  return (
    <nav className={cn(styles.nav, className)} aria-label="Asosiy" {...rest}>
      {items.map((it) => (
        <NavLink
          key={it.to}
          to={it.to}
          end={it.end ?? false}
          className={styles.item ?? ''}
          onClick={onNavigate}
        >
          <span className={styles.label}>
            {it.icon}
            {it.label}
          </span>
          <span className={styles.badge}>
            {it.badge === undefined || it.badge === '' ? null : String(it.badge)}
          </span>
        </NavLink>
      ))}
    </nav>
  );
}
