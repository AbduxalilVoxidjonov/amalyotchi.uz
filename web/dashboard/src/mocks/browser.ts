import { setupWorker } from 'msw/browser';
import { handlers } from './handlers';

/**
 * Brauzer uchun MSW worker. `VITE_USE_MOCKS=true` bo'lganda main.tsx ishga tushiradi.
 * `public/mockServiceWorker.js` — `npm run msw:init` bilan yaratilgan (msw versiyasi
 * yangilanganda qayta ishga tushiring).
 */
export const worker = setupWorker(...handlers);
