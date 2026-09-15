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
 */
export const authApi = {
  login: (body: LoginRequest) =>
    api.post<AuthResultDto>(AUTH_ENDPOINTS.login, body, { auth: false }),

  logout: (body: LogoutRequest) =>
    api.post<void>(AUTH_ENDPOINTS.logout, body, { retryOn401: false }),

  me: () => api.get<UserSummaryDto>(AUTH_ENDPOINTS.me),

  refresh: refreshSession,
};
