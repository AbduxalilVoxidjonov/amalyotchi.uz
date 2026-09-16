import { useQuery } from '@tanstack/react-query';
import { tutorKeys } from '../query-keys';
import { studentsApi } from './api';
import type { AttendanceRange, StudentApiArea } from './types';

export function useMyStudentsQuery() {
  return useQuery({ queryKey: tutorKeys.students(), queryFn: studentsApi.list });
}

/** Talaba profili (`/tutor/students/:studentId`). */
export function useStudentQuery(studentId: string) {
  return useQuery({
    queryKey: tutorKeys.students.detail(studentId),
    queryFn: () => studentsApi.detail(studentId),
    enabled: studentId.length > 0,
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
) {
  return useQuery({
    queryKey: tutorKeys.students.attendance(studentId, range, area),
    queryFn: () => studentsApi.attendance(studentId, range, area),
    enabled: studentId.length > 0,
  });
}

/** Talabaning kundaliklari (faqat o'qish). */
export function useStudentDiariesQuery(studentId: string, area: StudentApiArea = 'tutor') {
  return useQuery({
    queryKey: tutorKeys.students.diaries(studentId, area),
    queryFn: () => studentsApi.diaries(studentId, area),
    enabled: studentId.length > 0,
  });
}
