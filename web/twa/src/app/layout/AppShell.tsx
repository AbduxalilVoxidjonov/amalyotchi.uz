import { Suspense, useEffect } from 'react';
import { Outlet, useLocation } from 'react-router-dom';
import { Avatar, LoadingState } from '@/shared/ui';
import { useAuthStore } from '@/shared/auth/store';
import { requestWriteAccessOnce } from '@/shared/auth/telegram';
import { crumbFor, navItemForPath, pageTitleForPath } from '../nav';
import styles from './AppShell.module.css';
import { TabBar } from './TabBar';

/**
 * Mobil shell (❓ dizayndagi desktop sidebar+header o'rniga):
 *   sticky header (crumb + h1 + Avatar) · bitta ustun kontent · pastki tab-bar.
 * Avatar — faqat ko'rinish (havola emas); profilga pastki "Profil" tabi orqali kiriladi.
 * Sarlavha nav'dan avtomatik (SPEC-NAV 6: title = activeNav.label).
 */
export function AppShell({ locked = false }: { locked?: boolean }) {
  const { pathname } = useLocation();
  const user = useAuthStore((s) => s.user);
  const nav = navItemForPath(pathname);
  const title = nav?.label ?? pageTitleForPath(pathname) ?? 'Amaliyotchi';

  // Sessiya bor (shell faqat kirgandan keyin chiziladi) — bot xabar yubora olishi uchun ruxsat so'rovi
  // (bir marta, Telegram 6.9+ da, ruxsat hali berilmagan bo'lsa).
  useEffect(() => {
    requestWriteAccessOnce();
  }, []);

  useEffect(() => {
    document.title = title === 'Amaliyotchi' ? title : `${title} · Amaliyotchi`;
  }, [title]);

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
      {/* Yuz darvozasi: etalon yuborilmaguncha boshqa bo'limlarga o'tilmaydi. */}
      {!locked && <TabBar />}
    </div>
  );
}
