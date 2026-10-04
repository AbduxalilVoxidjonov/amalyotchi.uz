/**
 * Sidebar badge/crumb query kaliti: `['nav']` (`useNavData`, § nav-data.ts).
 * Alohida modul (bog'liqliksiz) — feature mutatsiyalari `navKeys.all` ni invalidate qilishi uchun.
 */
export const navKeys = {
  all: ['nav'] as const,
  admin: () => ['nav', 'admin'] as const,
};
