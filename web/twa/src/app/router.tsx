import { lazy } from 'react';
import { createBrowserRouter, createMemoryRouter, type RouteObject } from 'react-router-dom';
import { ForcePasswordScreen } from '@/features/auth/components/ForcePasswordScreen';
import { LoginScreen } from '@/features/auth/components/LoginScreen';
import { useAutoLogin } from '@/features/auth/hooks';
import { errorMessage, isApiError } from '@/shared/api/client';
import { useSessionFlags } from '@/shared/auth/session';
import { Button, LoadingState } from '@/shared/ui';
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
const ProfilePage = lazy(() => import('@/pages/ProfilePage'));

/**
 * Ildiz: sessiya holatiga qarab darvoza, keyin mobil shell.
 *  - Telegram rejimi (initData / mock): avtomatik kirish. Kira olmasa xabar:
 *      403 (hisob bog'lanmagan / imzo / faol emas) → "Telegram hisobingiz bog'lanmagan" + `detail`;
 *      foydalanuvchi o'zi chiqqan → "Qayta kirish" tugmasi (avtomatik qayta kirilmaydi).
 *  - Web rejimi (oddiy brauzer yoki `?web=1`): HEMIS ID + parol login sahifasi.
 *  - `mustChangePassword` → "Yangi parol o'rnating" ekrani (ilovaga o'tkazilmaydi).
 */
function RootLayout() {
  const { status, mode, loggedOut, isPending, error, relogin } = useAutoLogin();
  const mustChangePassword = useSessionFlags((s) => s.mustChangePassword);
  const forbidden = isApiError(error) && error.kind === 'forbidden';

  if (status === 'restoring' || isPending) {
    return (
      <div className={styles.gate}>
        <LoadingState height={200} label="Kirilmoqda…" />
      </div>
    );
  }
  if (status === 'anonymous') {
    if (mode === 'web') return <LoginScreen loggedOut={loggedOut} />;
    if (loggedOut) {
      return (
        <main className={styles.gate}>
          <h1 className={styles.gateTitle}>Hisobdan chiqdingiz</h1>
          <p className={styles.gateText}>Ilovadan foydalanish uchun qayta kiring.</p>
          <div className={styles.gateActions}>
            <Button variant="primary" size="lg" block onClick={relogin}>
              Qayta kirish
            </Button>
          </div>
        </main>
      );
    }
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
  if (mustChangePassword) return <ForcePasswordScreen />;
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
      { path: '/profil', element: <ProfilePage /> },
      { path: '*', element: <NotFoundPage /> },
    ],
  },
];

export const createAppRouter = () => createBrowserRouter(routes);
export const createTestRouter = (initialEntries: string[] = ['/']) =>
  createMemoryRouter(routes, { initialEntries });
