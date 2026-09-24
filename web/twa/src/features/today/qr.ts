/**
 * Amaliyot joyi QR kodi (check-in/check-out 1-qadami). Payload formati: `AMLQR:1:{token}`
 * (token — 32 belgili hex). Klient faqat FORMATNI tekshiradi — token korxonaga tegishliligini server
 * tekshiradi (noto'g'ri → 409 "QR kod bu amaliyot joyiga tegishli emas.").
 */
export const CHECKIN_QR_PREFIX = 'AMLQR:1:';

/** Mock rejimidagi korxona QR payload'i ("Test QR" tugmasi; MSW mock shu tokenni qabul qiladi). */
export const MOCK_TEST_QR = `${CHECKIN_QR_PREFIX}9f2c4e1a7b3d5f60a8c2e4b6d8f01a3c`;

export const QR_MESSAGES = {
  scanPrompt: 'Amaliyot joyidagi QR kodni kameraga tuting',
  foreign: 'Bu amaliyot joyining QR kodi emas. Amaliyot joyidagi QR kodni qayta skanerlang.',
  cancelled: 'QR skanerlash bekor qilindi. Davom etish uchun QR kodni skanerlang.',
  outdated: 'QR skanerlash uchun Telegram ilovasini yangilang.',
  unavailable:
    'Bu brauzerda kamera mavjud emas. QR kodni skanerlash uchun ilovani Telegram orqali yoki zamonaviy brauzerda (HTTPS) oching.',
  failed: 'QR skanerni ochib bo‘lmadi. Qayta urinib ko‘ring.',
} as const;

/** Skanerlangan matn amaliyot joyi QR'imi (prefiks + bo'sh bo'lmagan token). Mos bo'lsa — tozalangan payload. */
export function parseCheckinQr(raw: string | null | undefined): string | null {
  const text = raw?.trim() ?? '';
  if (!text.startsWith(CHECKIN_QR_PREFIX)) return null;
  return text.length > CHECKIN_QR_PREFIX.length ? text : null;
}

/** Sozlama `checkinQrRequired` (TodayDto `checkin.qrRequired`): flag yo'q bo'lsa — talab qilinadi. */
export function isQrRequired(flag: boolean | null | undefined): boolean {
  return flag !== false;
}
