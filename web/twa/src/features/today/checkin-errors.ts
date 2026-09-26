import { errorMessage, isApiError } from '@/shared/api/client';

/**
 * Check-in/check-out server xatosidan keyin oqim qaysi qadamga qaytadi (kontrakt §2.6, `CheckInRejectReason`):
 *  - `qr` — QR rad etildi (400 `errors.Qr` yoki 409 `qrInvalid` "QR kod bu amaliyot joyiga tegishli emas.");
 *  - `photo` — selfi rad etildi (400 `errors.Photo`);
 *  - `location` — joylashuv: 400 `poorAccuracy` ("GPS aniqligi yetarli emas…"), 409 `outOfRadius`
 *    ("Siz amaliyot joyida emassiz." / mock: "Korxona radiusidan tashqaridasiz…"), 400 `errors.Lat/Lng/Accuracy`;
 *  - `stale` — ekran eskirgan: amal hozir umuman mumkin emas (oyna yopilgan/ochilmagan, ish kuni emas, davr yo'q,
 *    ruxsat kuni, ariza tasdiqlanmagan, allaqachon belgilangan …) — bugungi holat qayta yuklanadi;
 *  - `retry` — tarmoq/server xatosi: shu (joylashuv) qadamida "Qayta urinish".
 * Backend rad sababini (`rejectReason`) ProblemDetails'da yubormaydi — `detail` matni va status bo'yicha ajratiladi.
 */
export type CheckinErrorStep = 'qr' | 'photo' | 'location' | 'stale' | 'retry';

export interface CheckinErrorVerdict {
  step: CheckinErrorStep;
  message: string;
}

const LOCATION_FIELDS = ['lat', 'lng', 'accuracy'] as const;
const LOCATION_TEXT = /GPS|aniqlig|radius|amaliyot joyida emas/i;

/** Server QR'ni rad etdi: 400 `errors.Qr` (yo'q) yoki 409 `qrInvalid` (begona korxona). */
function qrRejection(cause: unknown): string | null {
  if (!isApiError(cause)) return null;
  const field = cause.fieldError('qr');
  if (field) return field;
  if (cause.status !== 409) return null;
  const reason = (cause.problem as { rejectReason?: unknown } | undefined)?.rejectReason;
  return reason === 'qrInvalid' || /\bQR\b/i.test(cause.message) ? cause.message : null;
}

export function classifyCheckinError(cause: unknown): CheckinErrorVerdict {
  if (!isApiError(cause)) return { step: 'retry', message: errorMessage(cause) };

  const qr = qrRejection(cause);
  if (qr) return { step: 'qr', message: qr };

  const photo = cause.fieldError('photo');
  if (photo) return { step: 'photo', message: photo };

  const geoField = LOCATION_FIELDS.map((f) => cause.fieldError(f)).find(Boolean);
  if (geoField) return { step: 'location', message: geoField };

  if (cause.status === 400 || cause.status === 409) {
    if (LOCATION_TEXT.test(cause.message)) return { step: 'location', message: cause.message };
    return { step: 'stale', message: cause.message };
  }
  return { step: 'retry', message: cause.message };
}
