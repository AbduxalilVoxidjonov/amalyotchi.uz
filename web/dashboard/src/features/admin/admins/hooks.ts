import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { adminKeys } from '../shared/keys';
import { adminsApi } from './api';
import type { AdminCreateInput, AdminUpdateInput } from './types';

/** Adminlar ro'yxati (faqat bosh admin — `enabled` bilan oddiy adminda so'ralmaydi). */
export function useAdminsQuery(enabled = true) {
  return useQuery({
    queryKey: adminKeys.admins(),
    queryFn: adminsApi.list,
    enabled,
  });
}

function useInvalidateAdmins() {
  const queryClient = useQueryClient();
  return () => void queryClient.invalidateQueries({ queryKey: adminKeys.admins() });
}

export function useCreateAdmin() {
  const invalidate = useInvalidateAdmins();
  return useMutation({
    mutationKey: ['admin', 'admins', 'create'],
    mutationFn: (body: AdminCreateInput) => adminsApi.create(body),
    onSuccess: invalidate,
  });
}

export function useUpdateAdmin() {
  const invalidate = useInvalidateAdmins();
  return useMutation({
    mutationKey: ['admin', 'admins', 'update'],
    mutationFn: ({ id, body }: { id: string; body: AdminUpdateInput }) =>
      adminsApi.update(id, body),
    onSuccess: invalidate,
  });
}

export function useSetAdminStatus() {
  const invalidate = useInvalidateAdmins();
  return useMutation({
    mutationKey: ['admin', 'admins', 'set-status'],
    mutationFn: ({ id, isActive }: { id: string; isActive: boolean }) =>
      adminsApi.setStatus(id, isActive),
    onSuccess: invalidate,
  });
}

export function useResetAdminPassword() {
  return useMutation({
    mutationKey: ['admin', 'admins', 'reset-password'],
    mutationFn: (id: string) => adminsApi.resetPassword(id),
  });
}
