import { http, HttpResponse, type HttpHandler } from 'msw';
import { STUDENT_ENDPOINTS } from '@/shared/api/endpoints';
import { problem, requireBearer } from '@/mocks/problem';
import { PHOTO_CONTENT_TYPES, PHOTO_MAX_BYTES } from './photo';
import { isCheckedIn, isFinished, type TodayDto } from './types';

/** Mock korxona koordinatasi (SPEC isJoyim: 41.3111, 69.2797) — dev'da DevTools → Sensors bilan qo'ying. */
export const MOCK_PLACE = { lat: 41.3111, lng: 69.2797, radiusM: 150 } as const;

/** Haversine (m). */
export function distanceMeters(aLat: number, aLng: number, bLat: number, bLng: number): number {
  const R = 6_371_000;
  const toRad = (d: number) => (d * Math.PI) / 180;
  const dLat = toRad(bLat - aLat);
  const dLng = toRad(bLng - aLng);
  const s =
    Math.sin(dLat / 2) ** 2 +
    Math.cos(toRad(aLat)) * Math.cos(toRad(bLat)) * Math.sin(dLng / 2) ** 2;
  return 2 * R * Math.asin(Math.sqrt(s));
}

/** Kontrakt v2 shakli (backend `GetStudentTodayQuery` javobi bilan bir xil). */
function initialToday(): TodayDto {
  return {
    date: '2026-10-12',
    window: { start: '09:00', end: '09:15', closesAt: '10:30', checkoutAt: '17:00', isOpen: true },
    checkin: {
      status: 'pending',
      checkInAt: null,
      checkOutAt: null,
      distanceM: 45,
      radiusM: MOCK_PLACE.radiusM,
      gpsAccuracyM: 12,
      suspicious: false,
      autoClosed: false,
      note: null,
    },
    place: {
      company: 'Tech Solutions MChJ',
      address: "Toshkent, Amir Temur ko'chasi 108",
      radiusM: MOCK_PLACE.radiusM,
      attendancePct: 94,
      daysPresent: 34,
      daysTotal: 36,
      reports: 32,
      avgScore: 4.2,
    },
    diary: { submittedToday: false, minChars: 150, maxFiles: 5 },
  };
}

export let mockToday: TodayDto = initialToday();

/**
 * Backend sozlamasi `checkinPhotoRequired` (SettingKeys, default "false") ko'zgusi —
 * testda `setCheckinPhotoRequired(true)` bilan yoqiladi.
 */
export let mockCheckinPhotoRequired = false;

/** Oxirgi qabul qilingan selfie (test tekshiruvi uchun). */
export let lastCheckinPhoto: { name: string; type: string; size: number } | null = null;

export function setCheckinPhotoRequired(value: boolean) {
  mockCheckinPhotoRequired = value;
}

export function resetTodayMocks() {
  mockToday = initialToday();
  mockCheckinPhotoRequired = false;
  lastCheckinPhoto = null;
}

/** Kundalik yuborilganda bosh ekran hisoblagichini yangilash (diary mock chaqiradi). */
export function markDiarySubmitted() {
  mockToday = {
    ...mockToday,
    place: mockToday.place && { ...mockToday.place, reports: mockToday.place.reports + 1 },
    diary: { ...mockToday.diary, submittedToday: true },
  };
}

interface ParsedCheckinForm {
  lat: number;
  lng: number;
  accuracy: number;
  occurredAt: string;
  photo: File | null;
}

/**
 * Kontrakt §1.3 — multipart/form-data: `lat`, `lng`, `accuracy`, `occurredAt`, ixtiyoriy `photo`.
 * Xato → 400 ProblemDetails (`errors.Lat` / `errors.Photo`) — backend validatori bilan bir xil.
 */
