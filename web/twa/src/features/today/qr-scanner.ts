import { telegramQrSupport } from '@/shared/auth/telegram';

/**
 * Qaysi QR skaner ishlatiladi:
 * - `telegram` — Telegram ichida, Bot API 6.4+ (`showScanQrPopup`);
 * - `outdated` — Telegram ichida, lekin mijoz eski (kamera bo'lsa — kamera skaneri muqobil);
 * - `camera` — oddiy brauzer: `getUserMedia` (orqa kamera) + `BarcodeDetector` / `jsqr`;
 * - `unavailable` — kamera API'si yo'q (eski brauzer yoki HTTPS emas).
 */
export type QrScannerKind = 'telegram' | 'outdated' | 'camera' | 'unavailable';

export function canUseCamera(): boolean {
  return (
    typeof navigator !== 'undefined' && typeof navigator.mediaDevices?.getUserMedia === 'function'
  );
}

export function qrScannerKind(): QrScannerKind {
  const tg = telegramQrSupport();
  if (tg === 'native') return 'telegram';
  if (tg === 'outdated') return 'outdated';
  return canUseCamera() ? 'camera' : 'unavailable';
}

export const CAMERA_MESSAGES = {
  denied:
    'Kameraga ruxsat berilmadi. Brauzer sozlamalarida kameraga ruxsat bering va qayta urinib ko‘ring.',
  notFound: 'Qurilmada kamera topilmadi. QR kodni kamerasi bor telefondan skanerlang.',
  busy: 'Kamera boshqa ilova tomonidan band. Uni yopib, qayta urinib ko‘ring.',
  unsupported:
    'Bu brauzerda kamera mavjud emas. Ilovani Telegram orqali yoki zamonaviy brauzerda (HTTPS) oching.',
  failed: 'Kamerani ishga tushirib bo‘lmadi. Qayta urinib ko‘ring.',
} as const;

/** `getUserMedia` xatosi → o'zbekcha tushunarli xabar. */
export function cameraErrorMessage(cause: unknown): string {
  const name = cause instanceof Error || cause instanceof DOMException ? cause.name : '';
  switch (name) {
    case 'NotAllowedError':
    case 'PermissionDeniedError':
    case 'SecurityError':
      return CAMERA_MESSAGES.denied;
    case 'NotFoundError':
    case 'DevicesNotFoundError':
    case 'OverconstrainedError':
      return CAMERA_MESSAGES.notFound;
    case 'NotReadableError':
    case 'TrackStartError':
    case 'AbortError':
      return CAMERA_MESSAGES.busy;
    default:
      return CAMERA_MESSAGES.failed;
  }
}

/** Orqa kamera oqimi. Kamera API'si yo'q bo'lsa — `NotSupportedError`. */
export function openBackCamera(): Promise<MediaStream> {
  if (!canUseCamera()) {
    return Promise.reject(new DOMException(CAMERA_MESSAGES.unsupported, 'NotSupportedError'));
  }
  return navigator.mediaDevices.getUserMedia({
    audio: false,
    video: { facingMode: { ideal: 'environment' } },
  });
}

export function stopStream(stream: MediaStream | null | undefined): void {
  stream?.getTracks().forEach((track) => track.stop());
}

/** Brauzer kamera skaneri natijasi. */
export type CameraScanOutcome =
  { kind: 'scanned'; text: string } | { kind: 'cancelled' } | { kind: 'error'; message: string };

/** Video kadridan QR matnini o'qiydi (topilmasa — `null`). */
export interface QrFrameDecoder {
  decode: (video: HTMLVideoElement) => Promise<string | null>;
}

interface BarcodeDetectorLike {
  detect: (source: CanvasImageSource) => Promise<Array<{ rawValue: string }>>;
}
interface BarcodeDetectorCtor {
  new (options: { formats: string[] }): BarcodeDetectorLike;
  getSupportedFormats?: () => Promise<string[]>;
}

/** Tahlil uchun kadr o'lchami chegarasi — sekin telefonlarda ham tez ishlaydi. */
const MAX_FRAME_SIDE = 640;

async function nativeDecoder(): Promise<QrFrameDecoder | null> {
  const Ctor = (globalThis as { BarcodeDetector?: BarcodeDetectorCtor }).BarcodeDetector;
  if (!Ctor) return null;
  try {
    const formats = (await Ctor.getSupportedFormats?.()) ?? ['qr_code'];
    if (!formats.includes('qr_code')) return null;
    const detector = new Ctor({ formats: ['qr_code'] });
    return {
      decode: async (video) => {
        const codes = await detector.detect(video);
        return codes[0]?.rawValue ?? null;
      },
    };
  } catch {
    return null;
  }
}

async function jsqrDecoder(): Promise<QrFrameDecoder> {
  // `jsqr` (~45 kB) faqat kerak bo'lganda yuklanadi — asosiy bundle kichik qoladi.
  const { default: jsQR } = await import('jsqr');
  const canvas = document.createElement('canvas');
  const ctx = canvas.getContext('2d', { willReadFrequently: true });
  return {
    decode: (video) => {
      const vw = video.videoWidth;
      const vh = video.videoHeight;
      if (!ctx || vw === 0 || vh === 0) return Promise.resolve(null);
      const scale = Math.min(1, MAX_FRAME_SIDE / Math.max(vw, vh));
      const w = Math.round(vw * scale);
      const h = Math.round(vh * scale);
      canvas.width = w;
      canvas.height = h;
      ctx.drawImage(video, 0, 0, w, h);
      const image = ctx.getImageData(0, 0, w, h);
      const code = jsQR(image.data, w, h, { inversionAttempts: 'dontInvert' });
      return Promise.resolve(code?.data ?? null);
    },
  };
}

/** `BarcodeDetector` (format `qr_code`) bo'lsa — u, aks holda `jsqr`. */
export async function createQrFrameDecoder(): Promise<QrFrameDecoder> {
  return (await nativeDecoder()) ?? jsqrDecoder();
}
