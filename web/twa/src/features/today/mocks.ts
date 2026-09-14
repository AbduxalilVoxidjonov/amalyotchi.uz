import { http, HttpResponse, type HttpHandler } from 'msw';
import { STUDENT_ENDPOINTS } from '@/shared/api/endpoints';
import { problem, requireBearer } from '@/mocks/problem';
import { isCheckedIn, isFinished, type CheckinRequest, type TodayDto } from './types';

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

export function resetTodayMocks() {
  mockToday = initialToday();
}

/** Kundalik yuborilganda bosh ekran hisoblagichini yangilash (diary mock chaqiradi). */
export function markDiarySubmitted() {
  mockToday = {
    ...mockToday,
    place: mockToday.place && { ...mockToday.place, reports: mockToday.place.reports + 1 },
    diary: { ...mockToday.diary, submittedToday: true },
  };
}

function isValidPoint(body: Partial<CheckinRequest>): body is CheckinRequest {
  return (
    typeof body.lat === 'number' &&
    typeof body.lng === 'number' &&
    typeof body.accuracy === 'number' &&
    typeof body.occurredAt === 'string'
  );
}

/**
 * Check-in mock: haqiqiy masofa hisoblanadi (haversine); radius tashqarisi → 409 ProblemDetails
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
    const body = (await request.json().catch(() => ({}))) as Partial<CheckinRequest>;
    if (!isValidPoint(body)) {
      return problem(400, "Ma'lumotlar noto'g'ri", "Kiritilgan ma'lumotlarda xatolik bor.", {
        errors: { Lat: ["Kenglik (lat) -90 va 90 oralig'ida bo'lishi kerak."] },
      });
    }
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
    const body = (await request.json().catch(() => ({}))) as Partial<CheckinRequest>;
    if (!isValidPoint(body)) {
      return problem(400, "Ma'lumotlar noto'g'ri", "Kiritilgan ma'lumotlarda xatolik bor.");
    }
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
