import type {
  AuthResultDto,
  LoginRequest,
  LogoutRequest,
  UserSummaryDto,
} from '@amaliyotchi/shared';
import { api, AUTH_ENDPOINTS, refreshSession } from '@/shared/api/client';

/**
 * Auth endpoint'lari — src/Amaliyotchi.Api/Controllers/AuthController.cs
 *   POST /api/auth/login   {hemisId,password} → AuthResultDto | 403 ProblemDetails
 *   POST /api/auth/refresh {refreshToken}         → AuthResultDto | 403
 *   POST /api/auth/logout  {refreshToken} Bearer  → 204
 *   GET  /api/auth/me      Bearer                 → UserSummaryDto
 *   POST /api/auth/change-password {currentPassword,newPassword,refreshToken?} → 204 | 400
 *   GET  /api/auth/login-available?login=          → LoginAvailability (format xatosi ham 200 + reason)
 *   POST /api/auth/change-login {newLogin,currentPassword} → 200 UserSummaryDto | 400 | 409
 */
export const ACCOUNT_ENDPOINTS = {
  changePassword: '/api/auth/change-password',
  loginAvailable: '/api/auth/login-available',
  changeLogin: '/api/auth/change-login',
} as const;

/**
 * `ChangePasswordCommand`: 204. 400 `errors.CurrentPassword` ("Joriy parol noto'g'ri.") /
 * `errors.NewPassword` (8–128 belgi, joriydan farqli). Muvaffaqiyatda boshqa sessiyalarning refresh
 * tokenlari bekor qilinadi; `refreshToken` berilsa — joriy sessiya saqlanadi (berilmasa hammasi bekor).
 */
export interface ChangePasswordRequest {
  currentPassword: string;
  newPassword: string;
  refreshToken?: string;
}

export interface LoginAvailability {
  available: boolean;
  /** Server saqlaydigan ko'rinish (masalan, bo'shliqlarsiz). */
  normalized: string;
  /** `available=false` bo'lsa sabab (band / format); aks holda null. */
  reason: string | null;
}

/** 400 `errors.CurrentPassword` / `errors.NewLogin` · 409 "Bu login allaqachon band.". */
export interface ChangeLoginRequest {
  newLogin: string;
  currentPassword: string;
}

export const authApi = {
  login: (body: LoginRequest) =>
    api.post<AuthResultDto>(AUTH_ENDPOINTS.login, body, { auth: false }),

  logout: (body: LogoutRequest) =>
    api.post<void>(AUTH_ENDPOINTS.logout, body, { retryOn401: false }),

  /** Store'ga yozilmagan sessiyani yopish (talaba roli rad etilganda) — token qo'lda beriladi. */
  logoutWithToken: (accessToken: string, body: LogoutRequest) =>
    api.post<void>(AUTH_ENDPOINTS.logout, body, {
      auth: false,
      retryOn401: false,
      headers: { Authorization: `Bearer ${accessToken}` },
    }),

  me: () => api.get<UserSummaryDto>(AUTH_ENDPOINTS.me),

  changePassword: (body: ChangePasswordRequest) =>
    api.post<void>(ACCOUNT_ENDPOINTS.changePassword, body),

  loginAvailable: (login: string, signal?: AbortSignal) =>
    api.get<LoginAvailability>(ACCOUNT_ENDPOINTS.loginAvailable, {
      query: { login },
      ...(signal ? { signal } : {}),
    }),

  changeLogin: (body: ChangeLoginRequest) =>
    api.post<UserSummaryDto>(ACCOUNT_ENDPOINTS.changeLogin, body),

  refresh: refreshSession,
};
