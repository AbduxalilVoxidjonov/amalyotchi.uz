import { api } from '@/shared/api/client';
import { STUDENT_ENDPOINTS } from '@/shared/api/endpoints';
import type { CalendarMonthDto } from './types';

export const calendarApi = {
  /** `month` null → server joriy amaliyot oyini qaytaradi (javobdagi `month` bilan). */
  month: (month: string | null, signal?: AbortSignal) =>
    api.get<CalendarMonthDto>(STUDENT_ENDPOINTS.calendar, {
      query: { month: month ?? undefined },
      ...(signal ? { signal } : {}),
    }),
};
