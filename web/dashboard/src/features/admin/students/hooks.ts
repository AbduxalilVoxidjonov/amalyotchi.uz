import { keepPreviousData, useQuery } from '@tanstack/react-query';
import { adminKeys } from '../shared/keys';
import type { ListParams } from '../shared/types';
import { studentsApi } from './api';

export function useStudentsQuery(params: ListParams) {
  return useQuery({
    queryKey: adminKeys.students(params),
    queryFn: () => studentsApi.list(params),
    placeholderData: keepPreviousData,
  });
}

/** Talaba profili (`/admin/students/:studentId`). */
export function useStudentQuery(id: string) {
  return useQuery({
    queryKey: adminKeys.student(id),
    queryFn: () => studentsApi.detail(id),
    enabled: id !== '',
  });
}
