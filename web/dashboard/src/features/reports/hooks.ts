import { useQuery } from '@tanstack/react-query';
import { reportsApi } from './api';

export const reportsKeys = {
  all: ['reports'] as const,
  catalog: () => ['reports', 'catalog'] as const,
};

export function useReportsCatalogQuery() {
  return useQuery({
    queryKey: reportsKeys.catalog(),
    queryFn: reportsApi.catalog,
    staleTime: 5 * 60 * 1000,
  });
}
