import { api } from '@/shared/api';
import type { CalendarResponse } from './types';

/** Tyutor · Kalendar — `TutorCalendarController`: GET /api/tutor/calendar?month=2026-10 → CalendarResponse | 400 */
export const calendarApi = {
  get: (params: { month: string }) =>
    api.get<CalendarResponse>('/api/tutor/calendar', { query: { month: params.month } }),
};
