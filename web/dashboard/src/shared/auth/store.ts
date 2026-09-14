import { createAuthStore } from '@amaliyotchi/shared/auth';

/**
 * Dashboard auth store.
 * - accessToken — faqat xotirada.
 * - refreshToken — backend body'da qaytaradi (cookie emas), sahifa yangilanganda
 *   sessiya saqlanishi uchun localStorage'da turadi (kalit: amaliyotchi.dashboard.refreshToken).
 */
export const useAuthStore = createAuthStore({
  refreshTokenStorage: typeof window !== 'undefined' ? window.localStorage : null,
  storageKey: 'amaliyotchi.dashboard.refreshToken',
});
