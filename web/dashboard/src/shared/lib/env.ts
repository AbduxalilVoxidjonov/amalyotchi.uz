/** Vite muhit o'zgaruvchilari (`.env.example` ga qarang). */
export const env = {
  /** Bo'sh string → relative `/api` (dev proxy yoki bir domen). */
  apiUrl: (import.meta.env.VITE_API_URL as string | undefined)?.replace(/\/+$/, '') ?? '',
  useMocks: import.meta.env.VITE_USE_MOCKS === 'true',
  isDev: import.meta.env.DEV,
  isTest: import.meta.env.MODE === 'test',
} as const;
