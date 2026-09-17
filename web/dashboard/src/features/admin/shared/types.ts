import type { QueryParams } from '@amaliyotchi/shared';

/** Ro'yxat so'rovlari uchun umumiy parametrlar (`?q=&page=&pageSize=`). */
export interface ListParams {
  q?: string;
  page?: number;
  pageSize?: number;
}

/** Backend'dan kutiladigan sahifalangan javob shakli (barcha admin ro'yxatlari). */
export interface Paged<T> {
  items: T[];
  page: number;
  pageSize: number;
  total: number;
}

export const DEFAULT_PAGE_SIZE = 20;

/** `api.get(..., { query })` uchun — `QueryParams` index signature talab qiladi; bo'sh `q` yuborilmaydi. */
export function toQuery(params: ListParams): QueryParams {
  return { q: params.q || undefined, page: params.page, pageSize: params.pageSize };
}

/* ────────────────────────────────────────────────────────────────────────────
 * Excel import (talabalar va korxonalar uchun bir xil shakl — backend `ImportResult`).
 * ──────────────────────────────────────────────────────────────────────────── */

/** Qabul qilinmagan qator (bir qatorda bir nechta xato bo'lsa — bir nechta yozuv). */
export interface ImportError {
  /** Exceldagi qator raqami. */
  row: number;
  /** Ustun nomi ("FISH", "STIR", "Manzil", ...). */
  column: string;
  value: string | null;
  message: string;
}

/** Xato qatorlar tashlab yuboriladi, to'g'rilari saqlanadi (`created + failed = totalRows`). */
export interface ImportResult {
  totalRows: number;
  created: number;
  failed: number;
  errors: ImportError[];
}
