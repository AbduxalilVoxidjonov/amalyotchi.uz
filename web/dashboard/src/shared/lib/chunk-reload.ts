/**
 * Deploydan keyingi "eski chunk" qo'riqchisi: brauzerda eski index.html ochiq qolgan bo'lsa, yangi
 * deploy'dan keyin lazy sahifa chunk'i (eski hash) 404 → Vite `vite:preloadError` hodisasi.
 * Shunda sahifa BIR MARTA avtomatik qayta yuklanadi. Cheksiz loop bo'lmasligi uchun sessionStorage'da
 * vaqt belgisi: oxirgi avto-reload'dan `RELOAD_COOLDOWN_MS` o'tmagan bo'lsa — reload yo'q, xato odatdagidek
 * route xato ekraniga boradi.
 */
export const CHUNK_RELOAD_KEY = 'amaliyotchi:chunk-reload-at';
export const RELOAD_COOLDOWN_MS = 60_000;

/** Testlarda almashtiriladi (jsdom `location.reload` ni qo'llamaydi). */
export const chunkReloadDeps = {
  reload: () => window.location.reload(),
  now: () => Date.now(),
};

let reloading = false;

/** `true` — reload boshlandi; `false` — yaqinda reload bo'lgan yoki storage yo'q. */
export function reloadOnceForChunkError(): boolean {
  if (reloading) return true;
  try {
    const last = Number(window.sessionStorage.getItem(CHUNK_RELOAD_KEY) ?? 0);
    const now = chunkReloadDeps.now();
    if (last && now - last < RELOAD_COOLDOWN_MS) return false;
    window.sessionStorage.setItem(CHUNK_RELOAD_KEY, String(now));
  } catch {
    return false;
  }
  reloading = true;
  chunkReloadDeps.reload();
  return true;
}

let installed = false;

/** `main.tsx` da bir marta chaqiriladi. */
export function installChunkReloadGuard(): void {
  if (installed || typeof window === 'undefined') return;
  installed = true;
  window.addEventListener('vite:preloadError', () => {
    reloadOnceForChunkError();
  });
}

/** Faqat testlar uchun. */
export function resetChunkReloadState(): void {
  reloading = false;
}
