import type { LoginRequest, UserSummaryDto } from '@amaliyotchi/shared';
import { api, AUTH_ENDPOINTS, refreshSession } from '@/shared/api/client';
import type { TwaAuthResult } from '@/shared/auth/session';

/** POST /api/auth/telegram { initData } → AuthResultDto · 403 ProblemDetails (imzo noto'g'ri / hisob bog'lanmagan / faol emas). */
export interface TelegramAuthRequest {
  initData: string;
}

/**
 * POST /api/auth/telegram/link — birinchi kirish: Telegram akkauntini HEMIS ID + parol bilan bog'lash.
 * 200 → AuthResultDto (login bilan bir xil) · 403 / 409 / 429 ProblemDetails `detail`.
 */
export interface TelegramLinkRequest extends LoginRequest {
  initData: string;
}

/**
 * POST /api/auth/change-password → 204 · 400 ValidationProblem (`errors.CurrentPassword` / `errors.NewPassword`).
 * `refreshToken` — joriy sessiyaniki: berilsa saqlanadi, qolgan sessiyalar bekor (berilmasa — hammasi bekor).
 */
export interface ChangePasswordRequest {
  currentPassword: string;
  newPassword: string;
  refreshToken?: string;
}

export const authApi = {
  loginWithTelegram: (body: TelegramAuthRequest) =>
    api.post<TwaAuthResult>(AUTH_ENDPOINTS.telegram, body, { auth: false }),
  /** Telegram ichida birinchi kirish: initData + HEMIS ID + parol → akkaunt bog'lanadi va sessiya beriladi. */
  linkTelegram: (body: TelegramLinkRequest) =>
    api.post<TwaAuthResult>(AUTH_ENDPOINTS.telegramLink, body, { auth: false }),
  /** Web-login (Telegram tashqarisida): HEMIS ID + parol. */
  login: (body: LoginRequest) =>
    api.post<TwaAuthResult>(AUTH_ENDPOINTS.login, body, { auth: false }),
  /** Refresh token'ni bekor qilish. `accessToken` berilsa — shu token bilan (sessiya store'ga yozilmagan holat). */
  logout: (refreshToken: string, accessToken?: string) =>
    api.post<void>(
      AUTH_ENDPOINTS.logout,
      { refreshToken },
      accessToken
        ? { auth: false, headers: { Authorization: `Bearer ${accessToken}` } }
        : { retryOn401: false },
    ),
  changePassword: (body: ChangePasswordRequest) =>
    api.post<void>(AUTH_ENDPOINTS.changePassword, body),
  me: () => api.get<UserSummaryDto>(AUTH_ENDPOINTS.me),
  refresh: refreshSession,
};
