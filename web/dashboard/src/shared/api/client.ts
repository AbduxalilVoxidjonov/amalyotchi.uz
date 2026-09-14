import { createApiClient, type AuthResultDto, type RefreshRequest } from '@amaliyotchi/shared';
import { useAuthStore } from '@/shared/auth/store';
import { env } from '@/shared/lib/env';

export const AUTH_ENDPOINTS = {
  login: '/api/auth/login',
  refresh: '/api/auth/refresh',
  logout: '/api/auth/logout',
  me: '/api/auth/me',
} as const;

/**
 * Store'dagi refresh token bilan yangi sessiya oladi. Muvaffaqiyatsiz bo'lsa null.
 * `auth: false` — Authorization yuborilmaydi va 401 da rekursiv refresh bo'lmaydi.
 */
export async function refreshSession(): Promise<string | null> {
  const { refreshToken, setSession } = useAuthStore.getState();
  if (!refreshToken) return null;
  try {
    const result = await api.post<AuthResultDto>(
      AUTH_ENDPOINTS.refresh,
      { refreshToken } satisfies RefreshRequest,
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
export type { ApiClient, RequestOptions, ProblemDetails } from '@amaliyotchi/shared';
