import { api } from '@/shared/api/client';
import { STUDENT_ENDPOINTS } from '@/shared/api/endpoints';
import type { StudentFaceDto } from './types';

/** Etalon yuborish — multipart/form-data: `photo` (fayl) + `consent` ("true"). */
function faceForm(photo: File, consent: boolean): FormData {
  const form = new FormData();
  form.append('photo', photo, photo.name);
  form.append('consent', String(consent));
  return form;
}

export const faceApi = {
  get: (signal?: AbortSignal) =>
    api.get<StudentFaceDto>(STUDENT_ENDPOINTS.face, signal ? { signal } : {}),
  submit: ({ photo, consent }: { photo: File; consent: boolean }) =>
    api.post<StudentFaceDto>(STUDENT_ENDPOINTS.face, faceForm(photo, consent)),
};
