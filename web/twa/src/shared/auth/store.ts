import { createAuthStore } from '@amaliyotchi/shared/auth';

/**
 * TWA auth store. Telegram ichida sessiya initData orqali har ochilishda qayta olinadi,
 * shuning uchun refresh token faqat sessionStorage'da (yopilganda o'chadi).
 */
export const useAuthStore = createAuthStore({
  refreshTokenStorage: typeof window !== 'undefined' ? window.sessionStorage : null,
  storageKey: 'amaliyotchi.twa.refreshToken',
});
