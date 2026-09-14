import type { UseQueryResult } from '@tanstack/react-query';
import { useCallback, useMemo, useState } from 'react';
import type { TableStateProps } from '../components/AdminTable';
import { DEFAULT_PAGE_SIZE, type ListParams, type Paged } from './types';
import { useDebouncedValue } from './useDebouncedValue';

/**
 * Jadval ekranlari uchun qidiruv (300ms debounce) + sahifa holati.
 * Qidiruv o'zgarganda sahifa avtomatik 1 ga qaytadi (effect'siz: sahifa `q` ga bog'lab saqlanadi).
 */
export function useListParams(pageSize = DEFAULT_PAGE_SIZE) {
  const [search, setSearch] = useState('');
  const q = useDebouncedValue(search.trim(), 300);
  const [pageFor, setPageFor] = useState<{ q: string; page: number }>({ q: '', page: 1 });
  const page = pageFor.q === q ? pageFor.page : 1;

  const setPage = useCallback((next: number) => setPageFor({ q, page: next }), [q]);
  const params = useMemo<Required<ListParams>>(() => ({ q, page, pageSize }), [q, page, pageSize]);

  return { search, setSearch, page, setPage, params };
}

/** `useListParams()` + `useQuery` natijasini presentation props'iga yig'adi. */
export function tableState<T>(
  list: ReturnType<typeof useListParams>,
  query: UseQueryResult<Paged<T>>,
): TableStateProps<T> {
  return {
    data: query.data,
    isLoading: query.isPending,
    error: query.error,
    onRetry: () => void query.refetch(),
    search: list.search,
    onSearchChange: list.setSearch,
    page: list.page,
    pageSize: list.params.pageSize,
    onPageChange: list.setPage,
  };
}
