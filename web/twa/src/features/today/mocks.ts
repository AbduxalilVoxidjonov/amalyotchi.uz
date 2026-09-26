import { http, HttpResponse, type HttpHandler } from 'msw';
import { STUDENT_ENDPOINTS } from '@/shared/api/endpoints';
import { problem, requireBearer } from '@/mocks/problem';
import { setMockPlace, setPlaceEnrollmentPeriod } from '@/features/place/mocks';
import { MOCK_AUTUMN_PERIOD, MOCK_GAP_DATE, MOCK_SPRING_PERIOD } from '@/features/period/mocks';
import { periodPhase } from '@/features/period/types';
import { formatDate } from '@/shared/lib/format';
import { PHOTO_CONTENT_TYPES, PHOTO_MAX_BYTES } from './photo';
import { MOCK_TEST_QR } from './qr';
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
      // Sozlamalar ko'zgusi: `mockCheckinPhotoRequired` (false) va `checkinQrRequired` (backend sukuti true).
      photoRequired: false,
      qrRequired: true,
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
    diary: { submittedToday: false, minChars: 150, maxFiles: 5, pdfRequired: false },
    period: MOCK_AUTUMN_PERIOD,
    canWriteDiary: true,
    diaryBlockedReason: null,
  };
}

export let mockToday: TodayDto = initialToday();

/**
 * Backend sozlamasi `checkinPhotoRequired` ko'zgusi (mock sukuti `false` — mavjud testlar rasmsiz oqimni ham
 * tekshiradi) — testda `setCheckinPhotoRequired(true)` bilan yoqiladi (`today.checkin.photoRequired` ham).
 */
export let mockCheckinPhotoRequired = false;

/** Oxirgi qabul qilingan selfie (test tekshiruvi uchun). */
export let lastCheckinPhoto: { name: string; type: string; size: number } | null = null;

export function setCheckinPhotoRequired(value: boolean) {
  mockCheckinPhotoRequired = value;
  mockToday = { ...mockToday, checkin: { ...mockToday.checkin, photoRequired: value } };
}

/** Mock korxonaning QR payload'i — boshqa token → 409 (`qrInvalid`). */
export const MOCK_CHECKIN_QR = MOCK_TEST_QR;

/** Oxirgi qabul qilingan `qr` maydoni (test tekshiruvi uchun). */
export let lastCheckinQr: string | null = null;

/** Backend sozlamasi `checkinQrRequired` (sukut `true`) ko'zgusi — `today.checkin.qrRequired` bilan. */
export function setCheckinQrRequired(value: boolean) {
  mockToday = { ...mockToday, checkin: { ...mockToday.checkin, qrRequired: value } };
}

/** QR xatolari — backend `AttendanceAttempt` xabarlari bilan bir xil. */
export const QR_REQUIRED_MESSAGE = 'Amaliyot joyidagi QR kodni skanerlang.';
export const QR_INVALID_MESSAGE = 'QR kod bu amaliyot joyiga tegishli emas.';

/** Begona token → 409 (`rejectReason: qrInvalid`); aks holda null. */
function qrRejection(qr: string | null): Response | null {
  if (qr === null || qr === MOCK_CHECKIN_QR) return null;
  return problem(409, 'Ziddiyat', QR_INVALID_MESSAGE);
}

/** Backend sozlamasi `diaryPdfRequired` ko'zgusi — testda `setDiaryPdfRequired(true)` bilan yoqiladi. */
export function setDiaryPdfRequired(value: boolean) {
  mockToday = { ...mockToday, diary: { ...mockToday.diary, pdfRequired: value } };
}

/** Backend `diaryBlockedReason` matnlari (`setPeriodGap` bilan today'ga yoziladi). */
export const DIARY_BLOCKED_ENDED_MESSAGE =
  "Amaliyot davri yakunlangan — yangi kundalik yozuvi qo'shib bo'lmaydi.";
export const DIARY_BLOCKED_UPCOMING_MESSAGE = 'Amaliyot davri hali boshlanmagan.';

/**
 * Ikki davr oralig'i (kontrakt v3.5 §4.6, backend `GetStudentTodayQuery` bilan bir xil):
 * - `upcoming` — kuzgi tugagan, bahorgi hali boshlanmagan; bahorgi davrga ariza yo'q (GET place → 404,
 *   POST place → bahorgi davr sanalari bilan);
 * - `ended` — faqat tugagan kuzgi davr (kelgusi yo'q).
 * Ikkalasida ham `window.isOpen=false`, `checkin.status=dayOff`, check-in/check-out → 400 `note` matni bilan.
 */
