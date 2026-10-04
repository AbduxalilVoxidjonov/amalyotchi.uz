import type { SidebarNavItem } from '@/shared/ui';

/**
 * Admin sidebar ro'yxati (SPEC-NAV §2; v3.28 — tyutor bo'limi sahifalari adminga ko'chdi).
 * Badge sonlari — real API: `useNavData()` (`GET /api/admin/nav`) → `navBadges(data)` →
 * `adminNav(badges, { isSuperAdmin })`. Badges berilmasa (yuklanish/xato) — badge yo'q.
 */
export interface NavBadges {
  [to: string]: string | number | undefined;
}

/** "Adminlar" — faqat bosh admin uchun (`user.isSuperAdmin`). */
export const ADMINS_NAV_PATH = '/admin/admins';

/**
 * Tartib: umumiy ko'rinish → kundalik nazorat (Bugun, Arizalar, Yuz, Kundaliklar, Baholash,
 * Kalendar, Xarita) → ma'lumotnomalar (Talabalar, Korxonalar, Fakultetlar, davrlar) →
 * aloqa/hisobot → boshqaruv (Adminlar, Audit, Sozlamalar).
 */
const ADMIN_NAV: readonly SidebarNavItem[] = [
  { label: 'Dashboard', to: '/admin', end: true },
  { label: 'Bugun', to: '/admin/today' },
  { label: 'Arizalar', to: '/admin/applications' },
  { label: 'Yuz tasdiqlash', to: '/admin/face' },
  { label: 'Kundaliklar', to: '/admin/diaries' },
  { label: 'Baholash', to: '/admin/grading' },
  { label: 'Kalendar', to: '/admin/calendar' },
  { label: 'Xarita', to: '/admin/map' },
  { label: 'Talabalar', to: '/admin/students' },
  { label: 'Korxonalar', to: '/admin/companies' },
  { label: 'Fakultetlar', to: '/admin/faculties' },
  { label: 'Amaliyot davrlari', to: '/admin/practice-periods' },
  { label: 'Xabarlar', to: '/admin/messages' },
  { label: 'Hisobotlar', to: '/admin/reports' },
  { label: 'Adminlar', to: ADMINS_NAV_PATH },
  { label: 'Audit jurnali', to: '/admin/audit' },
  { label: 'Sozlamalar', to: '/admin/settings' },
];

/** SPEC-NAV §6 — title = TITLE_OVERRIDE[route] ?? nav label. */
const TITLE_OVERRIDE: Record<string, string> = {
  '/admin': 'Umumiy dashboard',
  '/admin/audit': 'Audit jurnali',
  '/admin/diaries': 'Kundalik hisobotlar',
};

/** Sidebar pastidagi rol yozuvi. */
export function roleLabel(isSuperAdmin: boolean): string {
  return isSuperAdmin ? "Bosh admin · o'quv bo'limi" : "Admin · o'quv bo'limi";
}

export interface AdminNavOptions {
  /** Bosh admin — "Adminlar" bandi ko'rinadi. */
  isSuperAdmin?: boolean;
}

/** `badges` yo'q bo'lsa — hech bir elementda badge ko'rsatilmaydi (soxta son chiqmasin). */
export function adminNav(badges?: NavBadges, options: AdminNavOptions = {}): SidebarNavItem[] {
  return ADMIN_NAV.filter((it) => it.to !== ADMINS_NAV_PATH || options.isSuperAdmin === true).map(
    (it) => ({ ...it, badge: badges?.[it.to] }),
  );
}

/** Joriy pathname uchun aktiv nav elementi (eng uzun mos prefiks). */
export function activeNavItem(
  items: readonly SidebarNavItem[],
  pathname: string,
): SidebarNavItem | undefined {
  let best: SidebarNavItem | undefined;
  for (const it of items) {
    const match = it.end
      ? pathname === it.to
      : pathname === it.to || pathname.startsWith(it.to + '/');
    if (match && (!best || it.to.length > best.to.length)) best = it;
  }
  return best;
}

export function titleForPath(items: readonly SidebarNavItem[], pathname: string): string {
  const active = activeNavItem(items, pathname);
  if (!active) return 'Amaliyotchi';
  return TITLE_OVERRIDE[active.to] ?? active.label;
}
