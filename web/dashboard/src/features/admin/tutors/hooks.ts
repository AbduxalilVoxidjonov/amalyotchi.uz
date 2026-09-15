import { keepPreviousData, useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { facultiesApi } from '../faculties/api';
import { adminKeys } from '../shared/keys';
import type { ListParams } from '../shared/types';
import { tutorsApi } from './api';
import type { TutorCreateInput, TutorListParams, TutorUpdateInput } from './types';

const FACULTY_OPTIONS_PARAMS: Required<ListParams> = { q: '', page: 1, pageSize: 100 };

export function useTutorsQuery(params: TutorListParams) {
  return useQuery({
    queryKey: adminKeys.tutors(params),
    queryFn: () => tutorsApi.list(params),
    placeholderData: keepPreviousData,
  });
}

/** Bitta tyutor — `TutorDetailPage` (sarlavha kartasi + biriktirilgan guruhlar). */
export function useTutorQuery(id: string) {
  return useQuery({
    queryKey: adminKeys.tutor(id),
    queryFn: () => tutorsApi.get(id),
  });
}

/** Tyutor fakultetidagi faol guruhlar — `GroupsPickerModal`. Faqat modal ochiq bo'lganda so'raladi. */
export function useAvailableGroupsQuery(id: string, enabled: boolean) {
  return useQuery({
    queryKey: adminKeys.tutorAvailableGroups(id),
    queryFn: () => tutorsApi.availableGroups(id),
    enabled,
  });
}

/**
 * Ro'yxat (barcha `q`/fakultet/sahifa variantlari), detail (+ available-groups) va dashboard
 * statistikasini yangilaydi. `id` berilmasa faqat ro'yxat/dashboard.
 */
function useInvalidateTutors() {
  const queryClient = useQueryClient();
  return (id?: string) => {
    void queryClient.invalidateQueries({ queryKey: adminKeys.tutorsAll() });
    if (id) void queryClient.invalidateQueries({ queryKey: adminKeys.tutor(id) });
    void queryClient.invalidateQueries({ queryKey: adminKeys.dashboard() });
    // Guruhlar jadvalidagi "Tyutor" ustuni ham o'zgaradi.
    void queryClient.invalidateQueries({ queryKey: ['admin', 'groups'] });
  };
}

export function useCreateTutor() {
  const invalidate = useInvalidateTutors();
  return useMutation({
    mutationKey: ['admin', 'tutors', 'create'],
    mutationFn: (body: TutorCreateInput) => tutorsApi.create(body),
    onSuccess: () => invalidate(),
  });
}

export function useUpdateTutor() {
  const invalidate = useInvalidateTutors();
  return useMutation({
    mutationKey: ['admin', 'tutors', 'update'],
    mutationFn: ({ id, body }: { id: string; body: TutorUpdateInput }) =>
      tutorsApi.update(id, body),
    onSuccess: (_data, { id }) => invalidate(id),
  });
}

export function useSetTutorStatus() {
  const invalidate = useInvalidateTutors();
  return useMutation({
    mutationKey: ['admin', 'tutors', 'set-status'],
    mutationFn: ({ id, isActive }: { id: string; isActive: boolean }) =>
      tutorsApi.setStatus(id, isActive),
    onSuccess: (_data, { id }) => invalidate(id),
  });
}

export function useResetTutorPassword() {
  return useMutation({
    mutationKey: ['admin', 'tutors', 'reset-password'],
    mutationFn: ({ id, password }: { id: string; password: string }) =>
      tutorsApi.resetPassword(id, password),
  });
}

export function useSetTutorGroups() {
  const invalidate = useInvalidateTutors();
  return useMutation({
    mutationKey: ['admin', 'tutors', 'set-groups'],
    mutationFn: ({ id, groupIds }: { id: string; groupIds: string[] }) =>
      tutorsApi.setGroups(id, groupIds),
    onSuccess: (_data, { id }) => invalidate(id),
  });
}

/** Fakultet select'i uchun variantlar (yangi endpoint yo'q — `facultiesApi.list`, 100 tagacha). */
export function useFacultyOptions() {
  return useQuery({
    queryKey: adminKeys.faculties(FACULTY_OPTIONS_PARAMS),
    queryFn: () => facultiesApi.list(FACULTY_OPTIONS_PARAMS),
    select: (data) => data.items.map((f) => ({ value: f.id, label: f.name })),
    staleTime: 60_000,
  });
}
