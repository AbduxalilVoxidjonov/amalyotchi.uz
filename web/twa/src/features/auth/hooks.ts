import { useMutation } from '@tanstack/react-query';
import { useEffect, useRef } from 'react';
import { useAuthStore } from '@/shared/auth/store';
import { getInitData } from '@/shared/auth/telegram';
import { env } from '@/shared/lib/env';
import { authApi } from './api';

export function useTelegramLogin() {
  const setSession = useAuthStore((s) => s.setSession);
  return useMutation({
    mutationKey: ['auth', 'telegram'],
    mutationFn: () => authApi.loginWithTelegram({ initData: getInitData() }),
    onSuccess: setSession,
  });
}

/**
 * Ilova ochilganda avtomatik kirish: refresh token bo'lsa — refresh, bo'lmasa initData bilan login.
 * Natija: store.status → 'authenticated' yoki 'anonymous' (initData yo'q / rad etilgan).
 */
export function useAutoLogin() {
  const status = useAuthStore((s) => s.status);
  const login = useTelegramLogin();
  const { mutate } = login;
  // Bir martalik refresh: StrictMode'da ikkinchi effekt ham shu promise'ni kutadi (single-flight).
  const refreshInFlight = useRef<Promise<string | null> | null>(null);

  useEffect(() => {
    if (status === 'authenticated') return;
    let cancelled = false;

    if (status === 'restoring') {
      refreshInFlight.current ??= authApi.refresh().finally(() => {
        refreshInFlight.current = null;
      });
      void refreshInFlight.current.then((token) => {
        if (!cancelled && !token) useAuthStore.getState().clear();
      });
      return () => {
        cancelled = true;
      };
    }

    // 'anonymous' va initData bor (Telegram yoki dev override) → POST /api/auth/telegram (bir marta).
    // 403 → login.error → RootLayout "Kirish imkoni yo'q" + backend `detail` (hisob bog'lanmagan / imzo).
    // Mock rejimida (oddiy brauzer, initData bo'sh) ham kiriladi — MSW istalgan initData'ni qabul qiladi.
    if ((getInitData() || env.useMocks) && login.isIdle) mutate();
    return () => {
      cancelled = true;
    };
  }, [status, mutate, login.isIdle]);

  return { status, isPending: login.isPending, error: login.error };
}
