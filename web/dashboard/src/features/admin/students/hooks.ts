import { keepPreviousData, useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { keepPreviousForSameStudent } from '@/features/tutor/students/hooks';
import { companiesApi } from '../companies/api';
import { adminKeys } from '../shared/keys';
import type { ListParams } from '../shared/types';
import { useTemplateDownload, type TemplateDownload } from '../shared/useTemplateDownload';
import { STUDENTS_TEMPLATE_ENDPOINT, STUDENTS_TEMPLATE_FILE_NAME, studentsApi } from './api';
import type { AssignCompanyInput } from './types';

export function useStudentsQuery(params: ListParams) {
  return useQuery({
    queryKey: adminKeys.students(params),
    queryFn: () => studentsApi.list(params),
    placeholderData: keepPreviousData,
  });
}

/** Talaba profili (`/admin/students/:studentId?period=`). `periodId` null — sukut davri. */
export function useStudentQuery(id: string, periodId: string | null = null) {
  return useQuery({
    queryKey: adminKeys.student(id, periodId),
    queryFn: () => studentsApi.detail(id, periodId),
    enabled: id !== '',
    placeholderData: keepPreviousForSameStudent(id, 2),
  });
}

/** Excel import: qabul qilingan qatorlar darhol ro'yxatda ko'rinishi uchun ro'yxat va dashboard yangilanadi. */
export function useImportStudents() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationKey: ['admin', 'students', 'import'],
    mutationFn: (file: File) => studentsApi.importExcel(file),
    onSuccess: (result) => {
      if (result.created === 0) return;
      void queryClient.invalidateQueries({ queryKey: adminKeys.studentsAll() });
      void queryClient.invalidateQueries({ queryKey: adminKeys.dashboard() });
    },
  });
}

/** "Shablon" tugmasi: talabalar import shablonini yuklab oladi. */
export function useStudentImportTemplate(): TemplateDownload {
  return useTemplateDownload(STUDENTS_TEMPLATE_ENDPOINT, STUDENTS_TEMPLATE_FILE_NAME);
}

/**
 * Biriktirish modalidagi korxona qidiruvi (`GET /api/admin/companies?q=`).
 * `features/admin/companies` hook'lari o'rniga shu yerda — modal ochilgandagina so'raladi.
 */
export function useCompanyPickerQuery(params: ListParams, enabled: boolean) {
  return useQuery({
    queryKey: adminKeys.companies(params),
    queryFn: () => companiesApi.list(params),
    placeholderData: keepPreviousData,
    enabled,
  });
}

/**
 * Ommaviy biriktirish: belgilangan talabalar bitta korxonaga biriktiriladi.
 * Muvaffaqiyatda talabalar ro'yxati, dashboard va korxonalar (talaba soni o'zgaradi) yangilanadi.
 */
export function useAssignCompany() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationKey: ['admin', 'students', 'assign-company'],
    mutationFn: (input: AssignCompanyInput) => studentsApi.assignCompany(input),
    onSuccess: (result) => {
      if (result.assigned === 0) return;
      void queryClient.invalidateQueries({ queryKey: adminKeys.studentsAll() });
      void queryClient.invalidateQueries({ queryKey: adminKeys.dashboard() });
      void queryClient.invalidateQueries({ queryKey: ['admin', 'companies'] });
    },
  });
}
