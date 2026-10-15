import { formatCount } from '@/features/admin/shared/format';
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

/** Sidebar bandi + qidiruv uchun qo'shimcha kalit so'zlar (sinonimlar, ichki sahifalar). */
export interface AdminNavItem extends SidebarNavItem {
  keywords?: readonly string[];
  /**
   * Badge — "kutilayotgan ish" soni (ko'rib chiqilmagan ariza, tasdiqlanmagan yuz, tekshirilmagan
   * kundalik). Faqat shular yopiq bo'lim yig'indisiga kiradi; ma'lumot sonlari (talabalar,
   * korxonalar, fakultetlar jami, bugungi jadval qatorlari) kirmaydi.
   */
  pending?: true;
}

export type NavGroupId = 'main' | 'students' | 'structure' | 'practice' | 'comms' | 'system';

/** Sidebar bo'limi (yig'iladigan guruh). */
export interface NavGroup {
  id: NavGroupId;
  label: string;
  items: AdminNavItem[];
}

interface NavGroupDef {
  id: NavGroupId;
  label: string;
  items: readonly AdminNavItem[];
}

/**
 * Bo'limlar: umumiy ko'rinish → talabalar bilan kundalik ish → tuzilma → amaliyot tashkiloti →
 * aloqa/hisobot → tizim boshqaruvi. Tartib shu yerda yagona manba (`adminNav` — tekis ro'yxat).
 */
const ADMIN_NAV_GROUPS: readonly NavGroupDef[] = [
  {
    id: 'main',
    label: 'Asosiy',
    items: [
      { label: 'Dashboard', to: '/admin', end: true, keywords: ['umumiy', 'bosh sahifa'] },
      { label: 'Bugun', to: '/admin/today', keywords: ['davomat', 'check-in', 'kelganlar'] },
      { label: 'Xarita', to: '/admin/map', keywords: ['joylashuv', 'lokatsiya', 'geo'] },
    ],
  },
  {
    id: 'students',
    label: 'Talabalar',
    items: [
      { label: 'Talabalar', to: '/admin/students', keywords: ['talaba', 'amaliyotchi'] },
      {
        label: 'Arizalar',
        to: '/admin/applications',
        pending: true,
        keywords: ["so'rov", 'ruxsat'],
      },
      {
        label: 'Yuz tasdiqlash',
        to: '/admin/face',
        pending: true,
        keywords: ['yuz etaloni', 'etalon', 'face'],
      },
      {
        label: 'Kundaliklar',
        to: '/admin/diaries',
        pending: true,
        keywords: ['kundalik', 'hisobot'],
      },
      { label: 'Baholash', to: '/admin/grading', keywords: ['baho', 'ball', 'reyting'] },
    ],
  },
  {
    id: 'structure',
    label: 'Tuzilma',
    items: [
      {
        label: 'Fakultetlar',
        to: '/admin/faculties',
        keywords: ['fakultet', 'kafedra', "yo'nalish", 'guruh'],
      },
    ],
  },
  {
    id: 'practice',
    label: 'Amaliyot',
    items: [
      { label: 'Amaliyot davrlari', to: '/admin/practice-periods', keywords: ['davr', 'guruh'] },
      { label: 'Korxonalar', to: '/admin/companies', keywords: ['kompaniya', 'tashkilot'] },
      { label: 'Kalendar', to: '/admin/calendar', keywords: ['jadval', 'sana', 'taqvim'] },
    ],
  },
  {
    id: 'comms',
    label: 'Aloqa va hisobot',
    items: [
      { label: 'Xabarlar', to: '/admin/messages', keywords: ["e'lon", 'bildirishnoma', 'xabar'] },
      { label: 'Hisobotlar', to: '/admin/reports', keywords: ['eksport', 'excel', 'statistika'] },
    ],
  },
  {
    id: 'system',
    label: 'Tizim',
    items: [
      { label: 'Adminlar', to: ADMINS_NAV_PATH, keywords: ['admin', 'foydalanuvchi'] },
      { label: 'Audit jurnali', to: '/admin/audit', keywords: ['log', 'tarix'] },
      { label: 'Sozlamalar', to: '/admin/settings', keywords: ['settings', 'telegram'] },
    ],
  },
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

/**
 * Rol bo'yicha filtrlangan bo'limlar (bo'sh bo'lim tashlanadi). `badges` yo'q bo'lsa — hech bir
 * elementda badge ko'rsatilmaydi (soxta son chiqmasin).
 */
export function adminNavGroups(badges?: NavBadges, options: AdminNavOptions = {}): NavGroup[] {
  return ADMIN_NAV_GROUPS.map((g) => ({
    id: g.id,
    label: g.label,
    items: g.items
      .filter((it) => it.to !== ADMINS_NAV_PATH || options.isSuperAdmin === true)
      .map((it) => ({ ...it, badge: badges?.[it.to] })),
  })).filter((g) => g.items.length > 0);
}

/** Tekis ro'yxat (bo'limlar tartibida) — sarlavha/aktiv band hisoblash uchun. */
export function adminNav(badges?: NavBadges, options: AdminNavOptions = {}): AdminNavItem[] {
  return adminNavGroups(badges, options).flatMap((g) => g.items);
}

/** Joriy pathname joylashgan bo'lim (aktiv band bo'yicha). */
export function activeGroupId(
  groups: readonly NavGroup[],
  pathname: string,
): NavGroupId | undefined {
  const active = activeNavItem(
    groups.flatMap((g) => g.items),
    pathname,
  );
  return active ? groups.find((g) => g.items.includes(active))?.id : undefined;
}

/**
 * Bo'lim sarlavhasidagi badge — faqat "kutilayotgan ish" (`pending`) bandlari badge'lari yig'indisi
 * (yopiq bo'lganda ko'rsatiladi). Ma'lumot sonlari qo'shilmaydi — aralash raqam chalg'itadi.
 * Badge'lar formatlangan ("1 284") bo'lishi mumkin → raqamlar ajratib olinadi. 0 → undefined.
 */
export function groupBadge(group: NavGroup): string | undefined {
  let sum = 0;
  for (const it of group.items) {
    if (it.pending !== true || it.badge === undefined || it.badge === '') continue;
    const n = typeof it.badge === 'number' ? it.badge : Number(String(it.badge).replace(/\D/g, ''));
    if (Number.isFinite(n)) sum += n;
  }
  return sum > 0 ? formatCount(sum) : undefined;
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
