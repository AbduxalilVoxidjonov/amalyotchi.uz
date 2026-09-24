import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { departmentsApi } from '../faculties/departments/api';
import { directionsApi } from '../faculties/directions/api';
import { groupsApi } from '../faculties/groups/api';
import { facultiesApi } from '../faculties/api';
import { adminKeys } from '../shared/keys';
import type { ListParams } from '../shared/types';
import { practicePeriodsApi } from './api';
import type { PracticePeriodCreate, PracticePeriodStatus, PracticePeriodUpdate } from './types';

/**
 * `['admin', 'practice-periods', ...]` — ro'yxat (status bo'yicha), detail, statistika va davr
 * ichidagi guruh talabalari. Hammasi `all` prefiksi ostida — davr/guruh mutatsiyalari
 * (`useInvalidatePeriods`) statistikani ham yangilaydi.
 */
export const practicePeriodKeys = {
  all: ['admin', 'practice-periods'] as const,
  list: (status: PracticePeriodStatus | null) =>
    ['admin', 'practice-periods', 'list', status ?? 'all'] as const,
  detail: (id: string) => ['admin', 'practice-periods', 'detail', id] as const,
  stats: (id: string) => ['admin', 'practice-periods', 'stats', id] as const,
  groupStudents: (id: string, groupId: string) =>
    ['admin', 'practice-periods', 'group-students', id, groupId] as const,
};

export function usePracticePeriodsQuery(status: PracticePeriodStatus | null) {
  return useQuery({
    queryKey: practicePeriodKeys.list(status),
    queryFn: () => practicePeriodsApi.list(status),
  });
}

export function usePracticePeriodQuery(id: string) {
  return useQuery({
    queryKey: practicePeriodKeys.detail(id),
    queryFn: () => practicePeriodsApi.detail(id),
    enabled: id !== '',
  });
}

export function usePracticePeriodStatsQuery(id: string) {
  return useQuery({
    queryKey: practicePeriodKeys.stats(id),
    queryFn: () => practicePeriodsApi.stats(id),
    enabled: id !== '',
  });
}

export function usePeriodGroupStudentsQuery(id: string, groupId: string) {
  return useQuery({
    queryKey: practicePeriodKeys.groupStudents(id, groupId),
    queryFn: () => practicePeriodsApi.groupStudents(id, groupId),
    enabled: id !== '' && groupId !== '',
  });
}

/**
 * Davrlar (ro'yxat + detail + statistika + guruh talabalari), guruhlar ro'yxatlari (`period` maydoni o'zgaradi) va dashboard.
 */
function useInvalidatePeriods() {
  const queryClient = useQueryClient();
  return () => {
    void queryClient.invalidateQueries({ queryKey: practicePeriodKeys.all });
    void queryClient.invalidateQueries({ queryKey: ['admin', 'groups'] });
    void queryClient.invalidateQueries({ queryKey: adminKeys.dashboard() });
  };
}

export function useCreatePracticePeriod() {
  const invalidate = useInvalidatePeriods();
  const queryClient = useQueryClient();
  return useMutation({
    mutationKey: ['admin', 'practice-periods', 'create'],
    mutationFn: (body: PracticePeriodCreate) => practicePeriodsApi.create(body),
    onSuccess: (created) => {
      queryClient.setQueryData(practicePeriodKeys.detail(created.id), created);
      invalidate();
    },
  });
}

export function useUpdatePracticePeriod(id: string) {
  const invalidate = useInvalidatePeriods();
  return useMutation({
    mutationKey: ['admin', 'practice-periods', id, 'update'],
    mutationFn: (body: PracticePeriodUpdate) => practicePeriodsApi.update(id, body),
    onSuccess: invalidate,
  });
}

export function useSetPracticePeriodGroups(id: string) {
  const invalidate = useInvalidatePeriods();
  return useMutation({
    mutationKey: ['admin', 'practice-periods', id, 'groups'],
    mutationFn: (groupIds: string[]) => practicePeriodsApi.setGroups(id, { groupIds }),
    onSuccess: invalidate,
  });
}

export function useClosePracticePeriod(id: string) {
  const invalidate = useInvalidatePeriods();
  return useMutation({
    mutationKey: ['admin', 'practice-periods', id, 'close'],
    mutationFn: () => practicePeriodsApi.close(id),
    onSuccess: invalidate,
  });
}

export function useDeletePracticePeriod(id: string) {
  const invalidate = useInvalidatePeriods();
  const queryClient = useQueryClient();
  return useMutation({
    mutationKey: ['admin', 'practice-periods', id, 'delete'],
    mutationFn: () => practicePeriodsApi.remove(id),
    onSuccess: () => {
      queryClient.removeQueries({ queryKey: practicePeriodKeys.detail(id) });
      invalidate();
    },
  });
}

/* ────────────────────────────────────────────────────────────────────────────
 * GroupPicker kaskadi: mavjud ierarxiya endpoint'lari (bitta sahifa, backend maksimumi 100).
 * Kalitlar ierarxiya sahifalari bilan umumiy (`adminKeys`) — kesh ulashiladi.
 * ──────────────────────────────────────────────────────────────────────────── */
const PICKER_PARAMS: Required<ListParams> = { q: '', page: 1, pageSize: 100 };
/** Ma'lumotnoma — tez-tez o'zgarmaydi. */
const PICKER_STALE_MS = 60_000;

export function usePickerFaculties() {
  return useQuery({
    queryKey: adminKeys.faculties(PICKER_PARAMS),
    queryFn: () => facultiesApi.list(PICKER_PARAMS),
    staleTime: PICKER_STALE_MS,
  });
}

export function usePickerDepartments(facultyId: string) {
  return useQuery({
    queryKey: adminKeys.departments(facultyId, PICKER_PARAMS),
    queryFn: () => departmentsApi.list(facultyId, PICKER_PARAMS),
    enabled: facultyId !== '',
    staleTime: PICKER_STALE_MS,
  });
}

export function usePickerDirections(departmentId: string) {
  return useQuery({
    queryKey: adminKeys.directions(departmentId, PICKER_PARAMS),
    queryFn: () => directionsApi.list(departmentId, PICKER_PARAMS),
    enabled: departmentId !== '',
    staleTime: PICKER_STALE_MS,
  });
}

export function usePickerGroups(directionId: string) {
  return useQuery({
    queryKey: adminKeys.groups(directionId, PICKER_PARAMS),
    queryFn: () => groupsApi.list(directionId, PICKER_PARAMS),
    enabled: directionId !== '',
  });
}
