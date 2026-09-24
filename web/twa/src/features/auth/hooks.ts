import { useMutation, useQueryClient } from '@tanstack/react-query';
import { useCallback, useEffect, useRef } from 'react';
import { useNavigate } from 'react-router-dom';
import { toUserRole, UserRole, type LoginRequest } from '@amaliyotchi/shared';
import { errorMessage, isApiError } from '@/shared/api/client';
import { authMode } from '@/shared/auth/mode';
import { applyAuthResult, useSessionFlags, type TwaAuthResult } from '@/shared/auth/session';
import { useAuthStore } from '@/shared/auth/store';
import { getInitData } from '@/shared/auth/telegram';
import { profileKeys } from '@/features/profile/hooks';
import { authApi, type ChangePasswordRequest } from './api';

export function useTelegramLogin() {
  return useMutation({
    mutationKey: ['auth', 'telegram'],
    mutationFn: () => authApi.loginWithTelegram({ initData: getInitData() }),
    onSuccess: applyAuthResult,
  });
}

/**
 * Ilova ochilganda avtomatik kirish: refresh token bo'lsa — refresh (ikkala rejimda),
 * bo'lmasa Telegram rejimida initData bilan login. Web rejimida anonim qoladi → login sahifasi.
 * Telegram ichida foydalanuvchi o'zi chiqqan bo'lsa (`loggedOut`) — avtomatik qayta kirilmaydi.
 */
export function useAutoLogin() {
  const status = useAuthStore((s) => s.status);
  const loggedOut = useSessionFlags((s) => s.loggedOut);
  const mode = authMode();
  const login = useTelegramLogin();
  const { mutate, reset } = login;
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

    // 'anonymous' + Telegram rejimi (initData bor yoki mock) → POST /api/auth/telegram (bir marta).
    // 403 → login.error → RootLayout bog'lash formasi (POST /api/auth/telegram/link); boshqa xato → "Kirish imkoni yo'q".
    // Mock rejimida (oddiy brauzer, initData bo'sh) ham kiriladi — MSW istalgan initData'ni qabul qiladi.
    if (mode === 'telegram' && !loggedOut && login.isIdle) mutate();
    return () => {
      cancelled = true;
    };
  }, [status, mode, loggedOut, mutate, login.isIdle]);

  /** "Qayta kirish" (Telegram ichida chiqqandan keyin): bayroq olib tashlanadi → effekt qayta kiradi. */
  const relogin = useCallback(() => {
    reset();
    useSessionFlags.getState().setLoggedOut(false);
  }, [reset]);

  return { status, mode, loggedOut, isPending: login.isPending, error: login.error, relogin };
}

export const STAFF_REJECTED_MESSAGE =
  "Bu ilova faqat talabalar uchun. Xodimlar dashboard'dan kiradi.";

/** Xodim (admin/tyutor) hisobi bilan web-login — sessiya saqlanmaydi. */
export class StaffAccountError extends Error {
  constructor() {
    super(STAFF_REJECTED_MESSAGE);
    this.name = 'StaffAccountError';
  }
}

/** Web-login xatosi: backend `detail` bo'lmasa (401 body'siz) — tushunarli matn. */
export function loginErrorMessage(error: unknown): string {
  if (
    isApiError(error) &&
    (error.kind === 'unauthorized' || error.kind === 'forbidden') &&
    !error.problem?.detail
  ) {
    return "HEMIS ID yoki parol noto'g'ri.";
  }
  return errorMessage(error);
}

/** HEMIS ID + parol bilan sessiya oluvchi so'rov (web-login yoki Telegram bog'lash). */
export type CredentialsSubmit = (body: LoginRequest) => Promise<TwaAuthResult>;

/**
 * HEMIS ID + parol → sessiya. Faqat talaba roli qabul qilinadi: xodim bo'lsa server bergan sessiya
 * darhol bekor qilinadi va `StaffAccountError` (sessiya saqlanmaydi). Muvaffaqiyatda `applyAuthResult`.
 */
export function useCredentialsLogin(submit: CredentialsSubmit, mutationKey: readonly string[]) {
  return useMutation({
    mutationKey,
    mutationFn: async (body: LoginRequest) => {
      const result = await submit(body);
      if (toUserRole(result.user.role) !== UserRole.Student) {
        // Server bergan sessiyani darhol bekor qilamiz (natijasi muhim emas).
        void authApi.logout(result.refreshToken, result.accessToken).catch(() => undefined);
        throw new StaffAccountError();
      }
      return result;
    },
    onSuccess: applyAuthResult,
  });
}

/** POST /api/auth/login — web-login (Telegram tashqarisida). */
export const submitWebLogin: CredentialsSubmit = (body) => authApi.login(body);

/** POST /api/auth/telegram/link — Telegram ichida birinchi kirish (initData + HEMIS ID + parol). */
export const submitTelegramLink: CredentialsSubmit = (body) =>
  authApi.linkTelegram({ initData: getInitData(), ...body });

/** POST /api/auth/login — faqat talaba roli qabul qilinadi. */
export function useWebLogin() {
  return useCredentialsLogin(submitWebLogin, ['auth', 'login']);
}

/** 409 (Telegram akkaunti / hisob boshqasiga bog'langan) — `detail` ostidagi qo'shimcha izoh. */
export function telegramLinkErrorHint(error: unknown): string | undefined {
  return isApiError(error) && error.kind === 'conflict'
    ? 'Tyutoringizga murojaat qiling.'
    : undefined;
}

/** POST /api/auth/change-password — muvaffaqiyatda majburiy parol talabi yechiladi. */
export function useChangePassword() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationKey: ['auth', 'change-password'],
    // Joriy refresh token yuboriladi — aks holda backend barcha sessiyalarni (shu jumladan joriysini) bekor qiladi.
    mutationFn: (body: Omit<ChangePasswordRequest, 'refreshToken'>) => {
      const { refreshToken } = useAuthStore.getState();
      return authApi.changePassword(refreshToken ? { ...body, refreshToken } : body);
    },
    onSuccess: () => {
      useSessionFlags.getState().setMustChangePassword(false);
      void queryClient.invalidateQueries({ queryKey: profileKeys.all });
    },
  });
}

/**
 * Chiqish: refresh token serverda bekor qilinadi (xato bo'lsa ham lokal sessiya tozalanadi),
 * `loggedOut` bayrog'i qo'yiladi — Telegram ichida avtomatik qayta kirilmaydi.
 */
export function useLogout() {
  const queryClient = useQueryClient();
  const navigate = useNavigate();
  return useMutation({
    mutationKey: ['auth', 'logout'],
    mutationFn: async () => {
      const { refreshToken } = useAuthStore.getState();
      if (refreshToken) await authApi.logout(refreshToken).catch(() => undefined);
    },
    onSettled: () => {
      useSessionFlags.getState().setLoggedOut(true);
      void navigate('/', { replace: true });
      useAuthStore.getState().clear();
      queryClient.clear();
    },
  });
}
