import { useQuery } from '@tanstack/react-query';
import { formatCount } from '@/features/admin/shared/format';
import { api } from '@/shared/api';
import type { NavBadges } from './nav';
import { navKeys } from './nav-keys';

export { navKeys };

/**
 * Sidebar badge sonlari va Topbar crumb konteksti — real API: `GET /api/admin/nav` → AdminNavDto.
 * Har son tegishli sahifaning sukut holatidagi jamiga teng (backend kontrakti).
 */
export const NAV_ENDPOINT = '/api/admin/nav';

export interface AdminNavDto {
  counts: {
    faculties: number;
    companies: number;
    students: number;
    /**
     * v3.28 — sobiq tyutor nav sonlari (`/api/tutor/nav`) shu yerga qo'shildi. Eski server
     * yubormasligi mumkin → ixtiyoriy (yo'q bo'lsa badge yo'q).
     */
    today?: number;
    applications?: number;
    diaries?: number;
    pendingFaceEnrollments?: number;
  };
  context: { academicYear: string | null };
}

export type NavData = AdminNavDto;

/** Sidebar sonlari + crumb konteksti. 60s yangi hisoblanadi; oynaga qaytilganda qayta so'raladi. */
export function useNavData() {
  return useQuery({
    queryKey: navKeys.admin(),
    queryFn: () => api.get<AdminNavDto>(NAV_ENDPOINT),
    staleTime: 60_000,
    refetchOnWindowFocus: true,
  });
}

/** 0 — badge ko'rsatilmaydi (bo'sh "0" chip shovqin); katta son — "1 284" (thin space). */
function badge(n: number | undefined): string | undefined {
  return n !== undefined && Number.isFinite(n) && n > 0 ? formatCount(n) : undefined;
}

/** API javobi → `to` → badge xaritasi (`adminNav(badges)` uchun). */
export function navBadges(data: NavData): NavBadges {
  const c = data.counts;
  return {
    '/admin/today': badge(c.today),
    '/admin/applications': badge(c.applications),
    '/admin/face': badge(c.pendingFaceEnrollments),
    '/admin/diaries': badge(c.diaries),
    '/admin/faculties': badge(c.faculties),
    '/admin/companies': badge(c.companies),
    '/admin/students': badge(c.students),
  };
}

/**
 * SPEC-NAV §6 — crumb: "Admin · 2026-2027". Bo'sh qismlar tashlab yuboriladi; ma'lumot hali yo'q
 * (yuklanish/xato) — faqat "Admin".
 */
export function navCrumb(data: NavData | undefined): string {
  return ['Admin', data?.context.academicYear]
    .map((p) => p?.trim())
    .filter(Boolean)
    .join(' · ');
}
