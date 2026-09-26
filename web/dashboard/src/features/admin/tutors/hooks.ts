import { keepPreviousData, useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { navKeys } from '@/app/nav-keys';
import { facultiesApi } from '../faculties/api';
import { adminKeys } from '../shared/keys';
import type { ListParams } from '../shared/types';
import { tutorsApi } from './api';
import type { TutorCreateInput, TutorListParams, TutorScopeInput, TutorUpdateInput } from './types';

const FACULTY_OPTIONS_PARAMS: Required<ListParams> = { q: '', page: 1, pageSize: 100 };

export function useTutorsQuery(params: TutorListParams) {
  return useQuery({
    queryKey: adminKeys.tutors(params),
    queryFn: () => tutorsApi.list(params),
    placeholderData: keepPreviousData,
  });
}

/** Bitta tyutor — `TutorDetailPage` (sarlavha kartasi + biriktirilgan ko'lam). */
export function useTutorQuery(id: string) {
  return useQuery({
    queryKey: adminKeys.tutor(id),
    queryFn: () => tutorsApi.get(id),
  });
}

/** Tyutor fakultetlari daraxtlari (egalari bilan) — `ScopePickerModal`. Faqat modal ochiq bo'lganda so'raladi. */
export function useTutorScopeTreeQuery(id: string, enabled: boolean) {
  return useQuery({
    queryKey: adminKeys.tutorScopeTree(id),
    queryFn: () => tutorsApi.scopeTree(id),
    enabled,
  });
}

/**
 * Ro'yxat (barcha `q`/fakultet/sahifa variantlari), detail (+ scope-tree) va dashboard
 * statistikasini yangilaydi. `id` berilmasa faqat ro'yxat/dashboard.
 */
function useInvalidateTutors() {
  const queryClient = useQueryClient();
  return (id?: string) => {
    void queryClient.invalidateQueries({ queryKey: adminKeys.tutorsAll() });
    void queryClient.invalidateQueries({ queryKey: navKeys.all });
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

export function useSetTutorScopes() {
  const invalidate = useInvalidateTutors();
  return useMutation({
    mutationKey: ['admin', 'tutors', 'set-scopes'],
    mutationFn: ({ id, scopes }: { id: string; scopes: TutorScopeInput[] }) =>
      tutorsApi.setScopes(id, scopes),
    onSuccess: (_data, { id }) => invalidate(id),
  });
}

/** Fakultet varianti: filtr `Select` uchun `value/label`, forma checkbox'lari uchun `code/isActive` ham. */
export interface FacultyOption {
  value: string;
  label: string;
  code: string;
  isActive: boolean;
}

/** Fakultet filtri/checkbox ro'yxati uchun variantlar (yangi endpoint yo'q — `facultiesApi.list`, 100 tagacha). */
export function useFacultyOptions() {
  return useQuery({
    queryKey: adminKeys.faculties(FACULTY_OPTIONS_PARAMS),
    queryFn: () => facultiesApi.list(FACULTY_OPTIONS_PARAMS),
    select: (data): FacultyOption[] =>
      data.items.map((f) => ({ value: f.id, label: f.name, code: f.code, isActive: f.isActive })),
    staleTime: 60_000,
  });
}
