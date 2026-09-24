import { create } from 'zustand';
import type { AuthResultDto } from '@amaliyotchi/shared';
import { useAuthStore } from '@/shared/auth/store';

/**
 * Auth javobi (login / refresh / telegram) — `AuthResultDto` + `mustChangePassword`
 * (backend vaqtinchalik parol bilan kirgan talabadan yangi parol talab qiladi).
 * Eski backend maydonni bermasa — `false` deb olinadi.
 */
export type TwaAuthResult = AuthResultDto & { mustChangePassword?: boolean };

const LOGGED_OUT_KEY = 'amaliyotchi.twa.loggedOut';

function readLoggedOut(): boolean {
  try {
    return window.sessionStorage.getItem(LOGGED_OUT_KEY) === '1';
  } catch {
    return false;
  }
}

function writeLoggedOut(value: boolean) {
  try {
    if (value) window.sessionStorage.setItem(LOGGED_OUT_KEY, '1');
    else window.sessionStorage.removeItem(LOGGED_OUT_KEY);
  } catch {
    /* private rejim — faqat xotirada */
  }
}

interface SessionFlagsState {
  /** true → ilova o'rniga "Yangi parol o'rnating" ekrani. */
  mustChangePassword: boolean;
  /**
   * Foydalanuvchi o'zi "Chiqish" bosgan. Telegram ichida bu holatda avtomatik qayta kirilmaydi —
   * "Qayta kirish" tugmasi ko'rsatiladi. sessionStorage'da (WebView qayta yuklansa ham saqlanadi).
   */
  loggedOut: boolean;
  setMustChangePassword: (value: boolean) => void;
  setLoggedOut: (value: boolean) => void;
  reset: () => void;
}

export const useSessionFlags = create<SessionFlagsState>((set) => ({
  mustChangePassword: false,
  loggedOut: typeof window !== 'undefined' ? readLoggedOut() : false,
  setMustChangePassword: (mustChangePassword) => set({ mustChangePassword }),
  setLoggedOut: (loggedOut) => {
    writeLoggedOut(loggedOut);
    set({ loggedOut });
  },
  reset: () => {
    writeLoggedOut(false);
    set({ mustChangePassword: false, loggedOut: false });
  },
}));

// Sessiya tugasa (clear) — parol talabi ham tushib ketadi; keyingi login javobi uni qayta beradi.
useAuthStore.subscribe((state, prev) => {
  if (state.status === 'anonymous' && prev.status !== 'anonymous') {
    useSessionFlags.getState().setMustChangePassword(false);
  }
});

/** Har qanday auth javobini store'ga yozish (login, telegram, refresh — bitta joyda). */
export function applyAuthResult(result: TwaAuthResult): void {
  useAuthStore.getState().setSession(result);
  const flags = useSessionFlags.getState();
  flags.setMustChangePassword(result.mustChangePassword === true);
  if (flags.loggedOut) flags.setLoggedOut(false);
}
