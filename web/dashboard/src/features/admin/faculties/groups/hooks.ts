import { keepPreviousData, useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { adminKeys } from '../../shared/keys';
import type { ListParams } from '../../shared/types';
import { groupsApi } from './api';
import type { GroupInput } from './types';

export function useGroupsQuery(directionId: string, params: ListParams) {
  return useQuery({
    queryKey: adminKeys.groups(directionId, params),
    queryFn: () => groupsApi.list(directionId, params),
    placeholderData: keepPreviousData,
  });
}

/**
 * Guruhlar ro'yxati (shu yo'nalish) + ota hisoblagichlari: yo'nalishlar (shu kafedra),
 * kafedralar (shu fakultet), fakultetlar, dashboard.
 */
function useInvalidateGroups(directionId: string, departmentId: string, facultyId: string) {
  const queryClient = useQueryClient();
  return () => {
    void queryClient.invalidateQueries({ queryKey: adminKeys.groupsAll(directionId) });
    void queryClient.invalidateQueries({ queryKey: adminKeys.directionsAll(departmentId) });
    void queryClient.invalidateQueries({ queryKey: adminKeys.departmentsAll(facultyId) });
    void queryClient.invalidateQueries({ queryKey: adminKeys.facultiesAll() });
    void queryClient.invalidateQueries({ queryKey: adminKeys.dashboard() });
  };
}

export function useCreateGroup(directionId: string, departmentId: string, facultyId: string) {
  const invalidate = useInvalidateGroups(directionId, departmentId, facultyId);
  return useMutation({
    mutationKey: ['admin', 'groups', directionId, 'create'],
    mutationFn: (body: GroupInput) => groupsApi.create(directionId, body),
    onSuccess: invalidate,
  });
}

export function useUpdateGroup(directionId: string, departmentId: string, facultyId: string) {
  const invalidate = useInvalidateGroups(directionId, departmentId, facultyId);
  return useMutation({
    mutationKey: ['admin', 'groups', directionId, 'update'],
    mutationFn: ({ id, body }: { id: string; body: GroupInput }) => groupsApi.update(id, body),
    onSuccess: invalidate,
  });
}

export function useSetGroupStatus(directionId: string, departmentId: string, facultyId: string) {
  const invalidate = useInvalidateGroups(directionId, departmentId, facultyId);
  return useMutation({
    mutationKey: ['admin', 'groups', directionId, 'set-status'],
    mutationFn: ({ id, isActive }: { id: string; isActive: boolean }) =>
      groupsApi.setStatus(id, isActive),
    onSuccess: invalidate,
  });
}

export function useDeleteGroup(directionId: string, departmentId: string, facultyId: string) {
  const invalidate = useInvalidateGroups(directionId, departmentId, facultyId);
  return useMutation({
    mutationKey: ['admin', 'groups', directionId, 'delete'],
    mutationFn: (id: string) => groupsApi.remove(id),
    onSuccess: invalidate,
  });
}
