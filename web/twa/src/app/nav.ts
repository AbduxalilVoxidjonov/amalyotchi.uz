/**
 * Talaba navigatsiyasi — SPEC-NAV 2.3 (aynan tartib) + 3.2 marshrutlar.
 * ❓ Dizaynda 250px sidebar; TWA (360–430px) uchun pastki tab-bar: 4 asosiy tab + "Yana"
 * (SPEC-NAV 3.2 tavsiyasi). "Yana" ichida qolgan 2 bo'lim (Ruxsat so'rash, Portfolio) to'liq nomi bilan.
 */
export type NavIcon = 'home' | 'place' | 'diary' | 'calendar' | 'leave' | 'portfolio';

export interface StudentNavItem {
  /** Sarlavha (header h1) va "Yana" ro'yxati — SPEC'dagi aynan nom. */
  label: string;
  /** Tab-bar'dagi qisqa nom (joy tor). */
  short: string;
  to: string;
  icon: NavIcon;
}

export const STUDENT_NAV: readonly StudentNavItem[] = [
  { label: 'Bosh ekran', short: 'Bosh ekran', to: '/', icon: 'home' },
  { label: 'Amaliyot joyim', short: 'Joyim', to: '/joyim', icon: 'place' },
  { label: 'Kundaligim', short: 'Kundaligim', to: '/kundalik', icon: 'diary' },
  { label: 'Kalendarim', short: 'Kalendarim', to: '/kalendar', icon: 'calendar' },
  { label: "Ruxsat so'rash", short: "Ruxsat so'rash", to: '/ruxsat', icon: 'leave' },
  { label: 'Portfolio', short: 'Portfolio', to: '/portfolio', icon: 'portfolio' },
];

/** Tab-bar'da to'g'ridan-to'g'ri ko'rinadigan bo'limlar soni; qolgani "Yana" ichida. */
export const PRIMARY_TAB_COUNT = 4;

/** Header crumb: "Talaba · 3-kurs ishlab chiqarish amaliyoti" (kurs — UserSummaryDto.course dan). */
export function crumbFor(user: { course: number | null } | null): string {
  return user?.course ? `Talaba · ${user.course}-kurs ishlab chiqarish amaliyoti` : 'Talaba';
}

/** "Yana" varag'idagi rol satri (SPEC-SCREENS §2: `412-22 · 3-kurs`) — kontrakt v2 `groupName`/`course`. */
export function roleLineFor(
  user: { groupName: string | null; course: number | null } | null,
): string {
  const parts = [user?.groupName, user?.course ? `${user.course}-kurs` : null].filter(Boolean);
  return parts.length > 0 ? parts.join(' · ') : 'Talaba';
}

export function navItemForPath(pathname: string): StudentNavItem | undefined {
  return STUDENT_NAV.find((n) => (n.to === '/' ? pathname === '/' : pathname.startsWith(n.to)));
}
