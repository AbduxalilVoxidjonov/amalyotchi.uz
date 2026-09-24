import { createApiClient } from '@amaliyotchi/shared';
import { applyAuthResult, type TwaAuthResult } from '@/shared/auth/session';
import { useAuthStore } from '@/shared/auth/store';
import { env } from '@/shared/lib/env';
import { AUTH_ENDPOINTS } from './endpoints';

// Auth endpoint'lari `endpoints.ts` da (bitta joy); mavjud importlar uchun qayta eksport.
export { AUTH_ENDPOINTS };

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
