import { useQuery } from '@tanstack/react-query';
import { UserRole } from '@amaliyotchi/shared/auth';
import { formatCount } from '@/features/admin/shared/format';
import { api } from '@/shared/api';
import type { NavBadges, NavRole } from './nav';
import { navKeys } from './nav-keys';

export { navKeys };

/**
 * Sidebar badge sonlari va Topbar crumb konteksti — real API.
 *   GET /api/admin/nav (Admin) → AdminNavDto
 *   GET /api/tutor/nav (Tutor) → TutorNavDto
 * Har son tegishli sahifaning sukut holatidagi jamiga teng (backend kontrakti).
 */
export const NAV_ENDPOINTS = {
  [UserRole.Admin]: '/api/admin/nav',
  [UserRole.Tutor]: '/api/tutor/nav',
} as const satisfies Record<NavRole, string>;

export interface AdminNavDto {
  counts: { faculties: number; tutors: number; companies: number; students: number };
  context: { academicYear: string | null };
}

export interface TutorNavDto {
  counts: {
    today: number;
    applications: number;
    students: number;
    diaries: number;
    /** v3.27 — tyutor tekshiruvini kutayotgan etalon yuz rasmlari. Eski server yubormaydi. */
    pendingFaceEnrollments?: number;
  };
  context: { groups: string[]; periodName: string | null };
  /** v3.27 — `counts` ichida bo'lmasa, yuqori darajada kelishi ham qabul qilinadi. */
  pendingFaceEnrollments?: number;
}

/** Rol bilan belgilangan javob — `navBadges`/`navCrumb` qaysi shaklligini aniq biladi. */
export type NavData =
  ({ role: typeof UserRole.Admin } & AdminNavDto) | ({ role: typeof UserRole.Tutor } & TutorNavDto);

async function fetchNav(role: NavRole): Promise<NavData> {
  if (role === UserRole.Admin) {
    const dto = await api.get<AdminNavDto>(NAV_ENDPOINTS[UserRole.Admin]);
    return { role, ...dto };
  }
  const dto = await api.get<TutorNavDto>(NAV_ENDPOINTS[UserRole.Tutor]);
  return { role, ...dto };
}

/** Sidebar sonlari + crumb konteksti. 60s yangi hisoblanadi; oynaga qaytilganda qayta so'raladi. */
export function useNavData(role: NavRole) {
  return useQuery({
    queryKey: navKeys.role(role),
    queryFn: () => fetchNav(role),
    staleTime: 60_000,
    refetchOnWindowFocus: true,
  });
}

/** 0 — badge ko'rsatilmaydi (bo'sh "0" chip shovqin); katta son — "1 284" (thin space). */
function badge(n: number): string | undefined {
  return Number.isFinite(n) && n > 0 ? formatCount(n) : undefined;
}

/** API javobi → `to` → badge xaritasi (`navForRole(role, badges)` uchun). */
export function navBadges(data: NavData): NavBadges {
  if (data.role === UserRole.Admin) {
    const c = data.counts;
    return {
      '/admin/faculties': badge(c.faculties),
      '/admin/tutors': badge(c.tutors),
      '/admin/companies': badge(c.companies),
      '/admin/students': badge(c.students),
    };
  }
  const c = data.counts;
  return {
    '/tutor': badge(c.today),
    '/tutor/applications': badge(c.applications),
    '/tutor/students': badge(c.students),
    '/tutor/diaries': badge(c.diaries),
    '/tutor/face': badge(c.pendingFaceEnrollments ?? data.pendingFaceEnrollments ?? 0),
  };
}

/** Crumb'da ko'rsatiladigan guruhlar chegarasi; ko'p bo'lsa — "412-22, 413-22 +2". */
const MAX_CRUMB_GROUPS = 3;
const SHOWN_WHEN_TRUNCATED = 2;

export function formatCrumbGroups(groups: readonly string[]): string {
  const list = groups.map((g) => g.trim()).filter(Boolean);
  if (list.length <= MAX_CRUMB_GROUPS) return list.join(', ');
  const shown = list.slice(0, SHOWN_WHEN_TRUNCATED).join(', ');
  return `${shown} +${list.length - SHOWN_WHEN_TRUNCATED}`;
}

const ROLE_CRUMB: Record<NavRole, string> = {
  [UserRole.Admin]: 'Admin',
  [UserRole.Tutor]: 'Tyutor',
};

/**
 * SPEC-NAV §6 — crumb. Admin: "Admin · 2026-2027"; tyutor: "Tyutor · 412-22, 413-22 · 3-kurs amaliyoti".
 * Bo'sh qismlar tashlab yuboriladi; ma'lumot hali yo'q (yuklanish/xato) — faqat rol nomi.
 */
export function navCrumb(role: NavRole, data: NavData | undefined): string {
  const parts: (string | null | undefined)[] = [ROLE_CRUMB[role]];
  if (data?.role === UserRole.Admin && role === UserRole.Admin) {
    parts.push(data.context.academicYear);
  } else if (data?.role === UserRole.Tutor && role === UserRole.Tutor) {
    parts.push(formatCrumbGroups(data.context.groups), data.context.periodName);
  }
  return parts
    .map((p) => p?.trim())
    .filter(Boolean)
    .join(' · ');
}
