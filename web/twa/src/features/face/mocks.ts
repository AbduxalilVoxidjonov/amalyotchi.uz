import { http, HttpResponse, type HttpHandler } from 'msw';
import { STUDENT_ENDPOINTS } from '@/shared/api/endpoints';
import { problem, requireBearer } from '@/mocks/problem';
import { PHOTO_CONTENT_TYPES, PHOTO_MAX_BYTES } from '@/features/today/photo';
import type { StudentFaceDto } from './types';

/** Backend xabarlari (kontrakt v3.27 §6.33). */
export const FACE_MESSAGES = {
  consent: 'Rozilik berilishi kerak.',
  noFace: "Rasmda yuz topilmadi. Yuzingiz aniq ko'rinadigan, yorug' joyda qayta suratga oling.",
  manyFaces: "Rasmda faqat bitta yuz bo'lishi kerak.",
  alreadyApproved: 'Yuz rasmingiz allaqachon tasdiqlangan.',
} as const;

/** Mock etalon rasmi manzili (`/api/files/{id}`). */
export const MOCK_FACE_PHOTO_URL = '/api/files/face-mock-0001';

/** Sukut: tekshiruv o'chiq (`required=false`) — mavjud testlar darvozasiz ishlaydi. */
function initialFace(): StudentFaceDto {
  return {
    status: 'none',
    required: false,
    photoUrl: null,
    submittedAt: null,
    reviewedAt: null,
    rejectReason: null,
  };
}

export let mockFace: StudentFaceDto = initialFace();

/** Oxirgi yuborilgan etalon (test tekshiruvi uchun). */
export let lastFaceSubmission: { name: string; type: string; consent: string | null } | null = null;

export function setMockFace(patch: Partial<StudentFaceDto>) {
  mockFace = { ...mockFace, ...patch };
}

export function resetFaceMocks() {
  mockFace = initialFace();
  lastFaceSubmission = null;
}

const invalid = (field: 'Photo' | 'Consent', message: string) =>
  problem(400, "Ma'lumotlar noto'g'ri", message, { errors: { [field]: [message] } });

/**
 * GET/POST /api/student/face. Yuz aniqlash mock'da fayl nomi bo'yicha: `noface*` → "yuz topilmadi",
 * `twofaces*` → "faqat bitta yuz". Tasdiqlangan holatda POST → 409.
 */
export const faceHandlers: HttpHandler[] = [
  http.get(STUDENT_ENDPOINTS.face, ({ request }) => {
    return requireBearer(request) ?? HttpResponse.json(mockFace);
  }),

  http.post(STUDENT_ENDPOINTS.face, async ({ request }) => {
    const denied = requireBearer(request);
    if (denied) return denied;
    if (mockFace.status === 'approved') {
      return problem(409, 'Ziddiyat', FACE_MESSAGES.alreadyApproved);
    }
    const data = await request.formData().catch(() => null);
    const consent = data?.get('consent');
    const raw = data?.get('photo');
    const photo = raw instanceof File && raw.size > 0 ? raw : null;
    if (consent !== 'true') return invalid('Consent', FACE_MESSAGES.consent);
    if (!photo) return invalid('Photo', 'Rasm yuborilmadi.');
    if (!(PHOTO_CONTENT_TYPES as readonly string[]).includes(photo.type.toLowerCase())) {
      return invalid('Photo', 'Rasm formati qabul qilinmaydi (JPEG, PNG, WEBP, HEIC).');
    }
    if (photo.size > PHOTO_MAX_BYTES)
      return invalid('Photo', 'Rasm hajmi 5 MB dan oshmasligi kerak.');
    if (photo.name.startsWith('noface')) return invalid('Photo', FACE_MESSAGES.noFace);
    if (photo.name.startsWith('twofaces')) return invalid('Photo', FACE_MESSAGES.manyFaces);

    lastFaceSubmission = { name: photo.name, type: photo.type, consent };
    mockFace = {
      ...mockFace,
      status: 'pending',
      photoUrl: MOCK_FACE_PHOTO_URL,
      submittedAt: new Date().toISOString(),
      reviewedAt: null,
      rejectReason: null,
    };
    return HttpResponse.json(mockFace);
  }),

  // Himoyalangan fayllar (`/api/files/{id}`) — 1×1 PNG.
  http.get('/api/files/:id', ({ request }) => {
    const denied = requireBearer(request);
    if (denied) return denied;
    const png = Uint8Array.from(
      atob(
        'iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mNkYAAAAAYAAjCB0C8AAAAASUVORK5CYII=',
      ),
      (c) => c.charCodeAt(0),
    );
    return new HttpResponse(png, { headers: { 'Content-Type': 'image/png' } });
  }),
];
