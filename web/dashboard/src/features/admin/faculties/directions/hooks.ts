import { keepPreviousData, useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { adminKeys } from '../../shared/keys';
import { studentFiltersKey } from '../../students/api';
import type { ListParams } from '../../shared/types';
import { directionsApi } from './api';
import type { DirectionInput } from './types';

export function useDirectionsQuery(departmentId: string, params: ListParams) {
  return useQuery({
    queryKey: adminKeys.directions(departmentId, params),
    queryFn: () => directionsApi.list(departmentId, params),
    placeholderData: keepPreviousData,
  });
}

/** Bitta yo'nalish (breadcrumb uchun) — `DirectionGroupsPage`. */
export function useDirectionQuery(id: string) {
  return useQuery({
    queryKey: adminKeys.direction(id),
    queryFn: () => directionsApi.get(id),
  });
}

/**
 * Yo'nalishlar ro'yxati (shu kafedra) + ota hisoblagichlari: kafedralar ro'yxati (shu fakultet),
 * fakultetlar ro'yxati, dashboard.
 */
function useInvalidateDirections(departmentId: string, facultyId: string) {
  const queryClient = useQueryClient();
  return () => {
    void queryClient.invalidateQueries({ queryKey: adminKeys.directionsAll(departmentId) });
    void queryClient.invalidateQueries({ queryKey: adminKeys.departmentsAll(facultyId) });
    void queryClient.invalidateQueries({ queryKey: adminKeys.facultiesAll() });
    void queryClient.invalidateQueries({ queryKey: adminKeys.dashboard() });
    void queryClient.invalidateQueries({ queryKey: studentFiltersKey() });
  };
}

export function useCreateDirection(departmentId: string, facultyId: string) {
  const invalidate = useInvalidateDirections(departmentId, facultyId);
  return useMutation({
    mutationKey: ['admin', 'directions', departmentId, 'create'],
    mutationFn: (body: DirectionInput) => directionsApi.create(departmentId, body),
    onSuccess: invalidate,
  });
}

export function useUpdateDirection(departmentId: string, facultyId: string) {
  const invalidate = useInvalidateDirections(departmentId, facultyId);
  return useMutation({
    mutationKey: ['admin', 'directions', departmentId, 'update'],
    mutationFn: ({ id, body }: { id: string; body: DirectionInput }) =>
      directionsApi.update(id, body),
    onSuccess: invalidate,
  });
}

export function useSetDirectionStatus(departmentId: string, facultyId: string) {
  const invalidate = useInvalidateDirections(departmentId, facultyId);
  return useMutation({
    mutationKey: ['admin', 'directions', departmentId, 'set-status'],
    mutationFn: ({ id, isActive }: { id: string; isActive: boolean }) =>
      directionsApi.setStatus(id, isActive),
    onSuccess: invalidate,
  });
}

export function useDeleteDirection(departmentId: string, facultyId: string) {
  const invalidate = useInvalidateDirections(departmentId, facultyId);
  return useMutation({
    mutationKey: ['admin', 'directions', departmentId, 'delete'],
    mutationFn: (id: string) => directionsApi.remove(id),
    onSuccess: invalidate,
  });
}
