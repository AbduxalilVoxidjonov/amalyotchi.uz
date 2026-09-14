import { QueryClient } from '@tanstack/react-query';
import { render } from '@testing-library/react';
import { RouterProvider } from 'react-router-dom';
import { AppProviders } from '@/app/providers';
import { createTestRouter } from '@/app/router';
import { setInitData } from './telegram-stub';

/** Sahifani to'liq shell ichida (auto-login + tab-bar) render qiladi. */
export function renderApp(path: string, { initData = 'valid' }: { initData?: string } = {}) {
  setInitData(initData);
  const router = createTestRouter([path]);
  const queryClient = new QueryClient({
    defaultOptions: { queries: { retry: false }, mutations: { retry: false } },
  });
  render(
    <AppProviders queryClient={queryClient}>
      <RouterProvider router={router} />
    </AppProviders>,
  );
  return router;
}

export interface GeoStub {
  lat?: number;
  lng?: number;
  accuracy?: number;
  /** ISO — check-in vaqti (mock Toshkent 09:15 dan keyin → "Kech keldi"). */
  at?: string;
  /** 1 = PERMISSION_DENIED · 2 = POSITION_UNAVAILABLE · 3 = TIMEOUT */
  errorCode?: 1 | 2 | 3;
}

/** `navigator.geolocation` stub'i (jsdom'da yo'q). Korxona mock koordinatasi: 41.3111, 69.2797. */
export function stubGeolocation({
  lat = 41.3113,
  lng = 69.2799,
  accuracy = 12,
  at = '2026-10-12T04:02:00Z',
  errorCode,
}: GeoStub = {}) {
  const getCurrentPosition = vi.fn((ok: PositionCallback, fail?: PositionErrorCallback | null) => {
    if (errorCode) {
      fail?.({
        code: errorCode,
        message: 'stub',
        PERMISSION_DENIED: 1,
        POSITION_UNAVAILABLE: 2,
        TIMEOUT: 3,
      } as GeolocationPositionError);
      return;
    }
    ok({
      coords: {
        latitude: lat,
        longitude: lng,
        accuracy,
        altitude: null,
        altitudeAccuracy: null,
        heading: null,
        speed: null,
        toJSON: () => ({}),
      },
      timestamp: new Date(at).getTime(),
      toJSON: () => ({}),
    } as GeolocationPosition);
  });
  Object.defineProperty(navigator, 'geolocation', {
    configurable: true,
    value: { getCurrentPosition, watchPosition: vi.fn(), clearWatch: vi.fn() },
  });
  return getCurrentPosition;
}

export function removeGeolocation() {
  Object.defineProperty(navigator, 'geolocation', { configurable: true, value: undefined });
}
