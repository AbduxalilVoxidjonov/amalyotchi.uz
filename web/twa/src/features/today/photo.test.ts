import { formatBytes } from '@/shared/lib/image';
import { PHOTO_MAX_BYTES, preparePhoto } from './photo';

/** jsdom'da `createImageBitmap` yo'q — `compressImage` asl faylni qaytaradi, tekshiruvlar esa ishlaydi. */
function file(type: string, bytes: number, name = 'selfie.jpg'): File {
  return new File([new Uint8Array(bytes)], name, { type });
}

describe('preparePhoto (check-in selfie qoidalari)', () => {
  it('ruxsat etilgan turdagi va chegaradagi rasm qabul qilinadi', async () => {
    await expect(preparePhoto(file('image/jpeg', 64))).resolves.toEqual({
      ok: true,
      file: expect.any(File),
    });
    // Aynan 5 MB — chegara ichida.
    await expect(preparePhoto(file('image/png', PHOTO_MAX_BYTES))).resolves.toMatchObject({
      ok: true,
    });
  });

  it('rasm bo‘lmagan fayl, chegaradan katta hajm va begona format rad etiladi', async () => {
    await expect(preparePhoto(file('application/pdf', 10, 'h.pdf'))).resolves.toMatchObject({
      ok: false,
      error: expect.stringContaining('Faqat rasm'),
    });
    await expect(preparePhoto(file('image/jpeg', PHOTO_MAX_BYTES + 1))).resolves.toMatchObject({
      ok: false,
      error: expect.stringContaining('5 MB'),
    });
    await expect(preparePhoto(file('image/gif', 10, 'a.gif'))).resolves.toMatchObject({
      ok: false,
      error: expect.stringContaining('format'),
    });
  });
});

describe('formatBytes', () => {
  it('o‘zbek yozuvida hajm', () => {
    expect(formatBytes(512)).toBe('512 B');
    expect(formatBytes(245_760)).toBe('240 KB');
    expect(formatBytes(1_572_864)).toBe('1,5 MB');
  });
});
