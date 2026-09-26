import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { useEffect, useRef } from 'react';
import { mapUser, toUserRole, UserRole, type LoginRequest } from '@amaliyotchi/shared';
import { authKeys } from '@/shared/api/query-keys';
import { useAuthStore } from '@/shared/auth/store';
import { authApi, type ChangeLoginRequest, type ChangePasswordRequest } from './api';

/**
 * Backend talabaga ham `/api/auth/login` da 200 qaytaradi (TWA brauzerda kiradi), lekin dashboard
 * faqat admin/tyutor uchun: talaba sessiyasi saqlanmaydi — tokenlar tashlanadi, server sessiyasi yopiladi.
 */
export class StudentLoginRejectedError extends Error {
  constructor() {
    super('Talabalar dashboard orqali kirmaydi.');
    this.name = 'StudentLoginRejectedError';
  }
}

export function isStudentLoginRejected(error: unknown): error is StudentLoginRejectedError {
  return error instanceof StudentLoginRejectedError;
}

export function useLogin() {
  const setSession = useAuthStore((s) => s.setSession);
  const queryClient = useQueryClient();

  return useMutation({
    mutationKey: ['auth', 'login'],
    mutationFn: async (body: LoginRequest) => {
      const result = await authApi.login(body);
      if (toUserRole(result.user.role) === UserRole.Student) {
        // Refresh token server tomonda bekor qilinadi (xatosi kirish xabariga ta'sir qilmaydi).
        void authApi
          .logoutWithToken(result.accessToken, { refreshToken: result.refreshToken })
          .catch(() => undefined);
        throw new StudentLoginRejectedError();
      }
      return result;
    },
    onSuccess: (result) => {
      setSession(result);
      queryClient.setQueryData(authKeys.me(), result.user);
    },
  });
}

export function useLogout() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationKey: ['auth', 'logout'],
    mutationFn: async () => {
      const { refreshToken } = useAuthStore.getState();
      // Token yo'q bo'lsa ham lokal sessiya tozalanadi; server xatosi chiqishga to'sqinlik qilmaydi.
      if (refreshToken) await authApi.logout({ refreshToken }).catch(() => undefined);
    },
    onSettled: () => {
      useAuthStore.getState().clear();
      queryClient.clear();
    },
  });
}

/** Joriy foydalanuvchi (server manba). Store'dagi `user` ni ham yangilab turadi. */
export function useMe(enabled = true) {
  const status = useAuthStore((s) => s.status);
  const setUser = useAuthStore((s) => s.setUser);

  const query = useQuery({
    queryKey: authKeys.me(),
    queryFn: authApi.me,
    enabled: enabled && status === 'authenticated',
    staleTime: 5 * 60 * 1000,
  });

  useEffect(() => {
    if (query.data) setUser(mapUser(query.data));
  }, [query.data, setUser]);

  return query;
}

/**
 * Ilova ochilganda: localStorage'da refresh token bo'lsa (status === 'restoring')
 * bir marta /refresh chaqirib sessiyani tiklaydi; bo'lmasa anonim.
 */
export function useSessionBootstrap() {
  const status = useAuthStore((s) => s.status);
  // Bir martalik: StrictMode (mount → unmount → mount) ikkinchi effekt ham shu promise'ni kutadi —
  // /refresh ikki marta chaqirilmaydi (client'dagi 401 single-flight bilan bir xil yondashuv).
  const inFlight = useRef<Promise<string | null> | null>(null);

  useEffect(() => {
    if (status !== 'restoring') return;
    let cancelled = false;
    inFlight.current ??= authApi.refresh().finally(() => {
      inFlight.current = null;
    });
    void inFlight.current.then((token) => {
      if (!cancelled && !token) useAuthStore.getState().clear();
    });
    return () => {
      cancelled = true;
    };
  }, [status]);

  return status;
}

/** `login-available` kalitlari — 409'dan keyin shu prefiks bo'yicha qayta so'raladi. */
export const loginAvailabilityKeys = {
  all: ['auth', 'login-available'] as const,
  check: (login: string) => ['auth', 'login-available', login] as const,
};

/**
 * O'z parolini almashtirish. Joriy refresh token yuboriladi — backend boshqa sessiyalarni bekor
 * qiladi, joriysini saqlaydi (access token o'zgarmaydi → store yangilanishi shart emas).
 */
export function useChangePassword() {
  return useMutation({
    mutationKey: ['auth', 'change-password'],
    mutationFn: (body: Omit<ChangePasswordRequest, 'refreshToken'>) => {
      const { refreshToken } = useAuthStore.getState();
      return authApi.changePassword(refreshToken ? { ...body, refreshToken } : body);
    },
  });
}

/** Login bo'shligini tekshirish. `login` — allaqachon debounce qilingan va trim'langan qiymat; bo'sh → so'rov yo'q. */
export function useLoginAvailability(login: string, enabled = true) {
  return useQuery({
    queryKey: loginAvailabilityKeys.check(login),
    queryFn: ({ signal }) => authApi.loginAvailable(login, signal),
    enabled: enabled && login.length > 0,
    staleTime: 15 * 1000,
    retry: false,
  });
}

/** O'z loginini almashtirish → 200 `UserSummaryDto`: store'dagi `user` va `me` keshi yangilanadi. */
export function useChangeLogin() {
  const queryClient = useQueryClient();
  const setUser = useAuthStore((s) => s.setUser);
  return useMutation({
    mutationKey: ['auth', 'change-login'],
    mutationFn: (body: ChangeLoginRequest) => authApi.changeLogin(body),
    onSuccess: (dto) => {
      setUser(mapUser(dto));
      queryClient.setQueryData(authKeys.me(), dto);
      queryClient.removeQueries({ queryKey: loginAvailabilityKeys.all });
    },
  });
}
