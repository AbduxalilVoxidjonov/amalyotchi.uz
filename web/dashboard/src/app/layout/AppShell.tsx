import { useCallback, useEffect, useMemo, useState } from 'react';
import { Outlet, useLocation } from 'react-router-dom';
import { UserRole } from '@amaliyotchi/shared/auth';
import { useLogout } from '@/features/auth/hooks';
import { homePathForRole, useAuth } from '@/shared/auth/useAuth';
import { Topbar } from '@/shared/ui';
import { navForRole, ROLE_LABEL, titleForPath, type NavRole } from '../nav';
import { navBadges, navCrumb, useNavData } from '../nav-data';
import styles from './AppShell.module.css';
import { PageHeaderContext, type PageHeaderOverride } from './page-header-context';
import { Sidebar } from './Sidebar';
import { useClock } from './useClock';

function isNavRole(role: UserRole | undefined): role is NavRole {
  return role === UserRole.Admin || role === UserRole.Tutor;
}

/**
 * Kirgan foydalanuvchi qobig'i (SPEC-SCREENS §2): sidebar + sticky Topbar + content.
 * `RequireRole` ichida layout route sifatida ishlatiladi; sahifalar `<Outlet/>` orqali.
 * Sahifa sarlavhasi/amallari: `usePageHeader()`.
 */
export function AppShell() {
  const { user } = useAuth();
  const location = useLocation();
  const logout = useLogout();
  const clock = useClock();
  const [override, setOverride] = useState<PageHeaderOverride>({});
  const [menuOpen, setMenuOpen] = useState(false);
  const [actionsEl, setActionsEl] = useState<HTMLElement | null>(null);

  const role: NavRole = isNavRole(user?.role) ? user.role : UserRole.Tutor;
  // Badge/crumb — real API; yuklanish yoki xatoda badge yo'q, crumb faqat rol nomi.
  const nav = useNavData(role);
  const items = useMemo(
    () => navForRole(role, nav.data ? navBadges(nav.data) : undefined),
    [role, nav.data],
  );
  const title = override.title ?? titleForPath(items, location.pathname);
  const crumb = override.crumb ?? navCrumb(role, nav.data);

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
          userRole={ROLE_LABEL[role]}
          homePath={homePathForRole(role)}
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
