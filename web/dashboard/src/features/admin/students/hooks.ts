import { useCallback, useState } from 'react';
import { keepPreviousData, useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { downloadAuthFile } from '@/shared/files';
import { adminKeys } from '../shared/keys';
import type { ListParams } from '../shared/types';
import {
  STUDENTS_TEMPLATE_ENDPOINT,
  STUDENTS_TEMPLATE_FILE_NAME,
  studentsApi,
} from './api';

export function useStudentsQuery(params: ListParams) {
  return useQuery({
    queryKey: adminKeys.students(params),
    queryFn: () => studentsApi.list(params),
    placeholderData: keepPreviousData,
  });
}

/** Talaba profili (`/admin/students/:studentId`). */
export function useStudentQuery(id: string) {
  return useQuery({
    queryKey: adminKeys.student(id),
    queryFn: () => studentsApi.detail(id),
    enabled: id !== '',
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

export interface TemplateDownload {
  download: () => void;
  isLoading: boolean;
  /** Yuklab olinmasa — tugma yonida ko'rsatiladigan xabar. */
  error: string | null;
}

/** "Shablon" tugmasi: `.xlsx` ni token bilan olib, brauzerga saqlatadi. */
export function useStudentImportTemplate(): TemplateDownload {
  const [isLoading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const download = useCallback(() => {
    setLoading(true);
    setError(null);
    downloadAuthFile(STUDENTS_TEMPLATE_ENDPOINT, STUDENTS_TEMPLATE_FILE_NAME)
      .catch(() => setError("Shablonni yuklab bo'lmadi. Qaytadan urinib ko'ring."))
      .finally(() => setLoading(false));
  }, []);

  return { download, isLoading, error };
}
