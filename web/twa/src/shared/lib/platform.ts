import { isInsideTelegram } from '@/shared/auth/telegram';

/**
 * `<html data-platform="telegram|web">` — CSS shu atribut bilan oddiy brauzerga moslashadi
 * (desktop'da markazda ≤ 480px mobil ustun, atrofda fon; `globals.css`, `AppShell`, `TabBar`).
 * Telegram ichida ko'rinish o'zgarmaydi.
 */
export function applyPlatformAttr(): void {
  try {
    document.documentElement.dataset['platform'] = isInsideTelegram() ? 'telegram' : 'web';
  } catch {
    /* SSR / document yo'q */
  }
}
