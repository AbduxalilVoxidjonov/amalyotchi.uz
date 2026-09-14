import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { useEffect, useRef } from 'react';
import { mapUser, type LoginRequest } from '@amaliyotchi/shared';
import { authKeys } from '@/shared/api/query-keys';
import { useAuthStore } from '@/shared/auth/store';
import { authApi } from './api';

export function useLogin() {
  const setSession = useAuthStore((s) => s.setSession);
  const queryClient = useQueryClient();

  return useMutation({
    mutationKey: ['auth', 'login'],
    mutationFn: (body: LoginRequest) => authApi.login(body),
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
