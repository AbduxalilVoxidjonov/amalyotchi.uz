import {
  CHUNK_RELOAD_KEY,
  RELOAD_COOLDOWN_MS,
  chunkReloadDeps,
  importWithReload,
  installChunkReloadGuard,
  isChunkLoadError,
  isChunkReloadPending,
  reloadOnceForChunkError,
  resetChunkReloadState,
} from './chunk-reload';

describe('chunk-reload', () => {
  let reload: ReturnType<typeof vi.fn<() => void>>;
  let now = 1_000_000;

  beforeEach(() => {
    resetChunkReloadState();
    reload = vi.fn<() => void>();
    vi.spyOn(chunkReloadDeps, 'reload').mockImplementation(reload);
    vi.spyOn(chunkReloadDeps, 'now').mockImplementation(() => now);
  });
  afterEach(() => {
    vi.restoreAllMocks();
    resetChunkReloadState();
  });

  it.each([
    new TypeError('Failed to fetch dynamically imported module: https://x/assets/DiaryPage-abc.js'),
    new TypeError('Importing a module script failed.'),
    new TypeError('error loading dynamically imported module: https://x/a.js'),
    new Error('Unable to preload CSS for /assets/DiaryPage-c.css'),
    Object.assign(new Error('Loading chunk 12 failed.'), { name: 'ChunkLoadError' }),
  ])('chunk xatosi taniladi: %s', (err) => {
    expect(isChunkLoadError(err)).toBe(true);
  });

  it.each([
    new Error('boom'),
    new TypeError("Cannot read properties of null (reading 'x')"),
    null,
    42,
  ])('boshqa xato chunk emas: %s', (err) => {
    expect(isChunkLoadError(err)).toBe(false);
  });

  it("bir marta reload; cooldown ichida qayta reload yo'q (loop yo'q)", () => {
    expect(reloadOnceForChunkError()).toBe(true);
    expect(reload).toHaveBeenCalledTimes(1);
    expect(window.sessionStorage.getItem(CHUNK_RELOAD_KEY)).toBe(String(now));
    expect(isChunkReloadPending()).toBe(true);

    // Sahifa qayta yuklandi (modul holati yangi), lekin chunk hali ham yo'q:
    resetChunkReloadState();
    now += 5_000;
    expect(reloadOnceForChunkError()).toBe(false);
    expect(reload).toHaveBeenCalledTimes(1);

    // Cooldown o'tgach (keyingi deploy) — yana bir marta ruxsat.
    now += RELOAD_COOLDOWN_MS;
    expect(reloadOnceForChunkError()).toBe(true);
    expect(reload).toHaveBeenCalledTimes(2);
  });

  it('sessionStorage ishlamasa — reload qilinmaydi (loop xavfi)', () => {
    vi.spyOn(Storage.prototype, 'getItem').mockImplementation(() => {
      throw new DOMException('denied', 'SecurityError');
    });
    expect(reloadOnceForChunkError()).toBe(false);
    expect(reload).not.toHaveBeenCalled();
  });

  it('importWithReload: chunk xatosida reload va promise osilib qoladi', async () => {
    const load = importWithReload(() =>
      Promise.reject(new TypeError('Failed to fetch dynamically imported module: /a.js')),
    );
    const settled = vi.fn();
    void load().then(settled, settled);
    await new Promise((r) => setTimeout(r, 10));
    expect(reload).toHaveBeenCalledTimes(1);
    expect(settled).not.toHaveBeenCalled();
  });

  it("importWithReload: reload qilib bo'lmasa yoki boshqa xato — xato tashlanadi", async () => {
    await expect(importWithReload(() => Promise.reject(new Error('boom')))()).rejects.toThrow(
      'boom',
    );
    window.sessionStorage.setItem(CHUNK_RELOAD_KEY, String(now - 1000));
    await expect(
      importWithReload(() => Promise.reject(new TypeError('Importing a module script failed.')))(),
    ).rejects.toThrow('Importing a module script failed.');
    expect(reload).not.toHaveBeenCalled();
  });

  it("importWithReload: muvaffaqiyatli import o'zgarmaydi", async () => {
    await expect(importWithReload(() => Promise.resolve({ default: 1 }))()).resolves.toEqual({
      default: 1,
    });
  });

  it('vite:preloadError hodisasi → bir marta reload', () => {
    installChunkReloadGuard();
    installChunkReloadGuard(); // ikki marta o'rnatilsa ham bitta listener
    window.dispatchEvent(new Event('vite:preloadError', { cancelable: true }));
    window.dispatchEvent(new Event('vite:preloadError', { cancelable: true }));
    expect(reload).toHaveBeenCalledTimes(1);
  });
});
