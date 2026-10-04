import { useCallback, useEffect, useMemo, useState } from 'react';
import { Outlet, useLocation } from 'react-router-dom';
import { useLogout } from '@/features/auth/hooks';
import { homePathForRole, useAuth } from '@/shared/auth/useAuth';
import { Topbar } from '@/shared/ui';
import { adminNav, roleLabel, titleForPath } from '../nav';
import { navBadges, navCrumb, useNavData } from '../nav-data';
import styles from './AppShell.module.css';
import { PageHeaderContext, type PageHeaderOverride } from './page-header-context';
import { Sidebar } from './Sidebar';
import { useClock } from './useClock';

/**
 * Kirgan foydalanuvchi qobig'i (SPEC-SCREENS §2): sidebar + sticky Topbar + content.
 * `RequireRole` ichida layout route sifatida ishlatiladi; sahifalar `<Outlet/>` orqali.
 * Sahifa sarlavhasi/amallari: `usePageHeader()`.
 */
export function AppShell() {
  const { user, isSuperAdmin } = useAuth();
  const location = useLocation();
  const logout = useLogout();
  const clock = useClock();
  const [override, setOverride] = useState<PageHeaderOverride>({});
  const [menuOpen, setMenuOpen] = useState(false);
  const [actionsEl, setActionsEl] = useState<HTMLElement | null>(null);

  // Badge/crumb — real API; yuklanish yoki xatoda badge yo'q, crumb faqat "Admin".
  const nav = useNavData();
  const items = useMemo(
    () => adminNav(nav.data ? navBadges(nav.data) : undefined, { isSuperAdmin }),
    [nav.data, isSuperAdmin],
  );
  const title = override.title ?? titleForPath(items, location.pathname);
  const crumb = override.crumb ?? navCrumb(nav.data);

  const closeMenu = useCallback(() => setMenuOpen(false), []);
  useEffect(() => {
    // Marshrut o'zgarganda drawer yopiladi.
    setMenuOpen(false);
  }, [location.pathname]);

  const ctx = useMemo(() => ({ override, setOverride, actionsEl }), [override, actionsEl]);

  return (
    <PageHeaderContext.Provider value={ctx}>
      <div className={styles.root}>
        <Sidebar
          items={items}
          userName={user?.fullName ?? ''}
          userRole={roleLabel(isSuperAdmin)}
          homePath={homePathForRole(user?.role)}
          onLogout={() => logout.mutate()}
          logoutPending={logout.isPending}
          open={menuOpen}
          onClose={closeMenu}
        />
        <div className={styles.backdrop} data-open={menuOpen || undefined} onClick={closeMenu} />
        <main className={styles.main}>
          <Topbar
            crumb={crumb}
            title={title}
            clock={clock}
            actions={<div ref={setActionsEl} className={styles.actionsSlot} />}
            onMenuClick={() => setMenuOpen((v) => !v)}
            menuOpen={menuOpen}
          />
          <div className={styles.content}>
            <Outlet />
          </div>
        </main>
      </div>
    </PageHeaderContext.Provider>
  );
}

export default AppShell;
