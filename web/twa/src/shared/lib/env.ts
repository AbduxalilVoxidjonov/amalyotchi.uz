const isDev = import.meta.env.DEV;
const isTest = import.meta.env.MODE === 'test';

/**
 * Dev'da (oddiy brauzer, Telegram tashqarisida) initData'ni qo'lda berish:
 *   1) `?initData=<url-encoded>` query (ustun) · 2) `VITE_DEV_INIT_DATA` (.env.development.local).
 * Xavfsizlik: FAQAT `import.meta.env.DEV` — production bundle'da bu shox umuman yo'q (dead-code),
 * testda ham o'chiq. Generatsiya: `node scratchpad/twa-initdata.mjs <telegramUserId>`.
 */
function readDevInitData(): string {
  if (!isDev || isTest) return '';
  try {
    const fromQuery = new URLSearchParams(window.location.search).get('initData');
    if (fromQuery) return fromQuery;
  } catch {
    /* window yo'q */
  }
  return (import.meta.env.VITE_DEV_INIT_DATA as string | undefined)?.trim() ?? '';
}

/**
 * Telegram tashqarisida web-login (HEMIS ID + parol) sahifasini majburan ko'rsatish:
 *   `?web=1` query yoki `VITE_WEB_LOGIN=true`. Mock rejimida (MSW) va dev initData bor bo'lsa ham
 * avtomatik Telegram-login o'rniga login sahifasi ochiladi. Haqiqiy Telegram ichida ta'sir qilmaydi.
 */
function readForceWebLogin(): boolean {
  if (isTest) return false;
  try {
    const q = new URLSearchParams(window.location.search).get('web');
    if (q === '1' || q === 'true') return true;
  } catch {
    /* window yo'q */
  }
  return import.meta.env.VITE_WEB_LOGIN === 'true';
}

export const env = {
  apiUrl: (import.meta.env.VITE_API_URL as string | undefined)?.replace(/\/+$/, '') ?? '',
  useMocks: import.meta.env.VITE_USE_MOCKS === 'true',
  isDev,
  isTest,
  devInitData: readDevInitData(),
  forceWebLogin: readForceWebLogin(),
} as const;
