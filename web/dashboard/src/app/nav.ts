import { UserRole } from '@amaliyotchi/shared/auth';
import type { SidebarNavItem } from '@/shared/ui';

/**
 * Sidebar ro'yxatlari — SPEC-NAV.md §2 (tartib aynan).
 * Badge sonlari — real API: `useNavData(role)` (`GET /api/admin/nav` | `GET /api/tutor/nav`) →
 * `navBadges(data)` → `navForRole(role, badges)`. Badges berilmasa (yuklanish/xato) — badge yo'q.
 */
export type NavRole = typeof UserRole.Admin | typeof UserRole.Tutor;

export interface NavBadges {
  [to: string]: string | number | undefined;
}

const ADMIN_NAV: readonly SidebarNavItem[] = [
  { label: 'Dashboard', to: '/admin', end: true },
  { label: 'Fakultetlar', to: '/admin/faculties' },
  { label: 'Tyutorlar', to: '/admin/tutors' },
  { label: 'Korxonalar', to: '/admin/companies' },
  { label: 'Talabalar', to: '/admin/students' },
  { label: 'Amaliyot davrlari', to: '/admin/practice-periods' },
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
  { label: 'Baholash', to: '/tutor/grading' },
  { label: 'Hisobotlar', to: '/tutor/reports' },
];

/** SPEC-NAV §6 — title = TITLE_OVERRIDE[route] ?? nav label. */
const TITLE_OVERRIDE: Record<string, string> = {
  '/admin': 'Umumiy dashboard',
  '/admin/audit': 'Audit jurnali',
  '/tutor/diaries': 'Kundalik hisobotlar',
};

export const ROLE_LABEL: Record<NavRole, string> = {
  [UserRole.Admin]: "Admin · o'quv bo'limi",
  [UserRole.Tutor]: 'Tyutor',
};

/** `badges` yo'q bo'lsa — hech bir elementda badge ko'rsatilmaydi (soxta son chiqmasin). */
export function navForRole(role: NavRole, badges?: NavBadges): SidebarNavItem[] {
  const base = role === UserRole.Admin ? ADMIN_NAV : TUTOR_NAV;
  return base.map((it) => ({ ...it, badge: badges?.[it.to] }));
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
