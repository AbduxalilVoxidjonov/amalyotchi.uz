import { UserRole } from '@amaliyotchi/shared/auth';
import type { SidebarNavItem } from '@/shared/ui';

/**
 * Sidebar ro'yxatlari — SPEC-NAV.md §2 (tartib va badge'lar aynan).
 * Badge'lar hozircha MOCK (statik). Keyingi agent: `useNavCounts()` (TanStack Query) → `badges` param.
 */
export type NavRole = typeof UserRole.Admin | typeof UserRole.Tutor;

export interface NavBadges {
  [to: string]: string | number | undefined;
}

/** SPEC-NAV §2.1 — badge'lar dizayndagi mock qiymatlar. */
export const ADMIN_NAV_BADGES: NavBadges = {
  '/admin/faculties': 11,
  '/admin/tutors': 18,
  '/admin/students': 1284,
  '/admin/companies': 412,
};

/** SPEC-NAV §2.2 */
export const TUTOR_NAV_BADGES: NavBadges = {
  '/tutor': 38,
  '/tutor/applications': 7,
  '/tutor/students': 38,
  '/tutor/diaries': 12,
  '/tutor/leave-requests': 2,
};

const ADMIN_NAV: readonly SidebarNavItem[] = [
  { label: 'Dashboard', to: '/admin', end: true },
  { label: 'Fakultetlar', to: '/admin/faculties' },
  { label: 'Tyutorlar', to: '/admin/tutors' },
  { label: 'Talabalar', to: '/admin/students' },
  { label: 'Korxonalar', to: '/admin/companies' },
  { label: 'Hisobotlar', to: '/admin/reports' },
  { label: 'Audit jurnali', to: '/admin/audit' },
  { label: 'Sozlamalar', to: '/admin/settings' },
];

/**
 * ❓ SPEC-NAV'da tyutor default `/tutor/today`; mavjud auth mantiqi (`homePathForRole`) va testlar
 * `/tutor` ni kutadi — "Bugun" `/tutor` index sahifasi qilindi.
 */
const TUTOR_NAV: readonly SidebarNavItem[] = [
  { label: 'Bugun', to: '/tutor', end: true },
  { label: 'Arizalar', to: '/tutor/applications' },
  { label: 'Talabalarim', to: '/tutor/students' },
  { label: 'Kundaliklar', to: '/tutor/diaries' },
  { label: 'Kalendar', to: '/tutor/calendar' },
  { label: 'Xarita', to: '/tutor/map' },
  { label: 'Korxonalar', to: '/tutor/companies' },
  { label: "Ruxsat so'rovlari", to: '/tutor/leave-requests' },
  { label: 'Baholash', to: '/tutor/grading' },
  { label: 'Hisobotlar', to: '/tutor/reports' },
];

/** SPEC-NAV §6 — title = TITLE_OVERRIDE[route] ?? nav label. */
const TITLE_OVERRIDE: Record<string, string> = {
  '/admin': 'Umumiy dashboard',
  '/admin/audit': 'Audit jurnali',
  '/tutor/diaries': 'Kundalik hisobotlar',
};

/** SPEC-NAV §6 — crumb. ❓ Tyutor guruhlari dinamik bo'lishi kerak; hozircha statik. */
export const CRUMB: Record<NavRole, string> = {
  [UserRole.Admin]: 'Admin · TDIU · 2026-2027',
  [UserRole.Tutor]: 'Tyutor · 412-22, 413-22 · 3-kurs amaliyoti',
};

export const ROLE_LABEL: Record<NavRole, string> = {
  [UserRole.Admin]: "Admin · o'quv bo'limi",
  [UserRole.Tutor]: 'Tyutor',
};

export function navForRole(role: NavRole, badges?: NavBadges): SidebarNavItem[] {
  const base = role === UserRole.Admin ? ADMIN_NAV : TUTOR_NAV;
  const b = badges ?? (role === UserRole.Admin ? ADMIN_NAV_BADGES : TUTOR_NAV_BADGES);
  return base.map((it) => ({ ...it, badge: b[it.to] }));
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
