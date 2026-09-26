/** Talabalar jadvalida sahifa hajmi chegaralari (backend `GET /api/admin/students` — `pageSize` ≤ 500). */
export const MIN_STUDENT_PAGE_SIZE = 1;
export const MAX_STUDENT_PAGE_SIZE = 500;

/**
 * Kiritilgan matnni 1..500 butun songa keltiradi. Bo'sh / son emas → `null` (joriy qiymat saqlanadi);
 * kasr → pastga yaxlitlanadi; 0 va manfiy → 1; 500 dan katta → 500.
 */
export function clampPageSize(raw: string): number | null {
  const trimmed = raw.trim();
  if (!trimmed) return null;
  const n = Number(trimmed);
  if (!Number.isFinite(n)) return null;
  return Math.min(MAX_STUDENT_PAGE_SIZE, Math.max(MIN_STUDENT_PAGE_SIZE, Math.floor(n)));
}
