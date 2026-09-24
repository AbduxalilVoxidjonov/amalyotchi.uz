import { getInitData, isInsideTelegram } from '@/shared/auth/telegram';
import { env } from '@/shared/lib/env';

/**
 * Kirish usuli:
 *  - `telegram` — haqiqiy Telegram ichida, yoki dev initData / mock rejim (mavjud xatti-harakat);
 *  - `web` — oddiy brauzer (initData yo'q) yoki `?web=1` / `VITE_WEB_LOGIN=true` bilan majburan.
 */
export type AuthMode = 'telegram' | 'web';

export function authMode(): AuthMode {
  if (isInsideTelegram()) return 'telegram';
  if (env.forceWebLogin) return 'web';
  if (getInitData() || env.useMocks) return 'telegram';
  return 'web';
}
