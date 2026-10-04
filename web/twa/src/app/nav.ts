/**
 * Talaba navigatsiyasi — pastki tab-bar'da aynan shu tartibda 5 ta to'g'ridan-to'g'ri tab ("Yana" yo'q).
 * ❓ Dizaynda 250px sidebar; TWA (360–430px) uchun pastki tab-bar.
 * Bosh ekran — amaliyot davri va uning kunlari (portfolio ko'rsatilmaydi). "Ruxsat so'rash" olib tashlangan.
 * "Korxonam" — amaliyot joyi (marshrut `/joyim` saqlangan).
 * "QR" (markazda) — davomat: korxonadagi QR → selfi → joylashuv. "Kalendarim" olib tashlangan
 * (eski `/kalendar` havolasi `/qr` ga yo'naltiriladi).
 */
export type NavIcon = 'home' | 'place' | 'diary' | 'qr' | 'profile';

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
  { label: 'QR orqali belgilash', short: 'QR', to: '/qr', icon: 'qr' },
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

/** Tab-bar'da bo'lmagan sahifalar sarlavhasi (masalan, profil kartasidan ochiladigan `/face`). */
const PAGE_TITLES: Record<string, string> = {
  '/face': 'Yuzni tasdiqlash',
};

export function pageTitleForPath(pathname: string): string | undefined {
  return PAGE_TITLES[pathname];
}
