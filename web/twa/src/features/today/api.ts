import { api } from '@/shared/api/client';
import { STUDENT_ENDPOINTS } from '@/shared/api/endpoints';
import type { CheckinRequest, TodayDto } from './types';

/**
 * Check-in/check-out — multipart/form-data (kontrakt §1.3). Kundalik yuborish naqshi bilan bir xil:
 * `api` klienti `FormData` ni o'zgarishsiz uzatadi (Content-Type'ni brauzer boundary bilan qo'yadi).
 */
function checkinForm({ lat, lng, accuracy, occurredAt, photo }: CheckinRequest): FormData {
  const form = new FormData();
  form.append('lat', String(lat));
  form.append('lng', String(lng));
  form.append('accuracy', String(accuracy));
  form.append('occurredAt', occurredAt);
  if (photo) form.append('photo', photo, photo.name);
  return form;
}

export const todayApi = {
  get: (signal?: AbortSignal) =>
    api.get<TodayDto>(STUDENT_ENDPOINTS.today, signal ? { signal } : {}),
  checkin: (body: CheckinRequest) =>
    api.post<TodayDto>(STUDENT_ENDPOINTS.checkin, checkinForm(body)),
  checkout: (body: CheckinRequest) =>
    api.post<TodayDto>(STUDENT_ENDPOINTS.checkout, checkinForm(body)),
};
