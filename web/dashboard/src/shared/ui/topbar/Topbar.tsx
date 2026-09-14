import type { HTMLAttributes, ReactNode } from 'react';
import { cn } from '@amaliyotchi/shared/ui';
import styles from './Topbar.module.css';

export interface TopbarProps extends Omit<HTMLAttributes<HTMLElement>, 'title'> {
  /** Uppercase kichik matn: "Admin · TDIU · 2026-2027". */
  crumb?: ReactNode;
  /** h1 25px. */
  title: ReactNode;
  /** Mono sana chipi: "12.10.2026 · Chorshanba · 09:47". */
  clock?: ReactNode;
  /** O'ng tomondagi tugmalar (Eksport ...). */
  actions?: ReactNode;
  /** ❓ Mobil: hamburger tugmasi (≤900px). Berilmasa ko'rsatilmaydi. */
  onMenuClick?: (() => void) | undefined;
  menuOpen?: boolean;
}

/** Sticky sahifa sarlavhasi (SPEC-TOKENS 4.11). `PageHeader` — shu komponentning taxallusi. */
export function Topbar({
  crumb,
  title,
  clock,
  actions,
  onMenuClick,
  menuOpen = false,
  className,
  ...rest
}: TopbarProps) {
  return (
    <header className={cn(styles.topbar, className)} {...rest}>
      <div className={styles.left}>
        {onMenuClick && (
          <button
            type="button"
            className={styles.menu}
            onClick={onMenuClick}
            aria-label={menuOpen ? 'Menyuni yopish' : 'Menyuni ochish'}
            aria-expanded={menuOpen}
            aria-controls="app-sidebar"
          >
            <MenuIcon />
          </button>
        )}
        <div className={styles.titles}>
          {crumb && <div className={styles.crumb}>{crumb}</div>}
          <h1 className={styles.title}>{title}</h1>
        </div>
      </div>
      {(clock || actions) && (
        <div className={styles.right}>
          {clock && <span className={styles.clock}>{clock}</span>}
          {actions}
        </div>
      )}
    </header>
  );
}

export const PageHeader = Topbar;

function MenuIcon() {
  return (
    <svg width="16" height="16" viewBox="0 0 16 16" aria-hidden="true">
      <path
        d="M2 4h12M2 8h12M2 12h12"
        stroke="currentColor"
        strokeWidth="1.6"
        strokeLinecap="round"
      />
    </svg>
  );
}
