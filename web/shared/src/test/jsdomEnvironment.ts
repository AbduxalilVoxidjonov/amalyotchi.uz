/**
 * Vitest uchun jsdom muhiti — Node (22–26) bilan realm/global ziddiyatlarisiz.
 *
 * Ulanishi (dashboard/twa `vite.config.ts`):
 *   test: { environment: '../shared/src/test/jsdomEnvironment.ts' }
 *
 * O'rnatilgan `jsdom` muhitining ustiga ikki tuzatish:
 *
 * 1. `localStorage` / `sessionStorage`. Node 22.4+ (bayroq bilan) va Node 25+ (standart) o'zining
 *    Web Storage global'larini beradi. Vitest `populateGlobal` global'da allaqachon bor kalitni
 *    faqat o'z ro'yxatida bo'lsa almashtiradi — Storage kalitlari u yerda yo'q. Natijada Node'ning
 *    (`--localstorage-file` siz — `undefined`) qiymati qoladi. Bu yerda ular jsdom oynasinikiga
 *    aniq bog'lanadi — `--no-experimental-webstorage` bayrog'i shart emas.
 *
 * 2. `File` / `Blob` / `FormData`. Node ichidagi undici multipart parseri (`Request#formData()`)
 *    yangi qismni *global* `File` bilan yaratadi, lekin `webidl.is.File` tekshiruvi yuklanish
 *    paytidagi native `File` ga bog'langan. jsdom global `File` ni o'zinikiga almashtirgani uchun
 *    parser jsdom `File` yaratadi va assert yiqiladi (MSW handler'dagi `request.formData()`).
 *    Yechim: bu uchala konstruktor Node'ning native'lari bo'lib qoladi — yagona realm. Ilova
 *    `new FormData()` + `new File()` ni native fetch bilan yuboradi, MSW native parser bilan o'qiydi.
 *    `<input type=file>` + `userEvent.upload` bunga bog'liq emas (FileList'ni o'zi yasaydi).
 */
import { builtinEnvironments, type Environment } from 'vitest/environments';

const NATIVE_KEYS = ['File', 'Blob', 'FormData'] as const;
const STORAGE_KEYS = ['localStorage', 'sessionStorage'] as const;
type GlobalKey = (typeof NATIVE_KEYS)[number] | (typeof STORAGE_KEYS)[number];

const jsdom = builtinEnvironments.jsdom;

const environment: Environment = {
  name: 'jsdom-node',
  viteEnvironment: jsdom.viteEnvironment ?? 'client',
  async setup(global, options) {
    const target = global as Record<GlobalKey, unknown> & { jsdom?: { window: Window } };
    const saved = new Map<GlobalKey, PropertyDescriptor | undefined>();
    for (const key of [...NATIVE_KEYS, ...STORAGE_KEYS]) {
      saved.set(key, Object.getOwnPropertyDescriptor(target, key));
    }
    const natives = Object.fromEntries(NATIVE_KEYS.map((key) => [key, target[key]]));

    const base = await jsdom.setup(global, options);

    const win = target.jsdom?.window;
    if (!win) throw new Error('jsdom muhiti `global.jsdom` ni o‘rnatmadi.');

    const define = (key: GlobalKey, get: () => unknown) =>
      Object.defineProperty(target, key, { get, configurable: true, enumerable: true });
    for (const key of NATIVE_KEYS) define(key, () => natives[key]);
    // Getter — jsdom Storage'ga har chaqiruvda murojaat (window yopilgach ham xato bermaydi).
    for (const key of STORAGE_KEYS) define(key, () => win[key]);

    return {
      async teardown(g) {
        await base.teardown(g);
        for (const [key, descriptor] of saved) {
          if (descriptor) Object.defineProperty(target, key, descriptor);
          else delete target[key];
        }
      },
    };
  },
};

export default environment;
