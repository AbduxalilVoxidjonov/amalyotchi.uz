import type { StatusKind } from '@/shared/ui';

/** Backend `DiaryStatus`: yuborildi → admin ko'rdi → tasdiqlandi (ball bilan) / qayta yozish. */
export type DiaryStatus = 'submitted' | 'seen' | 'rewrite' | 'approved';
export type DiaryScore = 1 | 2 | 3 | 4 | 5;

export interface DiaryFile {
  name: string;
  /** `/api/files/{id}` */
  url: string;
}

/** GET /api/admin/diaries?status= → DiaryEntry[] (tekshirilmaganlar birinchi) */
export interface DiaryEntry {
  id: string;
  studentId: string;
  studentName: string;
  group: string;
  /** Hisobot kuni (DateOnly). */
  date: string;
  /** ISO 8601 (DateTimeOffset) — haqiqiy yuborilgan vaqt. */
  submittedAt: string;
  /** v3.30: `submittedAt` ning Toshkent sanasi `date` dan keyin (o'tgan kunga kech yuborilgan). */
  submittedLate: boolean;
  status: DiaryStatus;
  /** v3.30: PDF hisobot yuborilganda bo'sh satr bo'lishi mumkin — `isPdfOnlyDiary`. */
  text: string;
  /** "Nimani o'rgandim" (ixtiyoriy). */
  learned: string | null;
  files: DiaryFile[];
  score: number | null;
  /** Admin izohi. */
  comment: string | null;
  reviewedAt: string | null;
}

export type DiaryReviewAction = 'approve' | 'score' | 'rewrite';

/**
 * POST /api/admin/diaries/:id/review → DiaryEntry
 * approve (ball ixtiyoriy) · score (ball 1–5 majburiy; ham tasdiqlaydi) · rewrite (izoh majburiy).
 * Allaqachon ko'rib chiqilgan (approved/rewrite) → 409.
 */
export interface DiaryReviewRequest {
  action: DiaryReviewAction;
  score?: DiaryScore;
  comment?: string;
}

export const DIARY_STATUS_LABEL: Record<DiaryStatus, { label: string; kind: StatusKind }> = {
  submitted: { label: 'Yuborilgan', kind: 'info' },
  seen: { label: "Admin ko'rdi", kind: 'neu' },
  rewrite: { label: 'Qayta yozish kerak', kind: 'late' },
  approved: { label: 'Tasdiqlangan', kind: 'ok' },
};

export const DIARY_SCORES: DiaryScore[] = [1, 2, 3, 4, 5];

/** Matnsiz (faqat PDF) yuborilgan hisobot — kartalarda matn o'rniga "PDF hisobot" va fayl havolasi. */
export function isPdfOnlyDiary(entry: Pick<DiaryEntry, 'text'>): boolean {
  return entry.text.trim().length === 0;
}

export const DIARY_PDF_ONLY_LABEL = 'PDF hisobot';
export const DIARY_LATE_LABEL = 'Kech yuborilgan';

/** Admin amal qila oladigan holatlar (aks holda backend 409). */
export function isDiaryReviewable(entry: Pick<DiaryEntry, 'status'>): boolean {
  return entry.status === 'submitted' || entry.status === 'seen';
}
