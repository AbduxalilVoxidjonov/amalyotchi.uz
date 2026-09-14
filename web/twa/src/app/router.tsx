import { lazy } from 'react';
import { createBrowserRouter, createMemoryRouter, type RouteObject } from 'react-router-dom';
import { useAutoLogin } from '@/features/auth/hooks';
import { errorMessage, isApiError } from '@/shared/api/client';
import { LoadingState } from '@/shared/ui';
import { NotFoundPage } from '@/pages/NotFoundPage';
import { AppShell } from './layout/AppShell';
import styles from './router.module.css';

// SPEC-NAV 3.2 — talaba marshrutlari. Sahifalar lazy (bundle bo'linadi).
const HomePage = lazy(() => import('@/pages/HomePage'));
const PlacePage = lazy(() => import('@/pages/PlacePage'));
const DiaryPage = lazy(() => import('@/pages/DiaryPage'));
const CalendarPage = lazy(() => import('@/pages/CalendarPage'));
const LeaveRequestPage = lazy(() => import('@/pages/LeaveRequestPage'));
const PortfolioPage = lazy(() => import('@/pages/PortfolioPage'));

/**
 * Ildiz: Telegram initData bilan avtomatik kirish, keyin mobil shell.
 * TWA'da "login sahifasi" yo'q — kira olmasa xabar ko'rsatiladi:
 *   403 (backend: hisob bog'lanmagan / imzo / faol emas) → "Telegram hisobingiz bog'lanmagan" + `detail`;
 *   initData yo'q (Telegram tashqarisida) → "Ilovani Telegram bot orqali oching".
 */
function RootLayout() {
  const { status, isPending, error } = useAutoLogin();
  const forbidden = isApiError(error) && error.kind === 'forbidden';

  if (status === 'restoring' || isPending) {
    return (
      <div className={styles.gate}>
        <LoadingState height={200} label="Kirilmoqda…" />
      </div>
    );
  }
  if (status === 'anonymous') {
    return (
      <main className={styles.gate}>
        <h1 className={styles.gateTitle}>
          {forbidden ? "Telegram hisobingiz bog'lanmagan" : "Kirish imkoni yo'q"}
        </h1>
        <p className={styles.gateText}>
          {error ? errorMessage(error) : 'Ilovani Telegram bot orqali oching.'}
        </p>
        {forbidden && (
          <p className={styles.gateText}>
            Tyutoringizdan taklif havolasini olib, botda ro'yxatdan o'ting, so'ng ilovani qaytadan
            oching.
          </p>
        )}
      </main>
    );
  }
  return <AppShell />;
}

export const routes: RouteObject[] = [
  {
    element: <RootLayout />,
    children: [
      { path: '/', element: <HomePage /> },
      { path: '/joyim', element: <PlacePage /> },
      { path: '/kundalik', element: <DiaryPage /> },
      { path: '/kalendar', element: <CalendarPage /> },
      { path: '/ruxsat', element: <LeaveRequestPage /> },
      { path: '/portfolio', element: <PortfolioPage /> },
      { path: '*', element: <NotFoundPage /> },
    ],
  },
];

export const createAppRouter = () => createBrowserRouter(routes);
export const createTestRouter = (initialEntries: string[] = ['/']) =>
  createMemoryRouter(routes, { initialEntries });
