import { useQuery } from '@tanstack/react-query';
import { tutorKeys } from '../query-keys';
import { companiesApi } from './api';

export function useTutorCompaniesQuery() {
  return useQuery({ queryKey: tutorKeys.companies.list(), queryFn: companiesApi.list });
}

export function useTutorCompanyQuery(id: string) {
  return useQuery({
    queryKey: tutorKeys.companies.detail(id),
    queryFn: () => companiesApi.detail(id),
    enabled: id !== '',
  });
}

export function useTutorCompanyStudentsQuery(id: string) {
  return useQuery({
    queryKey: tutorKeys.companies.students(id),
    queryFn: () => companiesApi.students(id),
    enabled: id !== '',
  });
}
