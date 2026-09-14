import { create, type StoreApi, type UseBoundStore } from 'zustand';
import { parseJwt } from './jwt';
import { toUserRole, UserRole } from './roles';
import type { AuthResultDto, AuthUser, UserSummaryDto } from './types';

/**
 * Sessiya holati:
 *  - 'restoring'     — sahifa yangilangan, refresh token bor, /refresh chaqirilmoqda
 *  - 'authenticated' — access token xotirada
 *  - 'anonymous'     — kirilmagan yoki sessiya tugagan
 */
export type AuthStatus = 'restoring' | 'authenticated' | 'anonymous';

export interface AuthState {
  status: AuthStatus;
  /** FAQAT xotirada — localStorage'ga yozilmaydi (XSS xavfi). */
  accessToken: string | null;
  /** Backend uni body'da beradi (cookie emas), shuning uchun sahifa yangilanganda
   *  saqlab qolish uchun `refreshTokenStorage` ga yoziladi. */
  refreshToken: string | null;
  user: AuthUser | null;

  setSession: (result: AuthResultDto) => void;
  setUser: (user: AuthUser) => void;
  setStatus: (status: AuthStatus) => void;
  clear: () => void;
}

/** localStorage / sessionStorage / TWA CloudStorage adapteri. */
export interface KeyValueStorage {
  getItem: (key: string) => string | null;
  setItem: (key: string, value: string) => void;
  removeItem: (key: string) => void;
}

export interface AuthStoreOptions {
  refreshTokenStorage?: KeyValueStorage | null;
  storageKey?: string;
}

export type AuthStore = UseBoundStore<StoreApi<AuthState>>;

export function mapUser(dto: UserSummaryDto): AuthUser {
  return {
    id: dto.id,
    fullName: dto.fullName,
    role: toUserRole(dto.role) ?? UserRole.Student,
    facultyId: dto.facultyId ?? null,
    phoneNumber: dto.phoneNumber ?? null,
    groupId: dto.groupId ?? null,
    groupName: dto.groupName ?? null,
    course: dto.course ?? null,
    hemisId: dto.hemisId ?? null,
  };
}

function safeStorage(storage: KeyValueStorage | null | undefined): KeyValueStorage | null {
  if (!storage) return null;
  // Private rejim / bloklangan storage — xatoni yutib, xotira rejimiga o'tamiz.
  return {
    getItem: (k) => {
      try {
        return storage.getItem(k);
      } catch {
        return null;
      }
    },
    setItem: (k, v) => {
      try {
        storage.setItem(k, v);
      } catch {
        /* ignore */
      }
    },
    removeItem: (k) => {
      try {
        storage.removeItem(k);
      } catch {
        /* ignore */
      }
    },
  };
}

/**
 * Zustand auth store fabrikasi — dashboard va TWA har biri o'zinikini yaratadi
 * (turli storage kaliti, TWA'da storage umuman bo'lmasligi mumkin).
 */
export function createAuthStore(options: AuthStoreOptions = {}): AuthStore {
  const storage = safeStorage(options.refreshTokenStorage);
  const key = options.storageKey ?? 'amaliyotchi.refreshToken';
  const storedRefresh = storage?.getItem(key) ?? null;

  return create<AuthState>((set) => ({
    status: storedRefresh ? 'restoring' : 'anonymous',
    accessToken: null,
    refreshToken: storedRefresh,
    user: null,

    setSession: (result) => {
      storage?.setItem(key, result.refreshToken);
      const user = mapUser(result.user);
      // JWT ichidagi ma'lumot DTO bilan mos bo'lmasa, DTO ustun (server manba).
      const payload = parseJwt(result.accessToken);
      set({
        status: 'authenticated',
        accessToken: result.accessToken,
        refreshToken: result.refreshToken,
        user: payload && !user.fullName ? { ...user, fullName: payload.name } : user,
      });
    },

    setUser: (user) => set({ user }),
    setStatus: (status) => set({ status }),

    clear: () => {
      storage?.removeItem(key);
      set({ status: 'anonymous', accessToken: null, refreshToken: null, user: null });
    },
  }));
}
