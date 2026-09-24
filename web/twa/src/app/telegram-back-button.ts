import { WebApp } from '@/shared/auth/telegram';
import type { AppRouter } from './router';

/** Bot API: `BackButton` — 6.1+. */
export const BACK_BUTTON_MIN_VERSION = '6.1';

function backButtonSupported(): boolean {
  try {
    return (
      typeof WebApp.isVersionAtLeast === 'function' &&
      WebApp.isVersionAtLeast(BACK_BUTTON_MIN_VERSION) &&
      typeof WebApp.BackButton?.show === 'function'
    );
  } catch {
    return false;
  }
}

/**
 * Telegram rejimi (memory router) uchun "orqaga": brauzer tarixi yo'q, shuning uchun Telegram'ning native
 * `BackButton`i ishlatiladi (Android apparat "orqaga" tugmasi ham shu hodisa orqali keladi).
 *  - bosh ekran (`/`) — yashirin; boshqa sahifa — ko'rinadi;
 *  - bosilganda `router.navigate(-1)`; memory tarixida orqada yozuv bo'lmasa — `/` ga (replace).
 *    Chuqurlik o'zimiz hisoblanadi (memory router indeksni ochmaydi): PUSH +1, POP −1, REPLACE — 0.
 * Qaytaradi: bog'lanishni bekor qiluvchi funksiya. Eski klient / Telegram tashqarisi — hech narsa.
 */
export function bindTelegramBackButton(router: AppRouter): () => void {
  if (!backButtonSupported()) return () => undefined;

  const sync = (pathname: string) => {
    try {
      if (pathname === '/') WebApp.BackButton.hide();
      else WebApp.BackButton.show();
    } catch {
      /* ignore */
    }
  };

  let depth = 0;
  let lastKey = router.state.location.key;
  let lastPath = router.state.location.pathname;

  const onBack = () => {
    if (router.state.location.pathname === '/') return;
    void (depth > 0 ? router.navigate(-1) : router.navigate('/', { replace: true }));
  };

  sync(lastPath);
  const unsubscribe = router.subscribe((state) => {
    if (state.location.key !== lastKey) {
      lastKey = state.location.key;
      if (state.historyAction === 'PUSH') depth += 1;
      else if (state.historyAction === 'POP') depth = Math.max(0, depth - 1);
    }
    if (state.location.pathname === lastPath) return;
    lastPath = state.location.pathname;
    sync(lastPath);
  });
  try {
    WebApp.BackButton.onClick(onBack);
  } catch {
    /* ignore */
  }

  return () => {
    unsubscribe();
    try {
      WebApp.BackButton.offClick(onBack);
      WebApp.BackButton.hide();
    } catch {
      /* ignore */
    }
  };
}
