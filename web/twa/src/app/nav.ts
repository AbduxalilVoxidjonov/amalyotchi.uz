/**
 * Talaba navigatsiyasi — pastki tab-bar'da aynan shu tartibda 5 ta to'g'ridan-to'g'ri tab ("Yana" yo'q).
 * ❓ Dizaynda 250px sidebar; TWA (360–430px) uchun pastki tab-bar.
 * Bosh ekran — amaliyot davri va uning kunlari (portfolio ko'rsatilmaydi). "Ruxsat so'rash" olib tashlangan.
 * "Korxonam" — amaliyot joyi (marshrut `/joyim` saqlangan).
 */
export type NavIcon = 'home' | 'place' | 'diary' | 'calendar' | 'profile';

export interface StudentNavItem {
  /** Sarlavha (header h1). */
  label: string;
  /** Tab-bar'dagi qisqa nom (joy tor). */
  short: string;
  to: string;
  icon: NavIcon;
}

export const STUDENT_NAV: readonly StudentNavItem[] = [
  { label: 'Bosh ekran', short: 'Bosh ekran', to: '/', icon: 'home' },
  { label: 'Kundaligim', short: 'Kundaligim', to: '/kundalik', icon: 'diary' },
  { label: 'Kalendarim', short: 'Kalendarim', to: '/kalendar', icon: 'calendar' },
  { label: 'Korxonam', short: 'Korxonam', to: '/joyim', icon: 'place' },
  { label: 'Profil', short: 'Profil', to: '/profil', icon: 'profile' },
];

/** Header crumb: "Talaba · 3-kurs ishlab chiqarish amaliyoti" (kurs — UserSummaryDto.course dan). */
export function crumbFor(user: { course: number | null } | null): string {
  return user?.course ? `Talaba · ${user.course}-kurs ishlab chiqarish amaliyoti` : 'Talaba';
}

export function navItemForPath(pathname: string): StudentNavItem | undefined {
  return STUDENT_NAV.find((n) => (n.to === '/' ? pathname === '/' : pathname.startsWith(n.to)));
}
