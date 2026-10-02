import { useCallback, useEffect, useMemo, useRef, useState } from 'react';
import { useSearchParams } from 'react-router-dom';
import { clampPageSize } from '../students/pageSize';
import { DEFAULT_PAGE_SIZE } from '../shared/types';
import { useDebouncedValue } from '../shared/useDebouncedValue';
import type { RecipientListParams } from './types';

/** URL kalitlari: `?q=&page=&size=&faculty=&direction=&course=&group=` (`tab` ga tegilmaydi). */
const KEY = {
  q: 'q',
  page: 'page',
  size: 'size',
  faculty: 'faculty',
  direction: 'direction',
  course: 'course',
  group: 'group',
};

/** Tanlangan filtrlar (`''` — barchasi). */
export interface RecipientFilterValues {
  facultyId: string;
  directionId: string;
  /** `''` yoki kurs raqami matn ko'rinishida (`<select>` qiymati). */
  course: string;
  groupId: string;
}

const FILTER_KEYS: Record<keyof RecipientFilterValues, string> = {
  facultyId: KEY.faculty,
  directionId: KEY.direction,
  course: KEY.course,
  groupId: KEY.group,
};

function parseCourse(raw: string | null): string {
  const n = Number(raw);
  return Number.isInteger(n) && n > 0 ? String(n) : '';
}

/**
 * "Ulanganlar" ro'yxati holati URL'da (talabalar sahifasidagi `useStudentListParams` bilan bir xil
 * qoidalar): qidiruv 300ms debounce, sahifa hajmi 1..500 (`size`, sukut bo'lsa URL'da yo'q),
 * filtr/qidiruv o'zgarsa — 1-sahifa. Yozuvlar `replace` bilan (tarix to'lmaydi).
 * Qaytaradigan shakli `useListParams()` bilan mos (`tableState()` ga beriladi).
 */
export function useRecipientListParams(defaultPageSize = DEFAULT_PAGE_SIZE) {
  const [searchParams, setSearchParams] = useSearchParams();
  const urlQ = searchParams.get(KEY.q) ?? '';
  const rawSize = searchParams.get(KEY.size);
  const pageSize = rawSize === null ? defaultPageSize : (clampPageSize(rawSize) ?? defaultPageSize);
  const page = Math.max(1, Math.floor(Number(searchParams.get(KEY.page))) || 1);
  const filters = useMemo<RecipientFilterValues>(
    () => ({
      facultyId: searchParams.get(KEY.faculty) ?? '',
      directionId: searchParams.get(KEY.direction) ?? '',
      course: parseCourse(searchParams.get(KEY.course)),
      groupId: searchParams.get(KEY.group) ?? '',
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

  // Qidiruv maydoni — mahalliy holat, debounce'dan keyin URL'ga.
  const [search, setSearch] = useState(urlQ);
  const q = useDebouncedValue(search.trim(), 300);

  // URL tashqaridan o'zgarsa (orqaga/oldinga) — maydon ham moslanadi.
  const [prevUrlQ, setPrevUrlQ] = useState(urlQ);
  if (prevUrlQ !== urlQ) {
    setPrevUrlQ(urlQ);
    if (search.trim() !== urlQ) setSearch(urlQ);
  }

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

  const setPageSize = useCallback(
    (next: number) => {
      const size = clampPageSize(String(next)) ?? defaultPageSize;
      update({ [KEY.size]: size === defaultPageSize ? null : String(size) });
    },
    [update, defaultPageSize],
  );

  /** Filtrlarni o'zgartirish (qisman) — sahifa 1 ga qaytadi. */
  const setFilters = useCallback(
    (patch: Partial<RecipientFilterValues>) => {
      const entries = Object.entries(patch).map(([k, v]) => [
        FILTER_KEYS[k as keyof RecipientFilterValues],
        v || null,
      ]);
      update(Object.fromEntries(entries) as Record<string, string | null>);
    },
    [update],
  );

  /** Fakultet/yo'nalish/kurs/guruh filtrlarini tozalash (qidiruv matni saqlanadi). */
  const clearFilters = useCallback(
    () =>
      update({ [KEY.faculty]: null, [KEY.direction]: null, [KEY.course]: null, [KEY.group]: null }),
    [update],
  );

  const params = useMemo<RecipientListParams & { q: string; page: number; pageSize: number }>(
    () => ({
      q: urlQ,
      page,
      pageSize,
      ...(filters.facultyId ? { facultyId: filters.facultyId } : {}),
      ...(filters.directionId ? { directionId: filters.directionId } : {}),
      ...(filters.course ? { course: Number(filters.course) } : {}),
      ...(filters.groupId ? { groupId: filters.groupId } : {}),
    }),
    [urlQ, page, pageSize, filters],
  );

  const hasFilters = Boolean(
    filters.facultyId || filters.directionId || filters.course || filters.groupId,
  );

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
