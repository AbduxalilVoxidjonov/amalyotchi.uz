/// <reference types="vite/client" />

interface ImportMetaEnv {
  readonly VITE_API_URL?: string;
  readonly VITE_USE_MOCKS?: string;
  readonly VITE_DEV_API_TARGET?: string;
  /** Talaba ilovasi (TWA) manzili — talabaga beriladigan kirish havolasi. Bo'sh bo'lsa ko'rsatilmaydi. */
  readonly VITE_TWA_URL?: string;
}

interface ImportMeta {
  readonly env: ImportMetaEnv;
}