async function parseCheckinForm(
  request: Request,
): Promise<{ form: ParsedCheckinForm } | { response: Response }> {
  const data = await request.formData().catch(() => null);
  if (!data) {
    return {
      response: problem(400, "Ma'lumotlar noto'g'ri", "Kiritilgan ma'lumotlarda xatolik bor.", {
        errors: { Lat: ["Kenglik (lat) -90 va 90 oralig'ida bo'lishi kerak."] },
      }),
    };
  }
  const num = (key: string) => {
    const raw = data.get(key);
    return typeof raw === 'string' && raw.trim() !== '' ? Number(raw) : Number.NaN;
  };
  const lat = num('lat');
  const lng = num('lng');
  const accuracy = num('accuracy');
  const occurredAtRaw = data.get('occurredAt');
  const occurredAt = typeof occurredAtRaw === 'string' ? occurredAtRaw : '';
  if (
    !Number.isFinite(lat) ||
    !Number.isFinite(lng) ||
    !Number.isFinite(accuracy) ||
    occurredAt === ''
  ) {
    return {
      response: problem(400, "Ma'lumotlar noto'g'ri", "Kiritilgan ma'lumotlarda xatolik bor.", {
        errors: { Lat: ["Kenglik (lat) -90 va 90 oralig'ida bo'lishi kerak."] },
      }),
    };
  }

  // MSW (undici) `File` ni o'z realmida yaratadi — `instanceof File` ishonchsiz, shuning uchun
  // matn bo'lmagan qiymat fayl deb qabul qilinadi.
  const raw = data.get('photo');
  const photo = raw !== null && typeof raw !== 'string' && raw.size > 0 ? raw : null;
  if (photo) {
    if (!(PHOTO_CONTENT_TYPES as readonly string[]).includes(photo.type.toLowerCase())) {
      return {
        response: problem(400, "Ma'lumotlar noto'g'ri", "Kiritilgan ma'lumotlarda xatolik bor.", {
          errors: { Photo: ['Rasm formati qabul qilinmaydi (JPEG, PNG, WEBP, HEIC).'] },
        }),
      };
    }
    if (photo.size > PHOTO_MAX_BYTES) {
      return {
        response: problem(400, "Ma'lumotlar noto'g'ri", "Kiritilgan ma'lumotlarda xatolik bor.", {
          errors: { Photo: ['Rasm hajmi 5 MB dan oshmasligi kerak.'] },
        }),
      };
    }
  } else if (mockCheckinPhotoRequired) {
    return {
      response: problem(400, "Ma'lumotlar noto'g'ri", 'Check-in uchun rasm majburiy.', {
        errors: { Photo: ['Check-in uchun rasm majburiy.'] },
      }),
    };
  }

  lastCheckinPhoto = photo ? { name: photo.name, type: photo.type, size: photo.size } : null;
  return { form: { lat, lng, accuracy, occurredAt, photo } };
}

/**
 * Check-in mock (multipart/form-data): haqiqiy masofa hisoblanadi (haversine); radius tashqarisi → 409 ProblemDetails
 * ("Ziddiyat"), allaqachon belgilangan → 409, oyna yopiq → 400 ("Noto'g'ri amal") — backend
 * `CheckInPolicy` xabarlari bilan bir xil. `occurredAt` soati 09:15 dan keyin → `late`
 * (mock soddaligi uchun UTC+5 qo'lda).
 */
export const todayHandlers: HttpHandler[] = [
  http.get(STUDENT_ENDPOINTS.today, ({ request }) => {
    return requireBearer(request) ?? HttpResponse.json(mockToday);
  }),

  http.post(STUDENT_ENDPOINTS.checkin, async ({ request }) => {
    const denied = requireBearer(request);
    if (denied) return denied;
    const parsed = await parseCheckinForm(request);
    if ('response' in parsed) return parsed.response;
    const body = parsed.form;
    if (mockToday.checkin.checkInAt) {
      return problem(409, 'Ziddiyat', 'Bugun allaqachon belgilangansiz.');
    }
    if (!mockToday.window.isOpen) {
      return problem(400, "Noto'g'ri amal", 'Bugungi belgilanish oynasi yopilgan.');
    }
    const distanceM = Math.round(
      distanceMeters(body.lat, body.lng, MOCK_PLACE.lat, MOCK_PLACE.lng),
    );
    if (distanceM > MOCK_PLACE.radiusM) {
      mockToday = {
        ...mockToday,
        checkin: { ...mockToday.checkin, distanceM, gpsAccuracyM: Math.round(body.accuracy) },
      };
      return problem(
        409,
        'Ziddiyat',
        `Korxona radiusidan tashqaridasiz: ${distanceM} m / ${MOCK_PLACE.radiusM} m. Urinish tyutorga ko'rinadi.`,
      );
    }
    const at = new Date(body.occurredAt);
    const tashkentMinutes = ((at.getUTCHours() + 5) % 24) * 60 + at.getUTCMinutes();
    const late = tashkentMinutes > 9 * 60 + 15;
    mockToday = {
      ...mockToday,
      checkin: {
        ...mockToday.checkin,
        status: late ? 'late' : 'present',
        checkInAt: body.occurredAt,
        distanceM,
        gpsAccuracyM: Math.round(body.accuracy),
        note: null,
      },
    };
    return HttpResponse.json(mockToday);
  }),

  http.post(STUDENT_ENDPOINTS.checkout, async ({ request }) => {
    const denied = requireBearer(request);
    if (denied) return denied;
    const parsed = await parseCheckinForm(request);
    if ('response' in parsed) return parsed.response;
    const body = parsed.form;
    if (isFinished(mockToday.checkin)) {
      return problem(409, 'Ziddiyat', 'Bugun allaqachon ketganingiz belgilangan.');
    }
    if (!isCheckedIn(mockToday.checkin)) {
      return problem(409, 'Ziddiyat', 'Avval kelganingizni belgilang (KELDIM).');
    }
    const distanceM = Math.round(
      distanceMeters(body.lat, body.lng, MOCK_PLACE.lat, MOCK_PLACE.lng),
    );
    mockToday = {
      ...mockToday,
      checkin: {
        ...mockToday.checkin,
        checkOutAt: body.occurredAt,
        distanceM,
        gpsAccuracyM: Math.round(body.accuracy),
      },
    };
    return HttpResponse.json(mockToday);
  }),
];
