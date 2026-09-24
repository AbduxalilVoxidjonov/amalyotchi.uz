import { RouterProvider } from 'react-router-dom';
import { authMode } from '@/shared/auth/mode';
import { diagRoute, setDiagRoutePath } from '@/shared/lib/diag';
import { AppProviders } from './providers';
import { createAppRouter } from './router';
import { bindTelegramBackButton } from './telegram-back-button';

// Rejim ilova ishga tushganda bir marta aniqlanadi: Telegram → memory router (URL o'zgarmaydi), web → browser.
const mode = authMode();
const router = createAppRouter(mode);
if (mode === 'telegram') bindTelegramBackButton(router);

// Diagnostika: har bir route o'zgarishi (router pathname + performance.now()).
let lastPath = router.state.location.pathname;
setDiagRoutePath(lastPath);
router.subscribe((state) => {
  if (state.location.pathname === lastPath) return;
  lastPath = state.location.pathname;
  diagRoute(lastPath);
});

export function App() {
  return (
    <AppProviders>
      <RouterProvider router={router} />
    </AppProviders>
  );
}

export default App;
