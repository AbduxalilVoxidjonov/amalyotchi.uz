import { useCallback, useEffect, useMemo, useRef, useState } from 'react';
import { useSearchParams } from 'react-router-dom';
import { DEFAULT_PAGE_SIZE } from '../shared/types';
import { useDebouncedValue } from '../shared/useDebouncedValue';
import { clampPageSize } from './pageSize';
import type { StudentListParams } from './types';

/** URL kalitlari: `?q=&page=&size=&faculty=&direction=&course=`. */
const KEY = {
  q: 'q',
  page: 'page',
  size: 'size',
  faculty: 'faculty',
  direction: 'direction',
  course: 'course',
};

/** Tanlangan filtrlar (`''` — barchasi). */
export interface StudentFilterValues {
  facultyId: string;
  directionId: string;
  /** `''` yoki kurs raqami matn ko'rinishida (`<select>` qiymati). */
  course: string;
}

const FILTER_KEYS: Record<keyof StudentFilterValues, string> = {
  facultyId: KEY.faculty,
  directionId: KEY.direction,
  course: KEY.course,
};

/** `?size=` → 1..500 (noto'g'ri/yo'q bo'lsa — sukut). */
function parsePageSize(raw: string | null, fallback: number): number {
  return raw === null ? fallback : (clampPageSize(raw) ?? fallback);
}

function parseCourse(raw: string | null): string {
  const n = Number(raw);
  return Number.isInteger(n) && n > 0 ? String(n) : '';
}

/**
 * Talabalar ro'yxati holati URL'da (`useListParams` o'rniga): sahifa yangilansa yoki profildan
 * orqaga qaytilsa qidiruv, sahifa, sahifa hajmi (`size`, sukut bo'lsa URL'da yo'q) va filtrlar saqlanadi. Yozuvlar `replace` bilan — tarix
 * har bir harf/filtr bilan to'lmaydi. Qidiruv (300ms debounce) yoki filtr o'zgarsa — 1-sahifa.
 * Qaytaradigan shakli `useListParams()` bilan mos (`tableState()` ga beriladi).
 */
export function useStudentListParams(defaultPageSize = DEFAULT_PAGE_SIZE) {
  const [searchParams, setSearchParams] = useSearchParams();
  const urlQ = searchParams.get(KEY.q) ?? '';
  const pageSize = parsePageSize(searchParams.get(KEY.size), defaultPageSize);
  const page = Math.max(1, Math.floor(Number(searchParams.get(KEY.page))) || 1);
  const filters = useMemo<StudentFilterValues>(
    () => ({
      facultyId: searchParams.get(KEY.faculty) ?? '',
      directionId: searchParams.get(KEY.direction) ?? '',
      course: parseCourse(searchParams.get(KEY.course)),
    }),
    [searchParams],
  );

  /** URL'ni yangilash: `null`/`''` — kalit o'chiriladi; `page` berilmasa — 1-sahifa (kalitsiz). */
  const update = useCallback(
    (patch: Record<string, string | null>) => {
      setSearchParams(
        (prev) => {
          const next = new URLSearchParams(prev);
          if (!(KEY.page in patch)) next.delete(KEY.page);
          for (const [key, value] of Object.entries(patch)) {
            if (value) next.set(key, value);
            else next.delete(key);
          }
          return next;
        },
        { replace: true },
      );
    },
    [setSearchParams],
  );

  // Qidiruv maydoni — mahalliy holat (har harfda URL yozilmaydi), debounce'dan keyin URL'ga.
  const [search, setSearch] = useState(urlQ);
  const q = useDebouncedValue(search.trim(), 300);

  // URL tashqaridan o'zgarsa (orqaga/oldinga, havola) — maydon ham moslanadi.
  const [prevUrlQ, setPrevUrlQ] = useState(urlQ);
  if (prevUrlQ !== urlQ) {
    setPrevUrlQ(urlQ);
    if (search.trim() !== urlQ) setSearch(urlQ);
  }

  // Faqat debounce qiymati o'zgarganda yoziladi (URL o'zgarishi o'zi qayta yozishga olib kelmaydi).
  const lastQ = useRef(q);
  useEffect(() => {
    if (lastQ.current === q) return;
    lastQ.current = q;
    if (q !== urlQ) update({ [KEY.q]: q });
  }, [q, urlQ, update]);

  const setPage = useCallback(
    (next: number) => update({ [KEY.page]: next > 1 ? String(next) : null }),
    [update],
  );

  /** Sahifa hajmi (1..500 ga qisiladi) — sukut bo'lsa URL'dan o'chiriladi; sahifa 1 ga qaytadi. */
  const setPageSize = useCallback(
    (next: number) => {
      const size = clampPageSize(String(next)) ?? defaultPageSize;
      update({ [KEY.size]: size === defaultPageSize ? null : String(size) });
    },
    [update, defaultPageSize],
  );

  /** Filtrlarni o'zgartirish (qisman) — sahifa 1 ga qaytadi. */
  const setFilters = useCallback(
    (patch: Partial<StudentFilterValues>) => {
      const entries = Object.entries(patch).map(([k, v]) => [
        FILTER_KEYS[k as keyof StudentFilterValues],
        v || null,
      ]);
      update(Object.fromEntries(entries) as Record<string, string | null>);
    },
    [update],
  );

  /** Fakultet/yo'nalish/kurs filtrlarini tozalash (qidiruv matni saqlanadi). */
  const clearFilters = useCallback(
    () => update({ [KEY.faculty]: null, [KEY.direction]: null, [KEY.course]: null }),
    [update],
  );

  const params = useMemo<StudentListParams & { q: string; page: number; pageSize: number }>(
    () => ({
      q: urlQ,
      page,
      pageSize,
      ...(filters.facultyId ? { facultyId: filters.facultyId } : {}),
      ...(filters.directionId ? { directionId: filters.directionId } : {}),
      ...(filters.course ? { course: Number(filters.course) } : {}),
    }),
    [urlQ, page, pageSize, filters],
  );

  const hasFilters = Boolean(filters.facultyId || filters.directionId || filters.course);

  return {
    search,
    setSearch,
    page,
    setPage,
    pageSize,
    setPageSize,
    params,
    filters,
    setFilters,
    clearFilters,
    hasFilters,
  };
}
