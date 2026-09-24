import { Suspense, useEffect } from 'react';
import { Outlet, useLocation } from 'react-router-dom';
import { Avatar, LoadingState } from '@/shared/ui';
import { useAuthStore } from '@/shared/auth/store';
import { crumbFor, navItemForPath } from '../nav';
import styles from './AppShell.module.css';
import { TabBar } from './TabBar';

/**
 * Mobil shell (❓ dizayndagi desktop sidebar+header o'rniga):
 *   sticky header (crumb + h1 + Avatar) · bitta ustun kontent · pastki tab-bar.
 * Avatar — faqat ko'rinish (havola emas); profilga pastki "Profil" tabi orqali kiriladi.
 * Sarlavha nav'dan avtomatik (SPEC-NAV 6: title = activeNav.label).
 */
export function AppShell() {
  const { pathname } = useLocation();
  const user = useAuthStore((s) => s.user);
  const nav = navItemForPath(pathname);
  const title = nav?.label ?? 'Amaliyotchi';

  useEffect(() => {
    document.title = nav ? `${nav.label} · Amaliyotchi` : 'Amaliyotchi';
  }, [nav]);

  return (
    <div className={styles.shell}>
      <header className={styles.header}>
        <div className={styles.headerText}>
          <div className={styles.crumb}>{crumbFor(user)}</div>
          <h1 className={styles.title}>{title}</h1>
        </div>
        <Avatar name={user?.fullName ?? null} variant="card" className={styles.avatar} />
      </header>
      <main className={styles.content}>
        <Suspense fallback={<LoadingState height={240} />}>
          <Outlet />
        </Suspense>
      </main>
      <TabBar />
    </div>
  );
}
