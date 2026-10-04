import { ApiError } from '@/shared/api/client';
import { classifyCheckinError } from './checkin-errors';

function apiError(status: number, detail: string, errors?: Record<string, string[]>): ApiError {
  return ApiError.fromProblem(
    status,
    { title: 'x', status, detail, ...(errors ? { errors } : {}) },
    detail,
  );
}

describe('classifyCheckinError — server xatosidan keyin qaysi qadamga', () => {
  it.each([
    [apiError(409, 'QR kod bu amaliyot joyiga tegishli emas.'), 'qr'],
    [
      apiError(400, "Ma'lumotlar noto'g'ri", { Qr: ['Amaliyot joyidagi QR kodni skanerlang.'] }),
      'qr',
    ],
    [
      apiError(400, 'Check-in uchun rasm majburiy.', { Photo: ['Check-in uchun rasm majburiy.'] }),
      'photo',
    ],
    [apiError(409, 'Siz amaliyot joyida emassiz.'), 'location'],
    [apiError(409, 'Korxona radiusidan tashqaridasiz: 900 m / 150 m.'), 'location'],
    [
      apiError(400, "GPS aniqligi yetarli emas. Ochiq joyga chiqib qayta urinib ko'ring."),
      'location',
    ],
    [
      apiError(400, "Ma'lumotlar noto'g'ri", {
        Lat: ["Kenglik (lat) -90 va 90 oralig'ida bo'lishi kerak."],
      }),
      'location',
    ],
    [apiError(409, 'Bugun allaqachon belgilangansiz.'), 'stale'],
    [apiError(409, 'Avval kelganingizni belgilang.'), 'stale'],
    [apiError(400, 'Bugungi belgilanish oynasi yopilgan.'), 'stale'],
    [apiError(400, 'Bugun ish kuni emas.'), 'stale'],
    [apiError(400, 'Amaliyot davri tugagan: Kuzgi amaliyot 2026.'), 'stale'],
    [apiError(500, 'Server xatosi.'), 'retry'],
    [ApiError.network(new TypeError('fetch')), 'retry'],
    [new Error('boom'), 'retry'],
  ] as const)('%s → %s', (cause, step) => {
    expect(classifyCheckinError(cause).step).toBe(step);
  });

  it('xabar — server `detail`/maydon xatosi', () => {
    expect(classifyCheckinError(apiError(409, 'Siz amaliyot joyida emassiz.')).message).toBe(
      'Siz amaliyot joyida emassiz.',
    );
    expect(
      classifyCheckinError(apiError(400, 'x', { Photo: ['Rasm 5 MB dan oshmasligi kerak.'] }))
        .message,
    ).toBe('Rasm 5 MB dan oshmasligi kerak.');
  });
});

describe('classifyCheckinError — yuz sabablari (v3.27)', () => {
  const withReason = (status: number, detail: string, rejectReason: string) =>
    ApiError.fromProblem(status, { title: 'x', status, detail, rejectReason }, detail);

  it.each([
    [withReason(400, 'Etalon yo‘q.', 'faceNotEnrolled'), 'faceNotEnrolled'],
    [withReason(400, 'Yuz yo‘q.', 'faceNotDetected'), 'faceNotDetected'],
    [withReason(409, 'Mos emas.', 'faceMismatch'), 'faceMismatch'],
    [apiError(400, 'Yuz rasmingiz hali tasdiqlanmagan.'), 'faceNotEnrolled'],
    [apiError(400, 'Selfida yuz topilmadi.'), 'faceNotDetected'],
    [apiError(409, 'Selfidagi yuz tasdiqlangan rasmingizga mos kelmadi.'), 'faceMismatch'],
  ])('%s → face/%s', (error, reason) => {
    expect(classifyCheckinError(error)).toMatchObject({ step: 'face', faceReason: reason });
  });

  it('boshqa rejectReason (qrInvalid emas) yuz deb hisoblanmaydi', () => {
    expect(classifyCheckinError(withReason(400, 'Bugun ish kuni emas.', 'notWorkDay')).step).toBe(
      'stale',
    );
  });
});
