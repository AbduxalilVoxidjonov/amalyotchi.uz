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
 *   GET /api/tutor/students/:id?periodId=         → TutorStudentDetail   (ko'lamdan tashqari / begona davr → 404)
 *   GET /api/tutor/students/:id/attendance?periodId=&from=&to= → StudentAttendanceDay[]
 *   GET /api/tutor/students/:id/diaries?periodId= → TutorDiaryEntry[] (= DiaryEntry)
 *
 * `periodId` (v3.5, §4.6) berilmasa backend sukut davrini tanlaydi; berilsa hamma bloklar shu davr bo'yicha.
 *
 * Davomat va kundaliklar admin profilida ham xuddi shu shaklda beriladi
 * (`AdminStudentsController` — `/api/admin/students/:id/...`), shuning uchun yo'l `area` bilan tanlanadi.
 */
export const studentBase = (area: StudentApiArea, studentId: string) =>
  `/api/${area}/students/${encodeURIComponent(studentId)}`;

export const studentsApi = {
  list: () => api.get<TutorStudent[]>('/api/tutor/students'),

  detail: (studentId: string, periodId: string | null = null) =>
    api.get<TutorStudentDetail>(studentBase('tutor', studentId), { query: { periodId } }),

  /** `from`/`to` — DateOnly; berilmasa backend davr boshidan bugungacha qaytaradi (davr chegarasiga qisiladi). */
  attendance: (
    studentId: string,
    range: AttendanceRange,
    area: StudentApiArea = 'tutor',
    periodId: string | null = null,
  ) =>
    api
      .get<
        (Omit<StudentAttendanceDay, 'events'> & {
          events?: StudentAttendanceDay['events'] | null;
        })[]
      >(`${studentBase(area, studentId)}/attendance`, {
        query: { periodId, from: range.from, to: range.to },
      })
      // `events` yangi maydon — eski javobda bo'lmasa bo'sh ro'yxat.
      .then((days): StudentAttendanceDay[] => days.map((d) => ({ ...d, events: d.events ?? [] }))),

  diaries: (studentId: string, area: StudentApiArea = 'tutor', periodId: string | null = null) =>
    api.get<DiaryEntry[]>(`${studentBase(area, studentId)}/diaries`, { query: { periodId } }),
};
