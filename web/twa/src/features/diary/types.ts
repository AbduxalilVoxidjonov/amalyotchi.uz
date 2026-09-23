import type { StatusKind } from '@amaliyotchi/shared/ui';

/** Domain `DiaryStatus`: yuborildi → tyutor ko'rdi → tasdiqlandi (ball bilan) / qayta yozish. */
export type DiaryStatus = 'submitted' | 'seen' | 'rewrite' | 'approved';

export interface DiaryFileDto {
  id: string;
  name: string;
  url: string;
}

/** GET /api/student/diary (`DiaryEntryDto`) — faqat o'z yozuvlari, yangisi birinchi. */
export interface DiaryEntryDto {
  id: string;
  /** DateOnly — hisobot kuni. */
  date: string;
  /** ISO datetime — UI: "11.10.2026 · 17:42" */
  submittedAt: string;
  status: DiaryStatus;
  text: string;
  learned: string | null;
  files: DiaryFileDto[];
  /** 1–5 (tasdiqlanganda) */
  score: number | null;
  /** Tyutor izohi (qayta yozish / tasdiqlash). */
  comment: string | null;
}

/** POST /api/student/diary — multipart/form-data: text, learned?, files[] (0–5, ≤5 MB, rasm/PDF). */
export interface DiaryCreate {
  text: string;
  learned: string;
  files: File[];
}

export const DIARY_STATUS: Record<DiaryStatus, { label: string; kind: StatusKind }> = {
  submitted: { label: 'Yuborilgan', kind: 'info' },
  seen: { label: "Tyutor ko'rdi", kind: 'neu' },
  rewrite: { label: 'Qayta yozish kerak', kind: 'late' },
  approved: { label: 'Tasdiqlangan', kind: 'ok' },
};

export const DIARY_MIN_CHARS = 150;
export const DIARY_MAX_FILES = 5;
/** Backend `CreateDiaryEntryCommandValidator.AllowedContentTypes`. */
export const DIARY_FILE_ACCEPT = 'image/jpeg,image/png,image/webp,image/heic,image/heif,.pdf';

export const DIARY_PDF_REQUIRED_MESSAGE = 'Hisobotga PDF fayl biriktirilishi shart.';

/** Backend qoidasi: PDF = content-type `application/pdf` yoki `.pdf` kengaytma. */
export function isPdfFile(file: { name: string; type?: string | undefined }): boolean {
  return file.type?.toLowerCase() === 'application/pdf' || /\.pdf$/i.test(file.name.trim());
}

/**
 * Bugungi yozuv `rewrite` holatida bo'lsa, qayta yuborishda uning fayllari saqlanadi
 * (backend `Resubmit` fayllarni qo'shadi) — PDF talabi shu fayllar bilan ham bajariladi.
 */
export function rewriteFilesFor(
  entries: readonly DiaryEntryDto[] | undefined,
  date: string | undefined,
): DiaryFileDto[] {
  if (!entries || !date) return [];
  return entries.find((e) => e.date === date && e.status === 'rewrite')?.files ?? [];
}
