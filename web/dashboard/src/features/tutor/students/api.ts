import { api } from '@/shared/api';
import type { DiaryEntry } from '../diaries/types';
import type {
  AttendanceRange,
  StudentAttendanceDay,
  TutorStudent,
  TutorStudentDetail,
} from './types';

/**
 * Tyutor · Talabalarim — `TutorStudentsController`:
 *   GET /api/tutor/students                       → TutorStudent[]
 *   GET /api/tutor/students/:id                   → TutorStudentDetail   (ko'lamdan tashqari → 404)
 *   GET /api/tutor/students/:id/attendance?from=&to= → StudentAttendanceDay[]
 *   GET /api/tutor/students/:id/diaries           → TutorDiaryEntry[] (= DiaryEntry)
 */
export const studentsApi = {
  list: () => api.get<TutorStudent[]>('/api/tutor/students'),

  detail: (studentId: string) =>
    api.get<TutorStudentDetail>(`/api/tutor/students/${encodeURIComponent(studentId)}`),

  /** `from`/`to` — DateOnly; berilmasa backend davr boshidan bugungacha qaytaradi. */
  attendance: (studentId: string, range: AttendanceRange) =>
    api.get<StudentAttendanceDay[]>(
      `/api/tutor/students/${encodeURIComponent(studentId)}/attendance`,
      { query: { from: range.from, to: range.to } },
    ),

  diaries: (studentId: string) =>
    api.get<DiaryEntry[]>(`/api/tutor/students/${encodeURIComponent(studentId)}/diaries`),
};
