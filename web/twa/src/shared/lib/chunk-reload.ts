/**
 * Deploydan keyingi "eski chunk" qo'riqchisi.
 *
 * Muammo: foydalanuvchi ilovani deploydan OLDIN ochgan (Telegram Mini App fonda/minimallashtirilgan holda
 * tirik qoladi yoki WebView eski index.html'ni ushlab turadi). Yangi deploy'da hash'li chunk nomlari
 * o'zgaradi, eski fayllar serverda yo'q → tab bosilganda lazy sahifa chunk'i 404 →
 * `Failed to fetch dynamically imported module` (Chrome/Android) /
 * `Importing a module script failed` (Safari/iOS) → sahifa ochilmaydi.
 *
 * Yechim: shunday xatoda sahifani BIR MARTA avtomatik qayta yuklash (`location.reload()` — Telegram
 * initData `telegram-web-app.js` tomonidan sessionStorage'da saqlanadi, sessiya refresh token'i ham).
 * Cheksiz loop bo'lmasligi uchun sessionStorage'da vaqt belgisi: oxirgi avto-reload'dan
 * `RELOAD_COOLDOWN_MS` o'tmagan bo'lsa qayta yuklanmaydi — xato ekrani ko'rsatiladi.
 */

export const CHUNK_RELOAD_KEY = 'amaliyotchi:chunk-reload-at';
export const RELOAD_COOLDOWN_MS = 60_000;

const CHUNK_ERROR_RE =
  /Failed to fetch dynamically imported module|Importing a module script failed|error loading dynamically imported module|Unable to preload CSS|Loading (CSS )?chunk [\w-]+ failed|ChunkLoadError/i;

/** Lazy import / preload xatosimi (chunk topilmadi yoki yuklanmadi). */
export function isChunkLoadError(error: unknown): boolean {
  if (!error || typeof error !== 'object')
    return typeof error === 'string' && CHUNK_ERROR_RE.test(error);
  const { name, message } = error as { name?: unknown; message?: unknown };
  if (name === 'ChunkLoadError') return true;
  return typeof message === 'string' && CHUNK_ERROR_RE.test(message);
}

/** Testlarda almashtiriladi (jsdom `location.reload` ni qo'llamaydi). */
export const chunkReloadDeps = {
  reload: () => window.location.reload(),
  now: () => Date.now(),
};

let reloading = false;

/** Shu sahifa hayotida avto-reload boshlanganmi (lazy import javobini "osilib" qoldirish uchun). */
export function isChunkReloadPending(): boolean {
  return reloading;
}

/**
 * Sahifani bir marta qayta yuklashga urinish. `true` — reload boshlandi (yoki allaqachon boshlangan);
 * `false` — yaqinda reload bo'lgan / storage yo'q → xatoni foydalanuvchiga ko'rsatish kerak.
 */
export function reloadOnceForChunkError(): boolean {
  if (reloading) return true;
  try {
    const storage = window.sessionStorage;
    const last = Number(storage.getItem(CHUNK_RELOAD_KEY) ?? 0);
    const now = chunkReloadDeps.now();
    if (last && now - last < RELOAD_COOLDOWN_MS) return false;
    storage.setItem(CHUNK_RELOAD_KEY, String(now));
  } catch {
    // sessionStorage yo'q/taqiqlangan — loop'ni oldini olib bo'lmaydi, avto-reload qilinmaydi.
    return false;
  }
  reloading = true;
  chunkReloadDeps.reload();
  return true;
}

/**
 * `React.lazy` uchun import o'rami: chunk xatosida sahifa qayta yuklanadi va promise hech qachon
 * resolve bo'lmaydi (Suspense fallback reload'gacha turadi — xato ekrani "miltillamaydi").
 * Reload qilib bo'lmasa — xato tashlanadi (route errorElement ushlaydi).
 */
export function importWithReload<T>(factory: () => Promise<T>): () => Promise<T> {
  return () =>
    factory().catch((error: unknown) => {
      if (isChunkLoadError(error) && reloadOnceForChunkError()) {
        return new Promise<T>(() => {});
      }
      throw error;
    });
}

let installed = false;

/**
 * Vite `vite:preloadError` — build'da lazy chunk yoki uning bog'liqliklari (JS/CSS) yuklanmasa.
 * `preventDefault` qilinmaydi: xato baribir lazy import'ga qaytadi (reload bo'lmasa errorElement ko'rsatadi).
 */
export function installChunkReloadGuard(): void {
  if (installed || typeof window === 'undefined') return;
  installed = true;
  window.addEventListener('vite:preloadError', () => {
    reloadOnceForChunkError();
  });
}

/** Faqat testlar uchun: modul holatini tiklash. */
export function resetChunkReloadState(): void {
  reloading = false;
}
