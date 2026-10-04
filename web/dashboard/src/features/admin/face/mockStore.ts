import type { StudentFace } from './types';

/**
 * Talabalar etalon yuz rasmi holati (mock, in-memory) — bog'liqliksiz modul: talaba profili mock'i
 * (`students/mocks.ts`, `detail.face`) va yuz ro'yxati mock'i (`face/mocks.ts`) shu yerdan o'qiydi.
 * Kalit — `mockStudents` id'si. Ro'yxatda yo'q talaba — `none`.
 */
function seed(): Map<string, StudentFace> {
  return new Map<string, StudentFace>([
    [
      's-341030',
      {
        status: 'approved',
        required: true,
        photoUrl: '/api/files/face-s-341030',
        submittedAt: '2026-10-01T08:12:00+05:00',
        reviewedAt: '2026-10-01T10:40:00+05:00',
        rejectReason: null,
      },
    ],
    [
      's-341031',
      {
        status: 'pending',
        required: true,
        photoUrl: '/api/files/face-s-341031',
        submittedAt: '2026-10-11T09:05:00+05:00',
        reviewedAt: null,
        rejectReason: null,
      },
    ],
    [
      's-341032',
      {
        status: 'pending',
        required: true,
        photoUrl: '/api/files/face-s-341032',
        submittedAt: '2026-10-12T07:48:00+05:00',
        reviewedAt: null,
        rejectReason: null,
      },
    ],
    [
      's-341033',
      {
        status: 'rejected',
        required: true,
        photoUrl: '/api/files/face-s-341033',
        submittedAt: '2026-10-10T18:20:00+05:00',
        reviewedAt: '2026-10-11T09:00:00+05:00',
        rejectReason: "Yuz to'liq ko'rinmaydi — yorug' joyda qayta suratga oling.",
      },
    ],
  ]);
}

let faces = seed();

const NONE: StudentFace = {
  status: 'none',
  required: true,
  photoUrl: null,
  submittedAt: null,
  reviewedAt: null,
  rejectReason: null,
};

export function mockFaceOf(studentId: string): StudentFace {
  return { ...(faces.get(studentId) ?? NONE) };
}

export function setMockFace(studentId: string, face: StudentFace) {
  faces.set(studentId, face);
}

export function mockFaceEntries(): [string, StudentFace][] {
  return [...faces.entries()];
}

export function resetFaceMock() {
  faces = seed();
}
