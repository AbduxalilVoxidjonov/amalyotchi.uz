import QRCode from 'qrcode';

/** Skanerlash ishonchliligi uchun o'rta darajadagi xatoni tuzatish (M ≈ 15%). */
const QR_OPTIONS = { errorCorrectionLevel: 'M', margin: 2 } as const;

/** Ekrandagi rasm — SVG (canvas talab qilmaydi, har qanday o'lchamda tiniq). */
export async function qrSvgDataUrl(payload: string): Promise<string> {
  const svg = await QRCode.toString(payload, { ...QR_OPTIONS, type: 'svg' });
  return `data:image/svg+xml;charset=utf-8,${encodeURIComponent(svg)}`;
}

/** Yuklab olish/chop etish uchun PNG (canvas orqali), standart 1024px. */
export function qrPngDataUrl(payload: string, width = 1024): Promise<string> {
  return QRCode.toDataURL(payload, { ...QR_OPTIONS, width, type: 'image/png' });
}

/** "Tech Solutions MChJ" → "tech-solutions-mchj" (fayl nomi uchun). */
export function fileSlug(name: string): string {
  const slug = name
    .toLowerCase()
    .replace(/[ʻʼ'`‘’]/g, '')
    .replace(/[^a-z0-9]+/g, '-')
    .replace(/^-+|-+$/g, '');
  return slug || 'korxona';
}

export function downloadDataUrl(dataUrl: string, fileName: string) {
  const a = document.createElement('a');
  a.href = dataUrl;
  a.download = fileName;
  a.rel = 'noopener';
  document.body.appendChild(a);
  a.click();
  a.remove();
}

function escapeHtml(value: string): string {
  return value
    .replace(/&/g, '&amp;')
    .replace(/</g, '&lt;')
    .replace(/>/g, '&gt;')
    .replace(/"/g, '&quot;')
    .replace(/'/g, '&#39;');
}

export const PRINT_HINT = 'Amaliyotchi: kelish/ketishda skanerlang';

/**
 * Chop etish varag'i: faqat QR + korxona nomi + qisqa ko'rsatma. Asosiy sahifa (sidebar, jadval)
 * chop etilmasligi uchun yashirin `iframe` ichida alohida hujjat yasaladi va o'shaning `print()`i chaqiriladi.
 */
export function buildPrintHtml(companyName: string, pngDataUrl: string): string {
  const name = escapeHtml(companyName);
  return `<!doctype html>
<html lang="uz"><head><meta charset="utf-8"><title>${name} — check-in QR</title>
<style>
  @page { size: A4 portrait; margin: 16mm; }
  html, body { margin: 0; height: 100%; }
  body { display: flex; flex-direction: column; align-items: center; justify-content: center;
    gap: 8mm; font-family: system-ui, -apple-system, 'Segoe UI', Roboto, sans-serif; color: #16181d; text-align: center; }
  h1 { margin: 0; font-size: 26pt; font-weight: 700; }
  img { width: 130mm; height: 130mm; image-rendering: pixelated; }
  p { margin: 0; font-size: 16pt; }
</style></head>
<body>
  <h1>${name}</h1>
  <img src="${pngDataUrl}" alt="${name} check-in QR kodi">
  <p>${escapeHtml(PRINT_HINT)}</p>
</body></html>`;
}

/** Yashirin iframe'da chop etish oynasini ochadi; yopilgach iframe olib tashlanadi. */
export function printHtml(html: string) {
  const frame = document.createElement('iframe');
  frame.setAttribute('aria-hidden', 'true');
  frame.tabIndex = -1;
  Object.assign(frame.style, {
    position: 'fixed',
    right: '0',
    bottom: '0',
    width: '0',
    height: '0',
    border: '0',
    visibility: 'hidden',
  });
  const cleanup = () => window.setTimeout(() => frame.remove(), 500);
  frame.onload = () => {
    const win = frame.contentWindow;
    if (!win) return cleanup();
    win.addEventListener('afterprint', cleanup, { once: true });
    win.focus();
    win.print();
    // `afterprint` bermaydigan brauzerlar uchun zaxira.
    window.setTimeout(() => frame.remove(), 60_000);
  };
  frame.srcdoc = html;
  document.body.appendChild(frame);
}
