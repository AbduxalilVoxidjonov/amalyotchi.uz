import { useEffect, useState } from 'react';
import { NavLink, useLocation } from 'react-router-dom';
import { Avatar } from '@/shared/ui';
import { useAuthStore } from '@/shared/auth/store';
import { PRIMARY_TAB_COUNT, roleLineFor, STUDENT_NAV } from '../nav';
import { Icon } from './icons';
import styles from './TabBar.module.css';

/**
 * Pastki tab-bar (❓ dizaynda sidebar). 4 asosiy tab + "Yana" — qolgan bo'limlar va foydalanuvchi.
 * Aktiv holat: NavLink `aria-current="page"`; "Yana" ichidagi bo'lim aktiv bo'lsa "Yana" tugmasi aktiv.
 */
export function TabBar() {
  const { pathname } = useLocation();
  const [moreOpen, setMoreOpen] = useState(false);
  const user = useAuthStore((s) => s.user);

  const primary = STUDENT_NAV.slice(0, PRIMARY_TAB_COUNT);
  const more = STUDENT_NAV.slice(PRIMARY_TAB_COUNT);
  const moreActive = more.some((n) => pathname.startsWith(n.to));

  // Marshrut o'zgarganda menyu yopiladi.
  useEffect(() => {
    setMoreOpen(false);
  }, [pathname]);

  useEffect(() => {
    if (!moreOpen) return;
    const onKey = (e: KeyboardEvent) => {
      if (e.key === 'Escape') setMoreOpen(false);
    };
    window.addEventListener('keydown', onKey);
    return () => window.removeEventListener('keydown', onKey);
  }, [moreOpen]);

  return (
    <>
      {moreOpen && (
        <div className={styles.sheetBackdrop} onClick={() => setMoreOpen(false)}>
          <div
            className={styles.sheet}
            role="dialog"
            aria-modal="true"
            aria-label="Yana"
            onClick={(e) => e.stopPropagation()}
          >
            <div className={styles.sheetUser}>
              <Avatar name={user?.fullName ?? null} variant="card" />
              <div className={styles.sheetUserText}>
                <div className={styles.sheetName}>{user?.fullName ?? 'Talaba'}</div>
                <div className={styles.sheetRole}>{roleLineFor(user)}</div>
              </div>
            </div>
            <ul className={styles.sheetList}>
              {more.map((n) => (
                <li key={n.to}>
                  <NavLink to={n.to} className={styles.sheetItem ?? ''}>
                    <Icon name={n.icon} />
                    <span>{n.label}</span>
                  </NavLink>
                </li>
              ))}
            </ul>
          </div>
        </div>
      )}

      <nav className={styles.bar} aria-label="Bo'limlar">
        {primary.map((n) => (
          <NavLink
            key={n.to}
            to={n.to}
            end={n.to === '/'}
            className={styles.tab ?? ''}
            aria-label={n.label}
            title={n.label}
          >
            <Icon name={n.icon} />
            <span className={styles.label}>{n.short}</span>
          </NavLink>
        ))}
        <button
          type="button"
          className={styles.tab}
          data-active={moreActive || moreOpen ? 'true' : undefined}
          aria-expanded={moreOpen}
          aria-haspopup="dialog"
          onClick={() => setMoreOpen((v) => !v)}
        >
          <svg width="22" height="22" viewBox="0 0 24 24" fill="currentColor" aria-hidden="true">
            <circle cx="5" cy="12" r="2" />
            <circle cx="12" cy="12" r="2" />
            <circle cx="19" cy="12" r="2" />
          </svg>
          <span className={styles.label}>Yana</span>
        </button>
      </nav>
    </>
  );
}
