import { StrictMode } from 'react';
import { createRoot } from 'react-dom/client';
import { App } from '@/app/App';
import { initTelegram } from '@/shared/auth/telegram';
import { installChunkReloadGuard } from '@/shared/lib/chunk-reload';
import { installDiagnostics } from '@/shared/lib/diag';
import { env } from '@/shared/lib/env';
import { applyPlatformAttr } from '@/shared/lib/platform';
import '@/styles/globals.css';

// Production diagnostikasi (boot/route/xato beacon'lari → /api/__diag) — birinchi bo'lib.
installDiagnostics();
// Deploydan keyin eski chunk (404) → bir marta avtomatik qayta yuklash (Vite `vite:preloadError`).
installChunkReloadGuard();

async function enableMocks() {
  if (!env.useMocks) return;
  const { worker } = await import('@/mocks/browser');
  await worker.start({ onUnhandledRequest: 'bypass' });
}

void enableMocks().then(() => {
  initTelegram();
  applyPlatformAttr();
  createRoot(document.getElementById('root')!).render(
    <StrictMode>
      <App />
    </StrictMode>,
  );
});
