import type { NavRole } from './nav';

/**
 * Sidebar badge/crumb query kalitlari: `['nav', role]` (`useNavData`, § nav-data.ts).
 * Alohida modul (bog'liqliksiz) — feature mutatsiyalari `navKeys.all` ni invalidate qilishi uchun.
 */
export const navKeys = {
  all: ['nav'] as const,
  role: (role: NavRole) => ['nav', role] as const,
};
