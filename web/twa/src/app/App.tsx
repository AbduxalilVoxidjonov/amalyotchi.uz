import { RouterProvider } from 'react-router-dom';
import { AppProviders } from './providers';
import { diagRoute } from '@/shared/lib/diag';
import { createAppRouter } from './router';

const router = createAppRouter();
// Diagnostika: har bir route o'zgarishi (pathname + performance.now()).
let lastPath = router.state.location.pathname;
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
