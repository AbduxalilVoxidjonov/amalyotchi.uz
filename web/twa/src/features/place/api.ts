import { api } from '@/shared/api/client';
import { COMPANY_ENDPOINTS, STUDENT_ENDPOINTS } from '@/shared/api/endpoints';
import type { CompanyLookupDto, PracticePlaceDto, SubmitPlaceCommand } from './types';

export const placeApi = {
  get: (signal?: AbortSignal) =>
    api.get<PracticePlaceDto>(STUDENT_ENDPOINTS.place, signal ? { signal } : {}),
  /** STIR bo'yicha korxonani topish (talaba ma'lumotni qo'lda kiritmaydi). */
  lookup: (tin: string, signal?: AbortSignal) =>
    api.get<CompanyLookupDto>(COMPANY_ENDPOINTS.lookup, {
      query: { tin },
      ...(signal ? { signal } : {}),
    }),
  /** Amaliyot joyi arizasini yuborish — faqat `{ tin }`. */
  submit: (body: SubmitPlaceCommand) => api.post<PracticePlaceDto>(STUDENT_ENDPOINTS.place, body),
};
