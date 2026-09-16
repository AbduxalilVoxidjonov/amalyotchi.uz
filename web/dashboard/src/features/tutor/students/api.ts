import { api } from '@/shared/api';
import type { DiaryEntry } from '../diaries/types';
import type {
  AttendanceRange,
  StudentAttendanceDay,
  StudentApiArea,
  TutorStudent,
  TutorStudentDetail,
} from './types';

/**
 * Tyutor · Talabalarim — `TutorStudentsController`:
 *   GET /api/tutor/students                       → TutorStudent[]
 *   GET /api/tutor/students/:id                   → TutorStudentDetail   (ko'lamdan tashqari → 404)
 *   GET /api/tutor/students/:id/attendance?from=&to= → StudentAttendanceDay[]
 *   GET /api/tutor/students/:id/diaries           → TutorDiaryEntry[] (= DiaryEntry)
 *
 * Davomat va kundaliklar admin profilida ham xuddi shu shaklda beriladi
 * (`AdminStudentsController` — `/api/admin/students/:id/...`), shuning uchun yo'l `area` bilan tanlanadi.
 */
export const studentBase = (area: StudentApiArea, studentId: string) =>
  `/api/${area}/students/${encodeURIComponent(studentId)}`;

export const studentsApi = {
  list: () => api.get<TutorStudent[]>('/api/tutor/students'),

  detail: (studentId: string) => api.get<TutorStudentDetail>(studentBase('tutor', studentId)),

  /** `from`/`to` — DateOnly; berilmasa backend davr boshidan bugungacha qaytaradi. */
  attendance: (studentId: string, range: AttendanceRange, area: StudentApiArea = 'tutor') =>
    api.get<StudentAttendanceDay[]>(`${studentBase(area, studentId)}/attendance`, {
      query: { from: range.from, to: range.to },
    }),

  diaries: (studentId: string, area: StudentApiArea = 'tutor') =>
    api.get<DiaryEntry[]>(`${studentBase(area, studentId)}/diaries`),
};
