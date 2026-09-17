import { keepPreviousData, useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { adminKeys } from '../shared/keys';
import type { ListParams } from '../shared/types';
import { useTemplateDownload, type TemplateDownload } from '../shared/useTemplateDownload';
import { COMPANIES_TEMPLATE_ENDPOINT, COMPANIES_TEMPLATE_FILE_NAME, companiesApi } from './api';
import type { CompanyInput } from './types';

export function useCompaniesQuery(params: ListParams) {
  return useQuery({
    queryKey: adminKeys.companies(params),
    queryFn: () => companiesApi.list(params),
    placeholderData: keepPreviousData,
  });
}

export function useCompanyQuery(id: string) {
  return useQuery({
    queryKey: adminKeys.company(id),
    queryFn: () => companiesApi.detail(id),
    enabled: id !== '',
  });
}

export function useCompanyStudentsQuery(id: string) {
  return useQuery({
    queryKey: adminKeys.companyStudents(id),
    queryFn: () => companiesApi.students(id),
    enabled: id !== '',
  });
}

/**
 * Ro'yxat (barcha `q`/sahifa variantlari) va dashboard statistikasini yangilaydi.
 * `id` berilsa — detail sahifasi ham (STIR/radius/holat o'zgargan bo'lishi mumkin).
 */
function useInvalidateCompanies() {
  const queryClient = useQueryClient();
  return (id?: string) => {
    void queryClient.invalidateQueries({ queryKey: adminKeys.companiesAll() });
    if (id) void queryClient.invalidateQueries({ queryKey: adminKeys.company(id) });
    void queryClient.invalidateQueries({ queryKey: adminKeys.dashboard() });
  };
}

export function useCreateCompany() {
  const invalidate = useInvalidateCompanies();
  return useMutation({
    mutationKey: ['admin', 'companies', 'create'],
    mutationFn: (body: CompanyInput) => companiesApi.create(body),
    onSuccess: () => invalidate(),
  });
}

export function useUpdateCompany() {
  const invalidate = useInvalidateCompanies();
  return useMutation({
    mutationKey: ['admin', 'companies', 'update'],
    mutationFn: ({ id, body }: { id: string; body: CompanyInput }) => companiesApi.update(id, body),
    onSuccess: (_data, { id }) => invalidate(id),
  });
}

export function useSetCompanyStatus() {
  const invalidate = useInvalidateCompanies();
  return useMutation({
    mutationKey: ['admin', 'companies', 'set-status'],
    mutationFn: ({ id, isActive }: { id: string; isActive: boolean }) =>
      companiesApi.setStatus(id, isActive),
    onSuccess: (_data, { id }) => invalidate(id),
  });
}

export function useDeleteCompany() {
  const invalidate = useInvalidateCompanies();
  return useMutation({
    mutationKey: ['admin', 'companies', 'delete'],
    mutationFn: (id: string) => companiesApi.remove(id),
    onSuccess: (_data, id) => invalidate(id),
  });
}

/** Excel import: qabul qilingan qatorlar darhol ro'yxatda ko'rinishi uchun ro'yxat/dashboard yangilanadi. */
export function useImportCompanies() {
  const invalidate = useInvalidateCompanies();
  return useMutation({
    mutationKey: ['admin', 'companies', 'import'],
    mutationFn: (file: File) => companiesApi.importExcel(file),
    onSuccess: (result) => {
      if (result.created === 0) return;
      invalidate();
    },
  });
}

/** "Shablon" tugmasi: korxonalar import shablonini yuklab oladi. */
export function useCompanyImportTemplate(): TemplateDownload {
  return useTemplateDownload(COMPANIES_TEMPLATE_ENDPOINT, COMPANIES_TEMPLATE_FILE_NAME);
}
