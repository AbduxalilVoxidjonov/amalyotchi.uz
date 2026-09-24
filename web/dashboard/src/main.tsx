import { StrictMode } from 'react';
import { createRoot } from 'react-dom/client';
import { App } from '@/app/App';
import { installChunkReloadGuard } from '@/shared/lib/chunk-reload';
import { env } from '@/shared/lib/env';
import '@/styles/globals.css';

// Deploydan keyin eski chunk (404) → bir marta avtomatik qayta yuklash (Vite `vite:preloadError`).
installChunkReloadGuard();

async function enableMocks() {
  if (!env.useMocks) return;
  const { worker } = await import('@/mocks/browser');
  await worker.start({ onUnhandledRequest: 'bypass' });
}

void enableMocks().then(() => {
  createRoot(document.getElementById('root')!).render(
    <StrictMode>
      <App />
    </StrictMode>,
  );
});
