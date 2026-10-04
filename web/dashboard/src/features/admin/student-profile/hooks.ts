import { keepPreviousData, useQuery } from '@tanstack/react-query';
import { opsKeys } from '../shared/opsKeys';
import { studentProfileApi } from './api';
import type { AttendanceRange } from './types';

/**
 * Davr almashganda eski ma'lumot (xiralashgan holda) qoladi — faqat ayni talaba uchun;
 * boshqa talabaga o'tilsa placeholder berilmaydi (begona profil ko'rinmasin).
 * `idIndex` — kalitdagi talaba id'sining o'rni.
 */
export function keepPreviousForSameStudent<T>(studentId: string, idIndex: number) {
  return (prev: T | undefined, prevQuery?: { queryKey: readonly unknown[] }) =>
    prevQuery?.queryKey[idIndex] === studentId ? prev : undefined;
}

/** Kun-bakun davomat; `range` o'zgarsa qayta so'raladi. */
export function useStudentAttendanceQuery(
  studentId: string,
  range: AttendanceRange,
  periodId: string | null = null,
) {
  return useQuery({
    queryKey: opsKeys.students.attendance(studentId, range, periodId),
    queryFn: () => studentProfileApi.attendance(studentId, range, periodId),
    enabled: studentId.length > 0,
    placeholderData: keepPreviousData,
  });
}

/** Talabaning tanlangan davrdagi kundaliklari (faqat o'qish). */
export function useStudentDiariesQuery(studentId: string, periodId: string | null = null) {
  return useQuery({
    queryKey: opsKeys.students.diaries(studentId, periodId),
    queryFn: () => studentProfileApi.diaries(studentId, periodId),
    enabled: studentId.length > 0,
    placeholderData: keepPreviousData,
  });
}
