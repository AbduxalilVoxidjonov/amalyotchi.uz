import WebApp from '@twa-dev/sdk';
import { env } from '@/shared/lib/env';

/**
 * Telegram WebApp ma'lumotlari (`@twa-dev/sdk` — rasmiy telegram-web-app.js ni o'zi yuklaydi,
 * shuning uchun index.html da alohida <script> yo'q).
 * Testlarda `@twa-dev/sdk` `src/test/setup.ts` orqali stub qilinadi.
 */

/**
 * Imzolangan initData satri (backend uni bot tokeni bilan tekshiradi). Telegram tashqarisida — ''
 * (dev'da `env.devInitData` — `?initData=` yoki `VITE_DEV_INIT_DATA` — o'rnini bosadi).
 */
export function getInitData(): string {
  const real = WebApp.initData ?? '';
  if (real) return real;
  return env.devInitData;
}

/** Haqiqiy Telegram klienti ichidamizmi (dev initData bunga kirmaydi — openLink va h.k. brauzerda). */
export function isInsideTelegram(): boolean {
  return (WebApp.initData ?? '').length > 0;
}

/**
 * Dizayn palitrasi (SPEC-TOKENS 1.1). Telegram header/fon rangi shu bilan tenglashtiriladi —
 * aks holda Telegram o'z temasi rangini ko'rsatib, ilova bilan "chok" hosil bo'ladi.
 * ❓ Dizayn faqat oq palitrada; Telegram `colorScheme === 'dark'` bo'lsa ham shu palitra qoladi
 * (`html[data-tg-scheme]` orqali keyin dark variant qo'shish mumkin).
 */
const DESIGN_BG = '#f4f3ef';

/** Telegram klienti Bot API versiyasi ≥ `version` (Telegram tashqarisida / stub'da — false). */
function versionAtLeast(version: string): boolean {
  try {
    return typeof WebApp.isVersionAtLeast === 'function' && WebApp.isVersionAtLeast(version);
  } catch {
    return false;
  }
}

/** Telegram temasini o'qib, ilova rangini moslash. Telegram tashqarisida — e'tiborsiz. */
export function applyTelegramTheme(): void {
  const root = document.documentElement;
  try {
    root.dataset['tgScheme'] = WebApp.colorScheme ?? 'light';
    const p = WebApp.themeParams ?? {};
    // Telegram tema ranglari CSS'ga ham beriladi (kelajakda dark variant uchun).
    for (const [key, value] of Object.entries(p)) {
      if (typeof value === 'string')
        root.style.setProperty(`--tg-theme-${key.replace(/_/g, '-')}`, value);
    }
    // Bot API: setHeaderColor/setBackgroundColor — 6.1+, setBottomBarColor — 7.10+.
    // Eski klientda telegram-web-app.js console'ga ogohlantirish yozadi — versiya tekshiruvi bilan o'raladi.
    if (versionAtLeast('6.1')) {
      WebApp.setHeaderColor(DESIGN_BG);
      WebApp.setBackgroundColor(DESIGN_BG);
    }
    if (versionAtLeast('7.10')) WebApp.setBottomBarColor(DESIGN_BG);
  } catch {
    /* Telegram tashqarisida (oddiy brauzer) — e'tiborsiz. */
  }
}

/** Telegram'ga "ilova tayyor" deyish, to'liq ekranga yoyish va temani qo'llash. */
export function initTelegram(): void {
  try {
    WebApp.ready();
    WebApp.expand();
    // Pastki tab-bar bilan tasodifiy pastga surib yopishni oldini oladi (Bot API 7.7+).
    if (versionAtLeast('7.7')) WebApp.disableVerticalSwipes();
    applyTelegramTheme();
    WebApp.onEvent?.('themeChanged', applyTelegramTheme);
  } catch {
    /* Telegram tashqarisida (oddiy brauzer) — e'tiborsiz. */
  }
}

/** Yengil vibratsiya (check-in tugmasi). Telegram tashqarisida — hech narsa. */
export function haptic(type: 'success' | 'error' | 'warning'): void {
  try {
    WebApp.HapticFeedback?.notificationOccurred(type);
  } catch {
    /* ignore */
  }
}

/**
 * Telegram native QR skaneri (Bot API `showScanQrPopup` — 6.4+):
 * - `native` — Telegram ichida, versiya yetarli;
 * - `outdated` — Telegram ichida, lekin mijoz eski (6.4 dan past) yoki metod yo'q;
 * - `none` — Telegram tashqarisida (oddiy brauzer — kamera skaneri ishlatiladi).
 */
export type TelegramQrSupport = 'native' | 'outdated' | 'none';

export const QR_SCANNER_MIN_VERSION = '6.4';

export function telegramQrSupport(): TelegramQrSupport {
  if (!isInsideTelegram()) return 'none';
  try {
    return versionAtLeast(QR_SCANNER_MIN_VERSION) && typeof WebApp.showScanQrPopup === 'function'
      ? 'native'
      : 'outdated';
  } catch {
    return 'outdated';
  }
}

/**
 * Telegram native QR skaneri. Birinchi skanerlangan matn bilan resolve bo'ladi (popup darhol yopiladi —
 * callback `true` qaytaradi); foydalanuvchi popup'ni yopsa (`scanQrPopupClosed`) — `null`.
 * Skaner mavjud bo'lmasa — reject (`telegramQrSupport()` bilan oldindan tekshiring).
 */
export function scanTelegramQr(text: string): Promise<string | null> {
  return new Promise<string | null>((resolve, reject) => {
    let settled = false;
    const onClosed = () => finish(null);
    function finish(value: string | null) {
      if (settled) return;
      settled = true;
      try {
        WebApp.offEvent('scanQrPopupClosed', onClosed);
      } catch {
        /* ignore */
      }
      resolve(value);
    }
    try {
      WebApp.onEvent('scanQrPopupClosed', onClosed);
      WebApp.showScanQrPopup({ text }, (data) => {
        finish(data);
        return true;
      });
    } catch (cause) {
      settled = true;
      try {
        WebApp.offEvent('scanQrPopupClosed', onClosed);
      } catch {
        /* ignore */
      }
      reject(cause instanceof Error ? cause : new Error('QR skaner ochilmadi'));
    }
  });
}

/** Ochiq QR popup'ni yopish (oqim bekor qilinganda). Telegram tashqarisida — hech narsa. */
export function closeTelegramQrScanner(): void {
  try {
    if (telegramQrSupport() === 'native') WebApp.closeScanQrPopup();
  } catch {
    /* ignore */
  }
}

export { WebApp };

/** Tashqi havola (PDF, shablon): Telegram ichida `openLink`, tashqarida yangi tab. */
export function openExternal(url: string): void {
  try {
    if (isInsideTelegram()) {
      WebApp.openLink(url);
      return;
    }
  } catch {
    /* fallthrough */
  }
  window.open(url, '_blank', 'noopener');
}
