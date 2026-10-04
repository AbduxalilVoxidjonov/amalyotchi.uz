import { api } from '@/shared/api';
import type { DiaryEntry } from '../diaries/types';
import type { AttendanceRange, StudentAttendanceDay } from './types';

/**
 * Talaba profilidagi davomat va kundaliklar — `AdminStudentsController`:
 *   GET /api/admin/students/:id/attendance?periodId=&from=&to= → StudentAttendanceDay[]
 *   GET /api/admin/students/:id/diaries?periodId=              → DiaryEntry[]
 * Profilning o'zi (`GET /api/admin/students/:id`) — `features/admin/students/api.ts`.
 * `periodId` (v3.5, §4.6) berilmasa backend sukut davrini tanlaydi.
 */
export const studentBase = (studentId: string) =>
  `/api/admin/students/${encodeURIComponent(studentId)}`;

export const studentProfileApi = {
  /** `from`/`to` — DateOnly; berilmasa backend davr boshidan bugungacha qaytaradi (davr chegarasiga qisiladi). */
  attendance: (studentId: string, range: AttendanceRange, periodId: string | null = null) =>
    api
      .get<
        (Omit<StudentAttendanceDay, 'events'> & {
          events?: StudentAttendanceDay['events'] | null;
        })[]
      >(`${studentBase(studentId)}/attendance`, {
        query: { periodId, from: range.from, to: range.to },
      })
      // `events` yangi maydon — eski javobda bo'lmasa bo'sh ro'yxat.
      .then((days): StudentAttendanceDay[] => days.map((d) => ({ ...d, events: d.events ?? [] }))),

  diaries: (studentId: string, periodId: string | null = null) =>
    api.get<DiaryEntry[]>(`${studentBase(studentId)}/diaries`, { query: { periodId } }),
};
