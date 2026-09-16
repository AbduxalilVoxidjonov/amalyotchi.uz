import { keepPreviousData, useQuery } from '@tanstack/react-query';
import { adminKeys } from '../shared/keys';
import type { ListParams } from '../shared/types';
import { companiesApi } from './api';

export function useCompaniesQuery(params: ListParams) {
  return useQuery({
    queryKey: adminKeys.companies(params),
    queryFn: () => companiesApi.list(params),
    placeholderData: keepPreviousData,
  });
}

export function useCompanyQuery(id: string) {
  return useQuery({
    queryKey: adminKeys.company(id),
    queryFn: () => companiesApi.detail(id),
    enabled: id !== '',
  });
}

export function useCompanyStudentsQuery(id: string) {
  return useQuery({
    queryKey: adminKeys.companyStudents(id),
    queryFn: () => companiesApi.students(id),
    enabled: id !== '',
  });
}
