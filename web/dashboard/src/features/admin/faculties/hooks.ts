import { keepPreviousData, useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { adminKeys } from '../shared/keys';
import type { ListParams } from '../shared/types';
import { facultiesApi } from './api';
import type { FacultyInput } from './types';

export function useFacultiesQuery(params: ListParams) {
  return useQuery({
    queryKey: adminKeys.faculties(params),
    queryFn: () => facultiesApi.list(params),
    placeholderData: keepPreviousData,
  });
}

/** Bitta fakultet (breadcrumb uchun) — `FacultyDepartmentsPage`. */
export function useFacultyQuery(id: string) {
  return useQuery({
    queryKey: adminKeys.faculty(id),
    queryFn: () => facultiesApi.get(id),
  });
}

/** Fakultetlar ro'yxati (barcha `q`/sahifa variantlari) + dashboard statistikasini yangilaydi. */
function useInvalidateFaculties() {
  const queryClient = useQueryClient();
  return () => {
    void queryClient.invalidateQueries({ queryKey: adminKeys.facultiesAll() });
    void queryClient.invalidateQueries({ queryKey: adminKeys.dashboard() });
  };
}

export function useCreateFaculty() {
  const invalidate = useInvalidateFaculties();
  return useMutation({
    mutationKey: ['admin', 'faculties', 'create'],
    mutationFn: (body: FacultyInput) => facultiesApi.create(body),
    onSuccess: invalidate,
  });
}

export function useUpdateFaculty() {
  const invalidate = useInvalidateFaculties();
  return useMutation({
    mutationKey: ['admin', 'faculties', 'update'],
    mutationFn: ({ id, body }: { id: string; body: FacultyInput }) => facultiesApi.update(id, body),
    onSuccess: invalidate,
  });
}

export function useSetFacultyStatus() {
  const invalidate = useInvalidateFaculties();
  return useMutation({
    mutationKey: ['admin', 'faculties', 'set-status'],
    mutationFn: ({ id, isActive }: { id: string; isActive: boolean }) =>
      facultiesApi.setStatus(id, isActive),
    onSuccess: invalidate,
  });
}

export function useDeleteFaculty() {
  const invalidate = useInvalidateFaculties();
  return useMutation({
    mutationKey: ['admin', 'faculties', 'delete'],
    mutationFn: (id: string) => facultiesApi.remove(id),
    onSuccess: invalidate,
  });
}
