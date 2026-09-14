/**
 * Check-in/check-out uchun joylashuv. `navigator.geolocation` ishlatiladi —
 * Telegram WebView'da brauzer ruxsat so'rovi chiqadi (Telegram 8.0+ da `WebApp.LocationManager`
 * ham bor, lekin barcha mijozlarda yo'q ❓ — hozircha faqat navigator).
 *
 * TODO(offline): tarmoq yo'q bo'lsa urinish navbatga qo'yilib keyin yuborilishi kerak
 * (IndexedDB navbat + retry). Bu bosqichda qo'shilmadi — faqat xato ko'rsatiladi.
 */

export interface GeoPoint {
  lat: number;
  lng: number;
  /** metr */
  accuracy: number;
  /** ISO 8601 — qurilma vaqti (server o'z vaqtini ham yozadi). */
  occurredAt: string;
}

export type GeoErrorKind = 'unsupported' | 'denied' | 'unavailable' | 'timeout';

export class GeoError extends Error {
  readonly kind: GeoErrorKind;
  constructor(kind: GeoErrorKind, message: string) {
    super(message);
    this.name = 'GeoError';
    this.kind = kind;
  }
}

export const GEO_MESSAGES: Record<GeoErrorKind, string> = {
  unsupported: 'Bu qurilmada joylashuvni aniqlab bo‘lmaydi.',
  denied:
    'Joylashuvga ruxsat berilmadi. Telegram va telefon sozlamalarida joylashuvga ruxsat bering.',
  unavailable: 'Joylashuv aniqlanmadi. GPS yoqilganini tekshirib, qayta urinib ko‘ring.',
  timeout: 'Joylashuvni aniqlash vaqti tugadi. Ochiq joyda qayta urinib ko‘ring.',
};

export function isGeoError(e: unknown): e is GeoError {
  return e instanceof GeoError;
}

export function getCurrentPosition(timeoutMs = 15_000): Promise<GeoPoint> {
  return new Promise((resolve, reject) => {
    const geo = typeof navigator !== 'undefined' ? navigator.geolocation : undefined;
    if (!geo) {
      reject(new GeoError('unsupported', GEO_MESSAGES.unsupported));
      return;
    }
    geo.getCurrentPosition(
      (pos) =>
        resolve({
          lat: pos.coords.latitude,
          lng: pos.coords.longitude,
          accuracy: pos.coords.accuracy,
          occurredAt: new Date(pos.timestamp || Date.now()).toISOString(),
        }),
      (err) => {
        // GeolocationPositionError: 1 PERMISSION_DENIED, 2 POSITION_UNAVAILABLE, 3 TIMEOUT
        const kind: GeoErrorKind =
          err.code === 1 ? 'denied' : err.code === 3 ? 'timeout' : 'unavailable';
        reject(new GeoError(kind, GEO_MESSAGES[kind]));
      },
      { enableHighAccuracy: true, timeout: timeoutMs, maximumAge: 0 },
    );
  });
}
