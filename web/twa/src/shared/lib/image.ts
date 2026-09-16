/**
 * Rasmni yuborishdan oldin siqish — canvas orqali (qo'shimcha kutubxonasiz, bundle o'smaydi).
 * Telefon kamerasi 3–8 MB JPEG beradi; backend chegarasi 5 MB, mobil internet sekin —
 * shuning uchun uzun tomoni `maxSide` ga keltirilib, JPEG `quality` bilan qayta kodlanadi.
 *
 * Muhit `createImageBitmap` ni qo'llab-quvvatlamasa (jsdom, juda eski WebView) —
 * ASL fayl qaytariladi; hajm/format tekshiruvi baribir yuqori qatlamda bajariladi.
 */

export interface CompressImageOptions {
  /** Uzun tomon (px). */
  maxSide?: number;
  /** JPEG sifati 0..1. */
  quality?: number;
  /** Natija fayl nomi (kengaytmasiz — `.jpg` qo'shiladi). */
  name?: string;
}

function canUseCanvas(): boolean {
  return (
    typeof document !== 'undefined' &&
    typeof createImageBitmap === 'function' &&
    typeof HTMLCanvasElement !== 'undefined' &&
    typeof HTMLCanvasElement.prototype.toBlob === 'function'
  );
}

function toBlob(canvas: HTMLCanvasElement, quality: number): Promise<Blob | null> {
  return new Promise((resolve) => canvas.toBlob(resolve, 'image/jpeg', quality));
}

/**
 * Rasmni maks. `maxSide` px va JPEG sifatiga keltiradi.
 * Natija asl fayldan katta chiqsa yoki siqib bo'lmasa — asl fayl qaytariladi.
 */
export async function compressImage(
  file: File,
  { maxSide = 1280, quality = 0.8, name = 'selfie' }: CompressImageOptions = {},
): Promise<File> {
  if (!canUseCanvas()) return file;

  let bitmap: ImageBitmap;
  try {
    bitmap = await createImageBitmap(file);
  } catch {
    // HEIC/HEIF ni ba'zi brauzerlar dekodlay olmaydi — asl fayl yuboriladi.
    return file;
  }

  try {
    const longest = Math.max(bitmap.width, bitmap.height);
    const scale = longest > maxSide ? maxSide / longest : 1;
    const width = Math.max(1, Math.round(bitmap.width * scale));
    const height = Math.max(1, Math.round(bitmap.height * scale));

    const canvas = document.createElement('canvas');
    canvas.width = width;
    canvas.height = height;
    const ctx = canvas.getContext('2d');
    if (!ctx) return file;
    ctx.drawImage(bitmap, 0, 0, width, height);

    const blob = await toBlob(canvas, quality);
    if (!blob || blob.size >= file.size) return file;
    return new File([blob], `${name}.jpg`, { type: 'image/jpeg', lastModified: Date.now() });
  } catch {
    return file;
  } finally {
    bitmap.close?.();
  }
}

/** `URL.createObjectURL` — muhitda bo'lmasa yoki xato bersa `null` (preview shunchaki ko'rsatilmaydi). */
export function createPreviewUrl(file: File): string | null {
  try {
    return URL.createObjectURL(file);
  } catch {
    return null;
  }
}

export function revokePreviewUrl(url: string | null | undefined): void {
  if (!url) return;
  try {
    URL.revokeObjectURL(url);
  } catch {
    /* muhim emas */
  }
}

/** 245_760 → "240 KB" (o'zbek yozuvi, vergul). */
export function formatBytes(bytes: number): string {
  if (bytes < 1024) return `${bytes} B`;
  if (bytes < 1024 * 1024) return `${Math.round(bytes / 1024)} KB`;
  return `${(bytes / (1024 * 1024)).toFixed(1).replace('.', ',')} MB`;
}
