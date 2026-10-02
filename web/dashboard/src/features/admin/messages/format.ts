import type { MessageSummary } from './types';

/** Ishlangan ulush (yetkazildi + xato + bloklangan) — 0..100. */
export function processedPct(m: Pick<MessageSummary, 'total' | 'pending'>): number {
  if (m.total <= 0) return 0;
  return ((m.total - m.pending) / m.total) * 100;
}

/** Ro'yxatda matnning qisqa ko'rinishi (bir qator, bo'shliqlar siqiladi). */
export function shortText(text: string, max = 80): string {
  const line = text.replace(/\s+/g, ' ').trim();
  return line.length > max ? `${line.slice(0, max - 1)}…` : line;
}