export function setPeriodGap(kind: 'upcoming' | 'ended') {
  const period =
    kind === 'upcoming' ? { ...MOCK_SPRING_PERIOD, isDefault: true } : MOCK_AUTUMN_PERIOD;
  const note =
    kind === 'upcoming'
      ? `Amaliyot davri hali boshlanmagan: ${period.name}, ${formatDate(period.startDate)} dan boshlanadi.`
      : `Amaliyot davri tugagan: ${period.name}.`;
  mockToday = {
    ...mockToday,
    date: MOCK_GAP_DATE,
    period,
    window: { ...mockToday.window, isOpen: false },
    checkin: {
      ...mockToday.checkin,
      status: 'dayOff',
      checkInAt: null,
      checkOutAt: null,
      distanceM: null,
      radiusM: kind === 'upcoming' ? null : mockToday.checkin.radiusM,
      gpsAccuracyM: null,
      note,
    },
    // `place` — faqat ko'rsatilayotgan davrdagi tasdiqlangan ariza: bahorgi davrga hali ariza yo'q.
    place: kind === 'upcoming' ? null : mockToday.place,
    diary: { ...mockToday.diary, submittedToday: false },
    // Backend: davom etayotgan davr yo'q → kundalik yozib bo'lmaydi (POST diary → 400 shu matn bilan).
    canWriteDiary: false,
    diaryBlockedReason:
      kind === 'upcoming' ? DIARY_BLOCKED_UPCOMING_MESSAGE : DIARY_BLOCKED_ENDED_MESSAGE,
  };
  if (kind === 'upcoming') {
    setMockPlace(null);
    setPlaceEnrollmentPeriod(MOCK_SPRING_PERIOD.startDate, MOCK_SPRING_PERIOD.endDate);
  }
}

/** Davom etayotgan davr yo'q → check-in/check-out 400 (`detail` = today `note`); aks holda null. */
function periodRejection(): Response | null {
  const { period, date, checkin } = mockToday;
  if (period && periodPhase(period, date) === 'ongoing') return null;
  return problem(400, "Noto'g'ri amal", checkin.note ?? "Faol amaliyot davri yo'q.");
}

export function resetTodayMocks() {
  mockToday = initialToday();
  mockCheckinPhotoRequired = false;
  lastCheckinPhoto = null;
  lastCheckinQr = null;
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
  qr: string | null;
}

/**
 * Kontrakt §1.3 — multipart/form-data: `lat`, `lng`, `accuracy`, `occurredAt`, ixtiyoriy `photo` va `qr`.
 * Xato → 400 ProblemDetails (`errors.Lat` / `errors.Photo` / `errors.Qr`) — backend validatori bilan bir xil.
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

  const raw = data.get('photo');
  const photo = raw instanceof File && raw.size > 0 ? raw : null;
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

  const qrRaw = data.get('qr');
  const qr = typeof qrRaw === 'string' && qrRaw.trim() !== '' ? qrRaw.trim() : null;
  if (qr === null && mockToday.checkin.qrRequired !== false) {
    return {
      response: problem(400, "Ma'lumotlar noto'g'ri", QR_REQUIRED_MESSAGE, {
        errors: { Qr: [QR_REQUIRED_MESSAGE] },
      }),
    };
  }

  lastCheckinPhoto = photo ? { name: photo.name, type: photo.type, size: photo.size } : null;
  return { form: { lat, lng, accuracy, occurredAt, photo, qr } };
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
    const outOfPeriod = periodRejection();
    if (outOfPeriod) return outOfPeriod;
    if (mockToday.checkin.checkInAt) {
      return problem(409, 'Ziddiyat', 'Bugun allaqachon belgilangansiz.');
    }
    if (!mockToday.window.isOpen) {
      return problem(400, "Noto'g'ri amal", 'Bugungi belgilanish oynasi yopilgan.');
    }
    const qrInvalid = qrRejection(body.qr);
    if (qrInvalid) return qrInvalid;
    lastCheckinQr = body.qr;
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
    const outOfPeriod = periodRejection();
    if (outOfPeriod) return outOfPeriod;
    if (isFinished(mockToday.checkin)) {
      return problem(409, 'Ziddiyat', 'Bugun allaqachon ketganingiz belgilangan.');
    }
    if (!isCheckedIn(mockToday.checkin)) {
      return problem(409, 'Ziddiyat', 'Avval kelganingizni belgilang (KELDIM).');
    }
    const qrInvalid = qrRejection(body.qr);
    if (qrInvalid) return qrInvalid;
    lastCheckinQr = body.qr;
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
