import { keepPreviousData, useQuery } from '@tanstack/react-query';
import { tutorKeys } from '../query-keys';
import { studentsApi } from './api';
import type { AttendanceRange, StudentApiArea } from './types';

export function useMyStudentsQuery() {
  return useQuery({ queryKey: tutorKeys.students(), queryFn: studentsApi.list });
}

/**
 * Davr almashganda eski ma'lumot (xiralashgan holda) qoladi — faqat ayni talaba uchun;
 * boshqa talabaga o'tilsa placeholder berilmaydi (begona profil ko'rinmasin).
 * `idIndex` — kalitdagi talaba id'sining o'rni.
 */
export function keepPreviousForSameStudent<T>(studentId: string, idIndex: number) {
  return (prev: T | undefined, prevQuery?: { queryKey: readonly unknown[] }) =>
    prevQuery?.queryKey[idIndex] === studentId ? prev : undefined;
}

/** Talaba profili (`/tutor/students/:studentId?period=`). `periodId` null — sukut davri. */
export function useStudentQuery(studentId: string, periodId: string | null = null) {
  return useQuery({
    queryKey: tutorKeys.students.detail(studentId, periodId),
    queryFn: () => studentsApi.detail(studentId, periodId),
    enabled: studentId.length > 0,
    placeholderData: keepPreviousForSameStudent(studentId, 3),
  });
}

/**
 * Kun-bakun davomat; `range` o'zgarsa qayta so'raladi. `area` — qaysi endpoint oilasidan
 * so'ralayotgani (tyutor yoki admin profili); kalitga ham kiradi, ikki rol keshni almashtirmasin.
 */
export function useStudentAttendanceQuery(
  studentId: string,
  range: AttendanceRange,
  area: StudentApiArea = 'tutor',
  periodId: string | null = null,
) {
  return useQuery({
    queryKey: tutorKeys.students.attendance(studentId, range, area, periodId),
    queryFn: () => studentsApi.attendance(studentId, range, area, periodId),
    enabled: studentId.length > 0,
    placeholderData: keepPreviousData,
  });
}

/** Talabaning tanlangan davrdagi kundaliklari (faqat o'qish). */
export function useStudentDiariesQuery(
  studentId: string,
  area: StudentApiArea = 'tutor',
  periodId: string | null = null,
) {
  return useQuery({
    queryKey: tutorKeys.students.diaries(studentId, area, periodId),
    queryFn: () => studentsApi.diaries(studentId, area, periodId),
    enabled: studentId.length > 0,
    placeholderData: keepPreviousData,
  });
}
