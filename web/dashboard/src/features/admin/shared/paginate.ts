import { DEFAULT_PAGE_SIZE, type Paged } from './types';

/**
 * MSW mock'lari uchun: `?q=&page=&pageSize=` parametrlarini o'qib, ro'yxatni filtrlaydi va sahifalaydi.
 * `haystack(item)` — qidiruv uchun matn maydonlari (case-insensitive `includes`).
 */
export function paginateMock<T>(
  url: string,
  items: readonly T[],
  haystack: (item: T) => readonly (string | null | undefined)[],
): Paged<T> {
  const { searchParams } = new URL(url);
  const q = (searchParams.get('q') ?? '').trim().toLowerCase();
  const page = Math.max(1, Number(searchParams.get('page')) || 1);
  const pageSize = Math.max(1, Number(searchParams.get('pageSize')) || DEFAULT_PAGE_SIZE);

  const filtered = q
    ? items.filter((it) => haystack(it).some((s) => s?.toLowerCase().includes(q)))
    : [...items];
  const start = (page - 1) * pageSize;
  return { items: filtered.slice(start, start + pageSize), page, pageSize, total: filtered.length };
}
