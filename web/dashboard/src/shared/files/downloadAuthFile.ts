import { useAuthStore } from '@/shared/auth/store';
import { env } from '@/shared/lib/env';

/** `filename*=UTF-8''...` (RFC 5987) yoki oddiy `filename="..."`. */
function fileNameFromDisposition(header: string | null): string | null {
  if (!header) return null;
  const encoded = /filename\*=UTF-8''([^;]+)/i.exec(header)?.[1];
  if (encoded) {
    try {
      return decodeURIComponent(encoded);
    } catch {
      return null;
    }
  }
  return /filename="?([^";]+)"?/i.exec(header)?.[1] ?? null;
}

/**
 * Bearer token talab qiladigan faylni yuklab oladi. Oddiy `<a href>` bunday manzilda 401 oladi
 * (`useAuthFile` bilan bir xil sabab), shuning uchun fayl `fetch` bilan olinib, blob havolasi orqali
 * saqlanadi. Nomi serverning `Content-Disposition` sarlavhasidan olinadi, bo'lmasa `fallbackName`.
 * Xato bo'lsa `Error` tashlaydi — chaqiruvchi xabarni o'zi ko'rsatadi.
 */
export async function downloadAuthFile(url: string, fallbackName: string): Promise<void> {
  const token = useAuthStore.getState().accessToken;
  const response = await fetch(`${env.apiUrl}${url}`, {
    headers: token ? { Authorization: `Bearer ${token}` } : {},
  });
  if (!response.ok) throw new Error(`HTTP ${response.status}`);

  const blob = await response.blob();
  const name = fileNameFromDisposition(response.headers.get('content-disposition')) ?? fallbackName;

  // jsdom va eski brauzerlarda blob havolasi yo'q — fayl olindi, saqlash bosqichi o'tkazib yuboriladi.
  if (typeof URL.createObjectURL !== 'function') return;

  const objectUrl = URL.createObjectURL(blob);
  const anchor = document.createElement('a');
  anchor.href = objectUrl;
  anchor.download = name;
  anchor.rel = 'noopener';
  document.body.appendChild(anchor);
  anchor.click();
  anchor.remove();
  URL.revokeObjectURL(objectUrl);
}
