import { useQuery } from '@tanstack/react-query';
import { tutorKeys } from '../query-keys';
import { studentsApi } from './api';
import type { AttendanceRange } from './types';

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

/** Kun-bakun davomat; `range` o'zgarsa qayta so'raladi. */
export function useStudentAttendanceQuery(studentId: string, range: AttendanceRange) {
  return useQuery({
    queryKey: tutorKeys.students.attendance(studentId, range),
    queryFn: () => studentsApi.attendance(studentId, range),
    enabled: studentId.length > 0,
  });
}

/** Talabaning kundaliklari (faqat o'qish). */
export function useStudentDiariesQuery(studentId: string) {
  return useQuery({
    queryKey: tutorKeys.students.diaries(studentId),
    queryFn: () => studentsApi.diaries(studentId),
    enabled: studentId.length > 0,
  });
}
