import { keepPreviousData, useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { adminKeys } from '../../shared/keys';
import type { ListParams } from '../../shared/types';
import { departmentsApi } from './api';
import type { DepartmentInput } from './types';

export function useDepartmentsQuery(facultyId: string, params: ListParams) {
  return useQuery({
    queryKey: adminKeys.departments(facultyId, params),
    queryFn: () => departmentsApi.list(facultyId, params),
    placeholderData: keepPreviousData,
  });
}

/** Bitta kafedra (breadcrumb uchun) — `DepartmentDirectionsPage`. */
export function useDepartmentQuery(id: string) {
  return useQuery({
    queryKey: adminKeys.department(id),
    queryFn: () => departmentsApi.get(id),
  });
}

/** Kafedralar ro'yxati (shu fakultet) + fakultetlar ro'yxati (hisoblagichlar) + dashboard. */
function useInvalidateDepartments(facultyId: string) {
  const queryClient = useQueryClient();
  return () => {
    void queryClient.invalidateQueries({ queryKey: adminKeys.departmentsAll(facultyId) });
    void queryClient.invalidateQueries({ queryKey: adminKeys.facultiesAll() });
    void queryClient.invalidateQueries({ queryKey: adminKeys.dashboard() });
  };
}

export function useCreateDepartment(facultyId: string) {
  const invalidate = useInvalidateDepartments(facultyId);
  return useMutation({
    mutationKey: ['admin', 'departments', facultyId, 'create'],
    mutationFn: (body: DepartmentInput) => departmentsApi.create(facultyId, body),
    onSuccess: invalidate,
  });
}

export function useUpdateDepartment(facultyId: string) {
  const invalidate = useInvalidateDepartments(facultyId);
  return useMutation({
    mutationKey: ['admin', 'departments', facultyId, 'update'],
    mutationFn: ({ id, body }: { id: string; body: DepartmentInput }) =>
      departmentsApi.update(id, body),
    onSuccess: invalidate,
  });
}

export function useSetDepartmentStatus(facultyId: string) {
  const invalidate = useInvalidateDepartments(facultyId);
  return useMutation({
    mutationKey: ['admin', 'departments', facultyId, 'set-status'],
    mutationFn: ({ id, isActive }: { id: string; isActive: boolean }) =>
      departmentsApi.setStatus(id, isActive),
    onSuccess: invalidate,
  });
}

export function useDeleteDepartment(facultyId: string) {
  const invalidate = useInvalidateDepartments(facultyId);
  return useMutation({
    mutationKey: ['admin', 'departments', facultyId, 'delete'],
    mutationFn: (id: string) => departmentsApi.remove(id),
    onSuccess: invalidate,
  });
}
