import { createApiClient } from '@amaliyotchi/shared';
import { applyAuthResult, type TwaAuthResult } from '@/shared/auth/session';
import { useAuthStore } from '@/shared/auth/store';
import { env } from '@/shared/lib/env';

export const AUTH_ENDPOINTS = {
  /** POST /api/auth/telegram { initData } → AuthResultDto | 403 (TelegramLoginCommand). */
  telegram: '/api/auth/telegram',
  /** POST { hemisId, password } → AuthResultDto (+ mustChangePassword) | 401/403 `detail` (web-login). */
  login: '/api/auth/login',
  refresh: '/api/auth/refresh',
  /** POST { refreshToken } + Bearer → 204. */
  logout: '/api/auth/logout',
  /** POST { currentPassword, newPassword } + Bearer → 204 | 400 errors.currentPassword/newPassword. */
  changePassword: '/api/auth/change-password',
  me: '/api/auth/me',
} as const;

export async function refreshSession(): Promise<string | null> {
  const { refreshToken } = useAuthStore.getState();
  if (!refreshToken) return null;
  try {
    const result = await api.post<TwaAuthResult>(
      AUTH_ENDPOINTS.refresh,
      { refreshToken },
      { auth: false },
    );
    applyAuthResult(result);
    return result.accessToken;
  } catch {
    return null;
  }
}

export const api = createApiClient({
  baseUrl: env.apiUrl,
  getAccessToken: () => useAuthStore.getState().accessToken,
  refreshSession,
  onSessionExpired: () => useAuthStore.getState().clear(),
});

export { ApiError, isApiError, errorMessage } from '@amaliyotchi/shared';
