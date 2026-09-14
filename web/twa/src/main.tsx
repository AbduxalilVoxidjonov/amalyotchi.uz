import { StrictMode } from 'react';
import { createRoot } from 'react-dom/client';
import { App } from '@/app/App';
import { initTelegram } from '@/shared/auth/telegram';
import { env } from '@/shared/lib/env';
import '@/styles/globals.css';

async function enableMocks() {
  if (!env.useMocks) return;
  const { worker } = await import('@/mocks/browser');
  await worker.start({ onUnhandledRequest: 'bypass' });
}

void enableMocks().then(() => {
  initTelegram();
  createRoot(document.getElementById('root')!).render(
    <StrictMode>
      <App />
    </StrictMode>,
  );
});
