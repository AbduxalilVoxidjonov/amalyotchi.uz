import { setupServer } from 'msw/node';
import { handlers } from './handlers';

/** Vitest (node/jsdom) uchun MSW server. `src/test/setup.ts` da ulanadi. */
export const server = setupServer(...handlers);
