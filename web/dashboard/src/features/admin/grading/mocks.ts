import { http, HttpResponse, type HttpHandler } from 'msw';
import { problem } from '@/mocks/data';
import type { GradingRow, GradingUpdateRequest } from './types';

type Seed = Omit<GradingRow, 'total' | 'grade'>;

/** SPEC-SCREENS §9.2 mock (5 qator). total/grade — hisoblanadi. */
const SPEC: Seed[] = [
  {
    studentId: 's-341033',
    name: 'Yusupova Nilufar',
    attendance: { points: 40, pct: 100 },
    reports: { points: 28.8, avg: 4.8 },
    tutorPoints: 19,
    referencePoints: 10,
    recommended: { tutorPoints: 19, referencePoints: 10 },
  },
  {
    studentId: 's-341030',
    name: 'Aliyev Akmal',
    attendance: { points: 37.6, pct: 94 },
    reports: { points: 25.2, avg: 4.2 },
    tutorPoints: 18,
    referencePoints: 10,
    recommended: { tutorPoints: 18, referencePoints: 10 },
  },
  {
    studentId: 's-341035',
    name: 'Toshpulatova Zarina',
    attendance: { points: 35.6, pct: 89 },
    reports: { points: 24, avg: 4.0 },
    tutorPoints: 16,
    referencePoints: 8,
    recommended: { tutorPoints: 16, referencePoints: 8 },
  },
  {
    studentId: 's-341034',
    name: 'Rahimov Sardor',
    attendance: { points: 31.2, pct: 78 },
    reports: { points: 21, avg: 3.5 },
    tutorPoints: 13,
    referencePoints: 7,
    recommended: { tutorPoints: 13, referencePoints: 7 },
  },
  {
    studentId: 's-341032',
    name: 'Sobirov Diyor',
    attendance: { points: 25.6, pct: 64 },
    reports: { points: 15.5, avg: 3.1 },
    tutorPoints: null,
    referencePoints: null,
    recommended: { tutorPoints: 10, referencePoints: 5 },
  },
];

/**
 * Backend `GradeCalculator`: jami = davomat + hisobot + tyutor + tavsifnoma (null → 0);
 * davomat < 70% → null (qayta topshiradi); ≥86 → 5, ≥71 → 4, ≥56 → 3, aks holda 2.
 */
function compute(seed: Seed): GradingRow {
  const total =
    Math.round(
      (seed.attendance.points +
        seed.reports.points +
        (seed.tutorPoints ?? 0) +
        (seed.referencePoints ?? 0)) *
        10,
    ) / 10;
  const grade: GradingRow['grade'] =
    seed.attendance.pct < 70 ? null : total >= 86 ? 5 : total >= 71 ? 4 : total >= 56 ? 3 : 2;
  return { ...seed, total, grade };
}

export let mockGrading: GradingRow[] = [];
export function resetGradingMock() {
  mockGrading = SPEC.map((s) => compute({ ...s }));
}
resetGradingMock();

export const gradingHandlers: HttpHandler[] = [
  http.get('/api/tutor/grading', () => HttpResponse.json(mockGrading)),

  http.put('/api/tutor/grading/:studentId', async ({ params, request }) => {
    const idx = mockGrading.findIndex((r) => r.studentId === params['studentId']);
    const row = mockGrading[idx];
    if (!row)
      return HttpResponse.json(problem(404, 'Topilmadi', 'Talaba topilmadi.'), { status: 404 });
    const body = (await request.json().catch(() => ({}))) as Partial<GradingUpdateRequest>;
    const inRange = (v: unknown, max: number) =>
      v === null || (typeof v === 'number' && v >= 0 && v <= max);
    if (!inRange(body.tutorPoints, 20) || !inRange(body.referencePoints, 10)) {
      return HttpResponse.json(
        problem(400, 'One or more validation errors occurred.', '', {
          errors: {
            TutorPoints: ["Tyutor bali 0–20 oralig'ida bo'lishi kerak."],
            ReferencePoints: ["Tavsifnoma bali 0–10 oralig'ida bo'lishi kerak."],
          },
        }),
        { status: 400 },
      );
    }
    const next = compute({
      ...row,
      tutorPoints: body.tutorPoints ?? null,
      referencePoints: body.referencePoints ?? null,
    });
    mockGrading[idx] = next;
    return HttpResponse.json(next);
  }),
];
