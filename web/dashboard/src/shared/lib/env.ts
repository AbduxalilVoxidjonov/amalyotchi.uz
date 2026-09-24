/** Vite muhit o'zgaruvchilari (`.env.example` ga qarang). */
export const env = {
  /** Bo'sh string → relative `/api` (dev proxy yoki bir domen). */
  apiUrl: (import.meta.env.VITE_API_URL as string | undefined)?.replace(/\/+$/, '') ?? '',
  useMocks: import.meta.env.VITE_USE_MOCKS === 'true',
  isDev: import.meta.env.DEV,
  isTest: import.meta.env.MODE === 'test',
} as const;

/**
 * Talaba ilovasi (TWA) manzili — `VITE_TWA_URL` (oxiridagi `/` olib tashlanadi). Bo'sh bo'lsa null:
 * UI havola qatorini ko'rsatmaydi. Funksiya — testlarda `vi.stubEnv` bilan o'zgartirish mumkin bo'lsin.
 */
export function getTwaUrl(): string | null {
  const raw = (import.meta.env.VITE_TWA_URL as string | undefined)?.trim().replace(/\/+$/, '');
  return raw ? raw : null;
}
