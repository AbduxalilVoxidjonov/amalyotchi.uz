import type { StatusKind } from '@/shared/ui';

/**
 * GET /api/tutor/grading → GradingRow[] (backend `GradingRow`).
 * Davomat 40 + hisobotlar 30 + tyutor 20 + tavsifnoma 10 → jami → baho (2–5; null = qayta topshiradi).
 */
export interface GradingRow {
  studentId: string;
  name: string;
  /** Davomat · 40 ball: { points: 37.6, pct: 94 } */
  attendance: { points: number; pct: number };
  /** Hisobot · 30 ball: { points: 25.2, avg: 4.2 } */
  reports: { points: number; avg: number };
  /** Tyutor · 20 (null → "—", hali qo'yilmagan) */
  tutorPoints: number | null;
  /** Tavsifnoma · 10 */
  referencePoints: number | null;
  /** Tizim tavsiyasi (qabul qilish tugmasi uchun). */
  recommended: { tutorPoints: number; referencePoints: number };
  total: number;
  /** 2–5 yoki null (qayta topshiradi — davomat < 70% yoki jami < 56). */
  grade: number | null;
}

/** PUT /api/tutor/grading/:studentId → GradingRow (0–20 / 0–10; 404 ko'lamdan tashqari; 400 faol davr yo'q; 409 yakunlangan) */
export interface GradingUpdateRequest {
  tutorPoints: number | null;
  referencePoints: number | null;
}

export function gradeLabel(row: GradingRow): { label: string; kind: StatusKind } {
  if (row.grade === null) return { label: 'Qayta topshiradi', kind: 'bad' };
  if (row.grade >= 4) return { label: String(row.grade), kind: 'ok' };
  if (row.grade === 3) return { label: '3', kind: 'late' };
  return { label: String(row.grade), kind: 'bad' };
}
