import {
  CHUNK_RELOAD_KEY,
  RELOAD_COOLDOWN_MS,
  chunkReloadDeps,
  installChunkReloadGuard,
  reloadOnceForChunkError,
  resetChunkReloadState,
} from './chunk-reload';

describe('dashboard chunk-reload', () => {
  let reload: ReturnType<typeof vi.fn<() => void>>;
  let now = 5_000_000;

  beforeEach(() => {
    window.sessionStorage.clear();
    resetChunkReloadState();
    reload = vi.fn<() => void>();
    vi.spyOn(chunkReloadDeps, 'reload').mockImplementation(reload);
    vi.spyOn(chunkReloadDeps, 'now').mockImplementation(() => now);
  });
  afterEach(() => {
    vi.restoreAllMocks();
    window.sessionStorage.clear();
  });

  it("vite:preloadError → bir marta reload (qayta hodisa — yana reload yo'q)", () => {
    installChunkReloadGuard();
    window.dispatchEvent(new Event('vite:preloadError', { cancelable: true }));
    window.dispatchEvent(new Event('vite:preloadError', { cancelable: true }));
    expect(reload).toHaveBeenCalledTimes(1);
    expect(window.sessionStorage.getItem(CHUNK_RELOAD_KEY)).toBe(String(now));
  });

  it("reload'dan keyin cooldown ichida — loop yo'q; cooldown o'tgach — ruxsat", () => {
    window.sessionStorage.setItem(CHUNK_RELOAD_KEY, String(now - 1_000));
    expect(reloadOnceForChunkError()).toBe(false);
    now += RELOAD_COOLDOWN_MS;
    expect(reloadOnceForChunkError()).toBe(true);
    expect(reload).toHaveBeenCalledTimes(1);
  });

  it('sessionStorage ishlamasa — reload qilinmaydi', () => {
    vi.spyOn(Storage.prototype, 'getItem').mockImplementation(() => {
      throw new DOMException('denied', 'SecurityError');
    });
    expect(reloadOnceForChunkError()).toBe(false);
    expect(reload).not.toHaveBeenCalled();
  });
});
