import { api } from '@/shared/api';
import type { FaceEnrollmentListResponse, FaceEnrollmentTab, StudentFace } from './types';

/**
 * Tyutor · Yuzni tasdiqlash (kontrakt §6.33):
 *   GET  /api/tutor/face-enrollments?status=pending|approved|rejected → { items }   (sukut: pending)
 *   POST /api/tutor/students/:id/face/approve                         → StudentFaceDto
 *   POST /api/tutor/students/:id/face/reject { reason }               → StudentFaceDto | 400 `errors.Reason`
 *   POST /api/tutor/students/:id/face/reset                           → StudentFaceDto (status = none)
 * Holat ziddiyati (masalan, allaqachon ko'rib chiqilgan) → 409 `detail`.
 */
export const FACE_ENROLLMENTS_ENDPOINT = '/api/tutor/face-enrollments';

export const studentFaceBase = (studentId: string) =>
  `/api/tutor/students/${encodeURIComponent(studentId)}/face`;

export const faceApi = {
  list: (params: { status: FaceEnrollmentTab }) =>
    api.get<FaceEnrollmentListResponse>(FACE_ENROLLMENTS_ENDPOINT, {
      query: { status: params.status },
    }),
  approve: (studentId: string) => api.post<StudentFace>(`${studentFaceBase(studentId)}/approve`),
  reject: (studentId: string, reason: string) =>
    api.post<StudentFace>(`${studentFaceBase(studentId)}/reject`, { reason }),
  reset: (studentId: string) => api.post<StudentFace>(`${studentFaceBase(studentId)}/reset`),
};
