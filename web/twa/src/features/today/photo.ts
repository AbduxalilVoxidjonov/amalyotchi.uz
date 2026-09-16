/**
 * Check-in selfie: format/hajm qoidalari va tayyorlash (siqish) — kontrakt §1.3.
 * Backend `photo` maydoni: ≤5 MB, `image/jpeg,image/png,image/webp,image/heic,image/heif`;
 * xato → 400 ProblemDetails, `errors.Photo`.
 */
import { compressImage } from '@/shared/lib/image';

/** Backend qabul qiladigan turlar (`CheckInCommandValidator.AllowedContentTypes`). */
export const PHOTO_CONTENT_TYPES = [
  'image/jpeg',
  'image/png',
  'image/webp',
  'image/heic',
  'image/heif',
] as const;

export const PHOTO_MAX_BYTES = 5 * 1024 * 1024;

/**
 * `capture="user"` — old (selfie) kamera. Telegram WebView'da `getUserMedia` har doim ham
 * ishlamaydi, `<input type="file">` esa Android/iOS mijozlarida ishonchli ishlaydi.
 * `accept="image/*"` — galereyadan tanlash ham mumkin (rasm siqilgach JPEG bo'ladi).
 */
export const PHOTO_ACCEPT = 'image/*';

export const PHOTO_MESSAGES = {
  notImage: 'Faqat rasm yuborish mumkin. Kamerani ochib, o‘zingizni suratga oling.',
  badFormat:
    'Rasm formati qo‘llab-quvvatlanmaydi. JPEG, PNG, WEBP yoki HEIC formatidagi rasm oling.',
  tooLarge: `Rasm hajmi ${PHOTO_MAX_BYTES / (1024 * 1024)} MB dan oshmasligi kerak. Qaytadan, kichikroq sifatda suratga oling.`,
  failed: 'Rasmni qayta ishlab bo‘lmadi. Qaytadan suratga oling.',
  required: 'Check-in uchun rasm majburiy. Iltimos, selfie oling.',
  cameraCancelled: 'Rasm olinmadi. "Rasmga olish" tugmasi orqali qaytadan urinib ko‘ring.',
} as const;

export type PreparedPhoto = { ok: true; file: File } | { ok: false; error: string };

/**
 * Tanlangan faylni yuborishga tayyorlaydi: tur tekshiriladi → siqiladi (maks. 1280px, JPEG 0.8)
 * → hajm/format qayta tekshiriladi. Siqish muhitda mumkin bo'lmasa — asl fayl qoladi,
 * shuning uchun tekshiruv siqishdan KEYIN bajariladi.
 */
export async function preparePhoto(file: File): Promise<PreparedPhoto> {
  if (file.type && !file.type.startsWith('image/')) {
    return { ok: false, error: PHOTO_MESSAGES.notImage };
  }

  let prepared: File;
  try {
    prepared = await compressImage(file, { maxSide: 1280, quality: 0.8, name: 'selfie' });
  } catch {
    return { ok: false, error: PHOTO_MESSAGES.failed };
  }

  const type = prepared.type.toLowerCase();
  if (!(PHOTO_CONTENT_TYPES as readonly string[]).includes(type)) {
    return { ok: false, error: PHOTO_MESSAGES.badFormat };
  }
  if (prepared.size > PHOTO_MAX_BYTES) {
    return { ok: false, error: PHOTO_MESSAGES.tooLarge };
  }
  return { ok: true, file: prepared };
}
