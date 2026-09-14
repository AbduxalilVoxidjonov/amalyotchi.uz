import { createApiClient, type AuthResultDto } from '@amaliyotchi/shared';
import { useAuthStore } from '@/shared/auth/store';
import { env } from '@/shared/lib/env';

export const AUTH_ENDPOINTS = {
  /** POST /api/auth/telegram { initData } → AuthResultDto | 403 (TelegramLoginCommand). */
  telegram: '/api/auth/telegram',
  refresh: '/api/auth/refresh',
  me: '/api/auth/me',
} as const;

export async function refreshSession(): Promise<string | null> {
  const { refreshToken, setSession } = useAuthStore.getState();
  if (!refreshToken) return null;
  try {
    const result = await api.post<AuthResultDto>(
      AUTH_ENDPOINTS.refresh,
      { refreshToken },
      { auth: false },
    );
    setSession(result);
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
