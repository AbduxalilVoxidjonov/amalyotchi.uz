import { AppNavLink } from '@/shared/ui';
import { STUDENT_NAV } from '../nav';
import { Icon } from './icons';
import styles from './TabBar.module.css';

/**
 * Pastki tab-bar (❓ dizaynda sidebar) — 5 ta to'g'ridan-to'g'ri tab, "Yana" yo'q.
 * Aktiv holat: `aria-current="page"`. Telegram rejimida `href`siz tugma (`AppNavLink`).
 */
export function TabBar() {
  return (
    <nav className={styles.bar} aria-label="Bo'limlar">
      {STUDENT_NAV.map((n) => (
        <AppNavLink
          key={n.to}
          to={n.to}
          end={n.to === '/'}
          className={styles.tab ?? ''}
          aria-label={n.label}
          title={n.label}
        >
          <Icon name={n.icon} />
          <span className={styles.label}>{n.short}</span>
        </AppNavLink>
      ))}
    </nav>
  );
}
