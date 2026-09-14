/// <reference types="vite/client" />

interface ImportMetaEnv {
  readonly VITE_API_URL?: string;
  readonly VITE_USE_MOCKS?: string;
  readonly VITE_DEV_API_TARGET?: string;
  /** Faqat dev: Telegram tashqarisida sinash uchun imzolangan initData (scratchpad/twa-initdata.mjs). */
  readonly VITE_DEV_INIT_DATA?: string;
}

interface ImportMeta {
  readonly env: ImportMetaEnv;
}
