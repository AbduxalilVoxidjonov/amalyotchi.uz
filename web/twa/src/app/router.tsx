import { lazy } from 'react';
import {
  createBrowserRouter,
  createMemoryRouter,
  Navigate,
  type RouteObject,
} from 'react-router-dom';
import { ForcePasswordScreen } from '@/features/auth/components/ForcePasswordScreen';
import { LoginScreen } from '@/features/auth/components/LoginScreen';
import { submitTelegramLink, telegramLinkErrorHint, useAutoLogin } from '@/features/auth/hooks';
import { errorMessage, isApiError } from '@/shared/api/client';
import { importWithReload } from '@/shared/lib/chunk-reload';
import { useSessionFlags } from '@/shared/auth/session';
import { Button, LoadingState } from '@/shared/ui';
import { NotFoundPage } from '@/pages/NotFoundPage';
import { AppShell } from './layout/AppShell';
import { RouteError } from './RouteError';
import styles from './router.module.css';

// SPEC-NAV 3.2 — talaba marshrutlari. Sahifalar lazy (bundle bo'linadi).
// `importWithReload`: deploydan keyin eski hash'li chunk 404 bo'lsa — sahifa bir marta qayta yuklanadi.
const HomePage = lazy(importWithReload(() => import('@/pages/HomePage')));
const PlacePage = lazy(importWithReload(() => import('@/pages/PlacePage')));
const DiaryPage = lazy(importWithReload(() => import('@/pages/DiaryPage')));
const CalendarPage = lazy(importWithReload(() => import('@/pages/CalendarPage')));
const ProfilePage = lazy(importWithReload(() => import('@/pages/ProfilePage')));

const TELEGRAM_LINK_KEY = ['auth', 'telegram-link'] as const;

/**
 * Ildiz: sessiya holatiga qarab darvoza, keyin mobil shell.
 *  - Telegram rejimi (initData / mock): avtomatik kirish. Kira olmasa:
 *      403 (hisob bog'lanmagan va h.k.) → bog'lash formasi (HEMIS ID + parol → POST /api/auth/telegram/link,
 *        web `LoginScreen` qayta ishlatiladi); boshqa xato (tarmoq, 500) → "Kirish imkoni yo'q" + xabar;
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
    if (forbidden) {
      return (
        <LoginScreen
          title="Hisobingizni bog'lang"
          subtitle="Birinchi marta kiryapsiz. Tyutoringiz bergan HEMIS ID va parolni kiriting — Telegram hisobingiz bog'lanadi va keyingi safar avtomatik kirasiz."
          submitLabel="Bog'lash va kirish"
          note={null}
          submit={submitTelegramLink}
          mutationKey={TELEGRAM_LINK_KEY}
          errorHint={telegramLinkErrorHint}
        />
      );
    }
    return (
      <main className={styles.gate}>
        <h1 className={styles.gateTitle}>Kirish imkoni yo'q</h1>
        <p className={styles.gateText}>
          {error ? errorMessage(error) : 'Ilovani Telegram bot orqali oching.'}
        </p>
      </main>
    );
  }
  if (mustChangePassword) return <ForcePasswordScreen />;
  return <AppShell />;
}

/**
 * Xato chegaralari (errorElement):
 *  - ildiz (RootLayout/AppShell o'zi yiqilsa) — to'liq ekran;
 *  - sahifalar guruhi (pathless) — AppShell `<Outlet />` ichida: bitta sahifa yiqilsa ham header va
 *    tab-bar qoladi, boshqa tabga o'tilganda chegara o'zi tiklanadi (location o'zgaradi).
 */
export const routes: RouteObject[] = [
  {
    element: <RootLayout />,
    errorElement: <RouteError scope="root" />,
    children: [
      {
        errorElement: <RouteError scope="page" />,
        children: [
          { path: '/', element: <HomePage /> },
          { path: '/joyim', element: <PlacePage /> },
          { path: '/kundalik', element: <DiaryPage /> },
          { path: '/kalendar', element: <CalendarPage /> },
          // Eski havolalar: portfolio endi bosh ekranda; "Ruxsat so'rash" olib tashlangan.
          { path: '/portfolio', element: <Navigate to="/" replace /> },
          { path: '/ruxsat', element: <Navigate to="/" replace /> },
          { path: '/profil', element: <ProfilePage /> },
          { path: '*', element: <NotFoundPage /> },
        ],
      },
    ],
  },
];

export const createAppRouter = () => createBrowserRouter(routes);
export const createTestRouter = (initialEntries: string[] = ['/']) =>
  createMemoryRouter(routes, { initialEntries });
