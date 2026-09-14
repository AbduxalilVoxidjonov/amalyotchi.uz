import type { AuthResultDto, UserSummaryDto } from '@amaliyotchi/shared';
import { api, AUTH_ENDPOINTS, refreshSession } from '@/shared/api/client';

/** POST /api/auth/telegram { initData } → AuthResultDto · 403 ProblemDetails (imzo noto'g'ri / hisob bog'lanmagan / faol emas). */
export interface TelegramAuthRequest {
  initData: string;
}

export const authApi = {
  loginWithTelegram: (body: TelegramAuthRequest) =>
    api.post<AuthResultDto>(AUTH_ENDPOINTS.telegram, body, { auth: false }),
  me: () => api.get<UserSummaryDto>(AUTH_ENDPOINTS.me),
  refresh: refreshSession,
};
