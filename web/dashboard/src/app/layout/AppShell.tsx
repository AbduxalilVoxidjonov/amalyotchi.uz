import { useEffect, useMemo, useState } from 'react';
import { Outlet, useLocation } from 'react-router-dom';
import { useLogout } from '@/features/auth/hooks';
import { homePathForRole, useAuth } from '@/shared/auth/useAuth';
import { Topbar } from '@/shared/ui';
import { adminNavGroups, roleLabel, titleForPath } from '../nav';
import { navBadges, navCrumb, useNavData } from '../nav-data';
import styles from './AppShell.module.css';
import { PageHeaderContext, type PageHeaderOverride } from './page-header-context';
import { Sidebar } from './Sidebar';
import { useSidebarOpen } from './useSidebarOpen';
import { useClock } from './useClock';

/**
 * Kirgan foydalanuvchi qobig'i (SPEC-SCREENS §2): sidebar (☰ bilan ochiladi/yopiladi) + sticky Topbar + content.
 * `RequireRole` ichida layout route sifatida ishlatiladi; sahifalar `<Outlet/>` orqali.
 * Sahifa sarlavhasi/amallari: `usePageHeader()`.
 */
export function AppShell() {
  const { user, isSuperAdmin } = useAuth();
  const location = useLocation();
  const logout = useLogout();
  const clock = useClock();
  const [override, setOverride] = useState<PageHeaderOverride>({});
  const [actionsEl, setActionsEl] = useState<HTMLElement | null>(null);

  // Badge/crumb — real API; yuklanish yoki xatoda badge yo'q, crumb faqat "Admin".
  const nav = useNavData();
  const groups = useMemo(
    () => adminNavGroups(nav.data ? navBadges(nav.data) : undefined, { isSuperAdmin }),
    [nav.data, isSuperAdmin],
  );
  const items = useMemo(() => groups.flatMap((g) => g.items), [groups]);
  const title = override.title ?? titleForPath(items, location.pathname);
  const crumb = override.crumb ?? navCrumb(nav.data);

  // ☰ — desktop'da ochib-yopadi (sahifalar orasida saqlanadi), tor ekranda drawer.
  const sidebar = useSidebarOpen(location.pathname);
  const menuOpen = sidebar.isOpen;
  const drawerOpen = sidebar.drawer && menuOpen;
  const closeDrawer = sidebar.close;
  useEffect(() => {
    // Esc — ochiq drawer'ni yopadi (qidiruv maydoni Esc'ni o'zi ushlaydi).
    if (!drawerOpen) return;
    const onKey = (e: KeyboardEvent) => {
      if (e.key === 'Escape' && !e.defaultPrevented) closeDrawer();
    };
    document.addEventListener('keydown', onKey);
    return () => document.removeEventListener('keydown', onKey);
  }, [drawerOpen, closeDrawer]);

  const ctx = useMemo(() => ({ override, setOverride, actionsEl }), [override, actionsEl]);

  return (
    <PageHeaderContext.Provider value={ctx}>
      <div className={styles.root}>
        <Sidebar
          groups={groups}
          userName={user?.fullName ?? ''}
          userRole={roleLabel(isSuperAdmin)}
          homePath={homePathForRole(user?.role)}
          onLogout={() => logout.mutate()}
          logoutPending={logout.isPending}
          open={menuOpen}
          onClose={sidebar.drawer ? sidebar.close : undefined}
          onSearchShortcut={sidebar.open}
        />
        <div
          className={styles.backdrop}
          data-open={drawerOpen || undefined}
          onClick={sidebar.close}
        />
        <main className={styles.main}>
          <Topbar
            crumb={crumb}
            title={title}
            clock={clock}
            actions={<div ref={setActionsEl} className={styles.actionsSlot} />}
            onMenuClick={sidebar.toggle}
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
