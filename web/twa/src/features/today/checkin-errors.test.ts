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
