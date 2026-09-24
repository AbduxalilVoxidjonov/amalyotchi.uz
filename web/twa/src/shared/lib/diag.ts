import { WebApp } from '@/shared/auth/telegram';
import { env } from '@/shared/lib/env';

/**
 * Production diagnostikasi (vaqtinchalik, telefondan dalil olish uchun). Backend o'zgarmaydi:
 * `navigator.sendBeacon('/api/__diag?d=<json>')` — POST, body bo'sh; nginx /api/ ni API'ga proxy qiladi,
 * API 404 qaytarsa ham access log'da query ko'rinadi.
 *
 * Hodisalar (`e`):
 *  - `boot`     — ilova ishga tushdi: navigatsiya turi (navigate/reload/back_forward), sessiyadagi boot soni,
 *                 UA qisqartmasi, Telegram platforma/versiyasi, JS heap (bo'lsa);
 *  - `route`    — route o'zgardi (pathname + performance.now());
 *  - `error`    — window.onerror; `rejection` — unhandledrejection; `route-error` — React errorElement;
 *  - `pagehide` — sahifa normal yopildi/qayta yuklandi (renderer yiqilsa bu hodisa KELMAYDI — farqlash uchun).
 * Sessiyada ko'pi bilan `MAX_BEACONS` ta. Test rejimida o'chiq.
 */

export const MAX_BEACONS = 30;
const SENT_KEY = 'amaliyotchi:diag-sent';
const BOOT_KEY = 'amaliyotchi:diag-boot';
const MAX_PAYLOAD = 1500;

let memorySent = 0;
let bootCount = 0;
let installed = false;
/** Router'dagi joriy yo'l (Telegram'da memory router — `window.location` doim `/`). */
let routePath: string | null = null;

function enabled(): boolean {
  return (
    !env.isTest && typeof navigator !== 'undefined' && typeof navigator.sendBeacon === 'function'
  );
}

function readCounter(key: string): number | null {
  try {
    return Number(window.sessionStorage.getItem(key) ?? 0) || 0;
  } catch {
    return null;
  }
}

function writeCounter(key: string, value: number): void {
  try {
    window.sessionStorage.setItem(key, String(value));
  } catch {
    /* storage yo'q — xotiradagi hisoblagich ishlatiladi */
  }
}

/** "Mozilla/5.0 (Linux; Android 10; …) … Chrome/153.0… Telegram-Android/12.10.3" → "Android 10|Chrome/153|Telegram-Android/12.10.3". */
export function shortUserAgent(ua: string): string {
  const parts = ua.match(
    /Android [\d.]+|iPhone OS [\d_]+|iPad; CPU OS [\d_]+|Mac OS X [\d_]+|Windows NT [\d.]+|Chrome\/\d+|Version\/[\d.]+|Firefox\/\d+|Telegram-[A-Za-z]+\/[\d.]+|wv\b/g,
  );
  return parts ? parts.join('|') : ua.slice(0, 80);
}

function heap(): { used: number; limit: number } | undefined {
  const m = (
    performance as Performance & {
      memory?: { usedJSHeapSize: number; jsHeapSizeLimit: number };
    }
  ).memory;
  if (!m) return undefined;
  return {
    used: Math.round(m.usedJSHeapSize / 1048576),
    limit: Math.round(m.jsHeapSizeLimit / 1048576),
  };
}

function navigationType(): string | undefined {
  try {
    const entry = performance.getEntriesByType('navigation')[0] as
      PerformanceNavigationTiming | undefined;
    return entry?.type;
  } catch {
    return undefined;
  }
}

/** Bitta beacon. Limitdan oshsa yoki o'chiq bo'lsa — hech narsa. */
export function diag(event: string, data: Record<string, unknown> = {}): void {
  if (!enabled()) return;
  try {
    const stored = readCounter(SENT_KEY);
    const sent = stored ?? memorySent;
    if (sent >= MAX_BEACONS) return;
    memorySent = sent + 1;
    writeCounter(SENT_KEY, sent + 1);
    const payload = {
      e: event,
      p: routePath ?? window.location.pathname,
      t: Math.round(performance.now()),
      b: bootCount,
      n: sent + 1,
      ...data,
    };
    const d = JSON.stringify(payload).slice(0, MAX_PAYLOAD);
    navigator.sendBeacon(`${env.apiUrl}/api/__diag?d=${encodeURIComponent(d)}`);
  } catch {
    /* diagnostika hech qachon ilovani yiqitmasin */
  }
}

function errorInfo(error: unknown): { m: string; s?: string | undefined } {
  if (error instanceof Error) {
    return { m: `${error.name}: ${error.message}`.slice(0, 300), s: error.stack?.slice(0, 300) };
  }
  try {
    return { m: (typeof error === 'string' ? error : JSON.stringify(error)).slice(0, 300) };
  } catch {
    return { m: String(error).slice(0, 300) };
  }
}

/** React errorElement / boundary xatosi. */
export function diagError(source: string, error: unknown): void {
  diag(source, errorInfo(error));
}

/** Router'ning joriy yo'lini beacon'siz qayd etish (ishga tushishda). */
export function setDiagRoutePath(pathname: string): void {
  routePath = pathname;
}

/** Route o'zgarishi (router.subscribe dan). */
export function diagRoute(pathname: string): void {
  routePath = pathname;
  diag('route', { to: pathname, mem: heap() });
}

/** Ilova ishga tushganda bir marta (main.tsx). */
export function installDiagnostics(): void {
  if (installed || !enabled()) return;
  installed = true;
  const prevBoots = readCounter(BOOT_KEY) ?? 0;
  bootCount = prevBoots + 1;
  writeCounter(BOOT_KEY, bootCount);

  let tg: string | undefined;
  try {
    tg = WebApp.initData ? `${WebApp.platform} ${WebApp.version}` : undefined;
  } catch {
    tg = undefined;
  }
  diag('boot', {
    nav: navigationType(),
    ua: shortUserAgent(navigator.userAgent),
    tg,
    vis: document.visibilityState,
    mem: heap(),
  });

  window.addEventListener('error', (ev) => {
    diag('error', {
      ...errorInfo(ev.error ?? ev.message),
      src: ev.filename
        ? `${ev.filename.split('/').pop() ?? ''}:${ev.lineno}:${ev.colno}`
        : undefined,
    });
  });
  window.addEventListener('unhandledrejection', (ev) => {
    diag('rejection', errorInfo(ev.reason));
  });
  window.addEventListener('vite:preloadError', (ev) => {
    diag('preload-error', errorInfo((ev as Event & { payload?: unknown }).payload));
  });
  window.addEventListener('pagehide', (ev) => {
    diag('pagehide', { persisted: ev.persisted, mem: heap() });
  });
}
