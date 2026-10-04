import { http, HttpResponse, type HttpHandler } from 'msw';
import { problem } from '@/mocks/data';
import { mockStudents } from '../students/mocks';
import { FACE_ENROLLMENTS_ENDPOINT } from './api';
import { mockFaceEntries, mockFaceOf, setMockFace } from './mockStore';
import {
  FACE_REJECT_REASON_REQUIRED,
  isFaceTab,
  type FaceEnrollment,
  type FaceEnrollmentListResponse,
} from './types';

/** Mock "hozir" — tasdiqlash/rad etish vaqti (`reviewedAt`). */
const MOCK_NOW = '2026-10-12T10:15:00+05:00';

const json = (status: number, title: string, detail: string, extra?: object) =>
  HttpResponse.json(problem(status, title, detail, extra), {
    status,
    headers: { 'Content-Type': 'application/problem+json' },
  });

/** Sidebar badge'i uchun — tekshiruv kutayotganlar soni (`GET /api/tutor/nav`). */
export function pendingFaceCount(): number {
  return mockFaceEntries().filter(([, f]) => f.status === 'pending').length;
}

export function mockFaceEnrollments(status: string): FaceEnrollment[] {
  return mockFaceEntries()
    .filter(([, f]) => f.status === status)
    .flatMap(([studentId, f]) => {
      const s = mockStudents.find((x) => x.id === studentId);
      if (!s) return [];
      return [
        {
          studentId,
          fullName: s.name,
          hemisId: s.hemisId,
          group: s.group,
          photoUrl: f.photoUrl,
          status: f.status,
          submittedAt: f.submittedAt,
          reviewedAt: f.reviewedAt,
          rejectReason: f.rejectReason,
        },
      ];
    })
    .sort((a, b) => (b.submittedAt ?? '').localeCompare(a.submittedAt ?? ''));
}

/** Talaba ko'lamda bo'lmasa — 404; aks holda joriy holat. */
function scoped(id: string) {
  return mockStudents.some((s) => s.id === id) ? mockFaceOf(id) : null;
}

const notFound = () => json(404, 'Topilmadi', 'Talaba topilmadi.');

export const faceHandlers: HttpHandler[] = [
  http.get(FACE_ENROLLMENTS_ENDPOINT, ({ request }) => {
    const raw = new URL(request.url).searchParams.get('status');
    const status = isFaceTab(raw) ? raw : 'pending';
    const body: FaceEnrollmentListResponse = { items: mockFaceEnrollments(status) };
    return HttpResponse.json(body);
  }),

  http.post('/api/tutor/students/:id/face/approve', ({ params }) => {
    const id = String(params['id']);
    const face = scoped(id);
    if (!face) return notFound();
    if (face.status !== 'pending')
      return json(409, 'Ziddiyat', "Tekshiruvni kutayotgan yuz rasmi yo'q.");
    const next = { ...face, status: 'approved' as const, reviewedAt: MOCK_NOW, rejectReason: null };
    setMockFace(id, next);
    return HttpResponse.json(next);
  }),

  http.post('/api/tutor/students/:id/face/reject', async ({ params, request }) => {
    const id = String(params['id']);
    const face = scoped(id);
    if (!face) return notFound();
    const body = (await request.json().catch(() => ({}))) as { reason?: string };
    const reason = (body.reason ?? '').trim();
    if (!reason)
      return json(400, "Ma'lumotlar noto'g'ri", FACE_REJECT_REASON_REQUIRED, {
        errors: { Reason: [FACE_REJECT_REASON_REQUIRED] },
      });
    if (face.status !== 'pending')
      return json(409, 'Ziddiyat', "Tekshiruvni kutayotgan yuz rasmi yo'q.");
    const next = {
      ...face,
      status: 'rejected' as const,
      reviewedAt: MOCK_NOW,
      rejectReason: reason,
    };
    setMockFace(id, next);
    return HttpResponse.json(next);
  }),

  http.post('/api/tutor/students/:id/face/reset', ({ params }) => {
    const id = String(params['id']);
    const face = scoped(id);
    if (!face) return notFound();
    const next = {
      ...face,
      status: 'none' as const,
      photoUrl: null,
      submittedAt: null,
      reviewedAt: null,
      rejectReason: null,
    };
    setMockFace(id, next);
    return HttpResponse.json(next);
  }),
];
