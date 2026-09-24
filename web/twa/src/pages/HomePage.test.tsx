import { http, HttpResponse } from 'msw';
import { act, cleanup, fireEvent, screen, waitFor, within } from '@testing-library/react';
// `lastCheckinPhoto` mock ichida qayta tayinlanadi — namespace orqali o'qiladi.
import * as todayMocks from '@/features/today/mocks';
import {
  mockToday,
  setCheckinPhotoRequired,
  setDiaryPdfRequired,
  setPeriodGap,
} from '@/features/today/mocks';
import { mockPlace, setMockPlace } from '@/features/place/mocks';
import { server } from '@/mocks/server';
import { removeGeolocation, renderApp, stubGeolocation } from '@/test/render-app';
import { qrPopup } from '@/test/telegram-stub';

afterEach(() => removeGeolocation());

/** `mockToday` mock ichida qayta tayinlanadi — joriy qiymat namespace orqali. */
const mockTodayNow = () => todayMocks.mockToday;

/** Kamera bergan fayl (jsdom'da canvas yo'q — `compressImage` asl faylni qaytaradi). */
function photoFile(name = 'selfie.jpg', type = 'image/jpeg', bytes = 64): File {
  return new File([new Uint8Array(bytes)], name, { type });
}

/**
 * KELDIM/KETDIM → Telegram QR popup (1-qadam) → mock korxona QR'i skanerlanadi.
 * QR talab qilinadi (`checkinQrRequired` sukuti `true`).
 */
async function pressAndScan(name: 'KELDIM' | 'KETDIM') {
  fireEvent.click(await screen.findByRole('button', { name }));
  await waitFor(() => expect(qrPopup.isOpen).toBe(true));
  act(() => qrPopup.scan(todayMocks.MOCK_CHECKIN_QR));
  expect(await screen.findByText('QR tasdiqlandi')).toBeInTheDocument();
}

/** Yashirin `input[capture=user]` ga fayl berish — kamera rasm qaytargani bilan bir xil. */
function capturePhoto(file: File = photoFile()) {
  fireEvent.change(screen.getByLabelText('Selfie olish'), { target: { files: [file] } });
}

describe('HomePage (isTalaba) — check-in selfie', () => {
  it('KELDIM → selfie → preview → tasdiqlash → KETDIM (toggle, rasm bilan)', async () => {
    const geo = stubGeolocation();
    renderApp('/');

    expect(await screen.findByText('Belgilanish oynasi ochiq')).toBeInTheDocument();
    expect(screen.getByText('Bugun · 12.10.2026')).toBeInTheDocument();
    expect(screen.getByText('Kutilmoqda')).toBeInTheDocument();
    expect(screen.getByText('45 m / 150 m')).toBeInTheDocument();

    // 1-bosqich: tugma → joylashuv so'raladi va kamera ochiladi (panel ko'rinadi).
    await pressAndScan('KELDIM');
    expect(await screen.findByText('Selfie bilan tasdiqlang')).toBeInTheDocument();
    expect(screen.getByText('Rasm hali olinmagan')).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'KELDIM' })).not.toBeInTheDocument();
    expect(geo).toHaveBeenCalledTimes(1);

    // 2-bosqich: rasm olindi → preview (qayta olish / tasdiqlash).
    capturePhoto();
    expect(await screen.findByAltText('Olingan selfie')).toBeInTheDocument();
    expect(screen.getByText(/Rasm tayyor · /)).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Qayta olish' })).toBeEnabled();

    // 3-bosqich: yuborish — multipart, `photo` maydoni bilan.
    fireEvent.click(screen.getByRole('button', { name: 'Tasdiqlash va yuborish' }));
    expect(await screen.findByText('Belgilandingiz · 09:02')).toBeInTheDocument();
    expect(screen.getByText('Keldi')).toBeInTheDocument();
    expect(screen.getByText(/Korxonadan \d+ m masofada qayd etildi/)).toBeInTheDocument();
    // jsdom/undici multipart'da fayl NOMI saqlanmaydi ('blob') — tur va hajm tekshiriladi.
    expect(todayMocks.lastCheckinPhoto).toMatchObject({ type: 'image/jpeg', size: 64 });
    // Joylashuv `start` da olingan va qayta so'ralmagan.
    expect(geo).toHaveBeenCalledTimes(1);

    // KETDIM ham xuddi shu oqim bilan.
    await pressAndScan('KETDIM');
    expect(await screen.findByText('Ketishni selfie bilan tasdiqlang')).toBeInTheDocument();
    capturePhoto(photoFile('ketdim.jpg', 'image/jpeg', 128));
    fireEvent.click(await screen.findByRole('button', { name: 'Tasdiqlash va yuborish' }));
    expect(await screen.findByText('Kun yakunlandi · 09:02')).toBeInTheDocument();
    expect(screen.getByText('Yakunlandi')).toBeInTheDocument();
    expect(todayMocks.lastCheckinPhoto?.size).toBe(128);
    expect(screen.queryByRole('button', { name: /KELDIM|KETDIM/ })).not.toBeInTheDocument();
  });

  it('5 MB dan katta rasm rad etiladi — preview chiqmaydi, so’rov ketmaydi', async () => {
    stubGeolocation();
    renderApp('/');
    await pressAndScan('KELDIM');
    capturePhoto(photoFile('katta.jpg', 'image/jpeg', 5 * 1024 * 1024 + 1));

    expect(await screen.findByText(/Rasm hajmi 5 MB dan oshmasligi kerak/)).toBeInTheDocument();
    expect(screen.queryByAltText('Olingan selfie')).not.toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Rasmga olish' })).toBeEnabled();
    expect(todayMocks.lastCheckinPhoto).toBeNull();
    expect(screen.getByText('Kutilmoqda')).toBeInTheDocument();
  });

  it("rasm bo'lmagan fayl rad etiladi", async () => {
    stubGeolocation();
    renderApp('/');
    await pressAndScan('KELDIM');
    capturePhoto(photoFile('hujjat.pdf', 'application/pdf'));

    expect(await screen.findByText(/Faqat rasm yuborish mumkin/)).toBeInTheDocument();
    expect(screen.queryByAltText('Olingan selfie')).not.toBeInTheDocument();
  });

  it("qo'llab-quvvatlanmaydigan rasm formati rad etiladi (siqish mumkin bo'lmaganda)", async () => {
    stubGeolocation();
    renderApp('/');
    await pressAndScan('KELDIM');
    capturePhoto(photoFile('rasm.gif', 'image/gif'));

    expect(await screen.findByText(/Rasm formati qo‘llab-quvvatlanmaydi/)).toBeInTheDocument();
  });

  it("joylashuvga ruxsat yo'q → darhol xabar, belgilanish bo'lmaydi", async () => {
    stubGeolocation({ errorCode: 1 });
    renderApp('/');
    await pressAndScan('KELDIM');

    expect(await screen.findByText(/Joylashuvga ruxsat berilmadi/)).toBeInTheDocument();
    expect(screen.getByText('Kutilmoqda')).toBeInTheDocument();

    // Bekor qilish → asosiy tugma qaytadi.
    fireEvent.click(screen.getByRole('button', { name: 'Bekor qilish' }));
    expect(await screen.findByRole('button', { name: 'KELDIM' })).toBeInTheDocument();
    expect(screen.queryByText('Selfie bilan tasdiqlang')).not.toBeInTheDocument();
  });

  it('radius tashqarisi → server 409 xabari, rasm preview saqlanadi', async () => {
    stubGeolocation({ lat: 41.32, lng: 69.29 }); // ~1 km
    renderApp('/');
    await pressAndScan('KELDIM');
    capturePhoto();
    fireEvent.click(await screen.findByRole('button', { name: 'Tasdiqlash va yuborish' }));

    expect(await screen.findByText(/Korxona radiusidan tashqaridasiz/)).toBeInTheDocument();
    expect(screen.getByAltText('Olingan selfie')).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Tasdiqlash va yuborish' })).toBeEnabled();
  });

  it('tarmoq xatosi → o’zbekcha xabar, qayta urinish mumkin', async () => {
    stubGeolocation();
    server.use(http.post('/api/student/checkin', () => HttpResponse.error()));
    renderApp('/');
    await pressAndScan('KELDIM');
    capturePhoto();
    fireEvent.click(await screen.findByRole('button', { name: 'Tasdiqlash va yuborish' }));

    expect(await screen.findByText(/Server bilan aloqa yo.q/)).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Tasdiqlash va yuborish' })).toBeEnabled();
  });

  it('rasmsiz davom etish — sozlama majbur qilmasa qabul qilinadi', async () => {
    stubGeolocation();
    renderApp('/');
    await pressAndScan('KELDIM');
    fireEvent.click(await screen.findByRole('button', { name: 'Rasmsiz davom etish' }));

    expect(await screen.findByText('Belgilandingiz · 09:02')).toBeInTheDocument();
    expect(todayMocks.lastCheckinPhoto).toBeNull();
  });

  it("checkinPhotoRequired=true → 'Rasmsiz davom etish' yo'q, selfi bilan qabul qilinadi", async () => {
    stubGeolocation();
    setCheckinPhotoRequired(true);
    renderApp('/');
    await pressAndScan('KELDIM');

    expect(await screen.findByRole('button', { name: 'Rasmga olish' })).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Rasmsiz davom etish' })).not.toBeInTheDocument();

    capturePhoto();
    fireEvent.click(await screen.findByRole('button', { name: 'Tasdiqlash va yuborish' }));
    expect(await screen.findByText('Belgilandingiz · 09:02')).toBeInTheDocument();
    expect(todayMocks.lastCheckinPhoto).toMatchObject({ type: 'image/jpeg' });
  });

  it('eskirgan flag (photoRequired=false, server majbur) → 400 errors.Photo ko‘rsatiladi', async () => {
    stubGeolocation();
    setCheckinPhotoRequired(true);
    server.use(
      http.get('/api/student/today', () =>
        HttpResponse.json({
          ...mockTodayNow(),
          checkin: { ...mockTodayNow().checkin, photoRequired: false },
        }),
      ),
    );
    renderApp('/');
    await pressAndScan('KELDIM');
    fireEvent.click(await screen.findByRole('button', { name: 'Rasmsiz davom etish' }));

    expect(await screen.findByText('Check-in uchun rasm majburiy.')).toBeInTheDocument();
    expect(screen.getByText('Kutilmoqda')).toBeInTheDocument();

    // Rasm olib qayta yuborilsa — qabul qilinadi (QR saqlangan).
    capturePhoto();
    fireEvent.click(await screen.findByRole('button', { name: 'Tasdiqlash va yuborish' }));
    expect(await screen.findByText('Belgilandingiz · 09:02')).toBeInTheDocument();
  });

  it('09:15 dan keyin → Kech keldi', async () => {
    stubGeolocation({ at: '2026-10-12T04:31:00Z' }); // 09:31 Toshkent
    renderApp('/');
    await pressAndScan('KELDIM');
    capturePhoto();
    fireEvent.click(await screen.findByRole('button', { name: 'Tasdiqlash va yuborish' }));
    expect(await screen.findByText('Kech keldi')).toBeInTheDocument();
  });

  it("server holatlari: autoClosed → 'Kun avtomatik yakunlandi'; absent → tugma yo'q; place null → bo'sh holat", async () => {
    server.use(
      http.get('/api/student/today', () =>
        HttpResponse.json({
          ...mockToday,
          checkin: {
            ...mockToday.checkin,
            status: 'late',
            checkInAt: '2026-10-12T04:40:00Z',
            autoClosed: true,
            suspicious: true,
          },
          place: null,
        }),
      ),
    );
    renderApp('/');
    expect(await screen.findByText('Kun avtomatik yakunlandi')).toBeInTheDocument();
    expect(screen.getByText('Kech keldi')).toBeInTheDocument();
    expect(screen.getByText('Avtomatik yopildi')).toBeInTheDocument();
    expect(screen.getByRole('status')).toHaveTextContent('shubhali');
    expect(screen.getByText('Amaliyot joyi hali biriktirilmagan')).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: /KELDIM|KETDIM/ })).not.toBeInTheDocument();

    server.use(
      http.get('/api/student/today', () =>
        HttpResponse.json({
          ...mockToday,
          window: { ...mockToday.window, isOpen: false },
          checkin: {
            ...mockToday.checkin,
            status: 'absent',
            note: 'Bugungi belgilanish oynasi yopilgan.',
          },
        }),
      ),
    );
    cleanup();
    renderApp('/');
    expect(await screen.findByText('Bugun belgilanmadingiz')).toBeInTheDocument();
    expect(screen.getByText('Bugungi belgilanish oynasi yopilgan.')).toBeInTheDocument();
    expect(screen.getByText('Kelmadi')).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: /KELDIM|KETDIM/ })).not.toBeInTheDocument();
  });

  it('kundalik: hisoblagich va korxona qisqachasi', async () => {
    renderApp('/');
    expect(await screen.findByText('Bugungi kundalik')).toBeInTheDocument();
    expect(screen.getByText('0 / 150 belgi')).toBeInTheDocument();
    const ta = screen.getByPlaceholderText('Bugun bajarilgan ishlar — kamida 150 belgi');
    await act(() => {
      fireEvent.change(ta, { target: { value: 'Salom dunyo' } });
    });
    expect(screen.getByText('11 / 150 belgi')).toBeInTheDocument();
    // Pastda portfolio ham bor (o'xshash statlar) — korxona bo'limi ichida tekshiriladi.
    const place = within(screen.getByRole('region', { name: 'Korxonam' }));
    // Telegram rejimi: ichki havola `href`siz (Telegram-Android `<a href>` ni tashqi havola deb ushlaydi).
    expect(place.getByRole('link', { name: 'Tech Solutions MChJ' })).not.toHaveAttribute('href');
    expect(place.getByText('94%')).toBeInTheDocument();
    expect(place.getByText('4,2')).toBeInTheDocument();
    await waitFor(() => expect(screen.getByRole('button', { name: 'KELDIM' })).toBeEnabled());
  });

  it("kundalik: diaryPdfRequired → eslatma, PDF'siz yuborilmaydi", async () => {
    setDiaryPdfRequired(true);
    renderApp('/');
    expect(await screen.findByText('PDF hisobot majburiy')).toBeInTheDocument();
    fireEvent.change(screen.getByPlaceholderText('Bugun bajarilgan ishlar — kamida 150 belgi'), {
      target: { value: 'x'.repeat(160) },
    });
    fireEvent.click(screen.getByRole('button', { name: 'Yuborish' }));
    expect(await screen.findByText('Hisobotga PDF fayl biriktirilishi shart.')).toBeInTheDocument();
    expect(screen.queryByText('Kundalik yuborildi.')).not.toBeInTheDocument();
    expect(mockToday.diary.submittedToday).toBe(false);
  });
});

describe("HomePage — ikki davr oralig'i (v3.5 §4.6)", () => {
  const UPCOMING_NOTE =
    'Amaliyot davri hali boshlanmagan: Bahorgi amaliyot 2027, 01.02.2027 dan boshlanadi.';

  it('kelgusi davr: kartochka (nom, sana, qolgan kun) + ariza tugmasi → forma bahorgi davr uchun', async () => {
    setPeriodGap('upcoming');
    renderApp('/');

    expect(
      await screen.findByRole('heading', { name: 'Bahorgi amaliyot 2027' }),
    ).toBeInTheDocument();
    expect(screen.getByText('01.02.2027 dan boshlanadi')).toBeInTheDocument();
    expect(screen.getByText('43 kun qoldi')).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: /KELDIM|KETDIM/ })).not.toBeInTheDocument();
    // Tanaffusda kundalik yozilmaydi — forma ko'rsatilmaydi.
    expect(screen.queryByText('Bugungi kundalik')).not.toBeInTheDocument();

    // Bahorgi davrga ariza yo'q (GET place → 404) → "Amaliyot joyini yuborish".
    const link = await screen.findByRole('link', { name: 'Amaliyot joyini yuborish' });
    expect(link.tagName).toBe('BUTTON');
    expect(link).not.toHaveAttribute('href');
    fireEvent.click(link);

    expect(await screen.findByText('Amaliyot joyini tanlash')).toBeInTheDocument();
    expect(screen.getByText('Bahorgi amaliyot 2027 uchun')).toBeInTheDocument();
    fireEvent.change(screen.getByLabelText('Korxona STIR raqami'), {
      target: { value: '305881204' },
    });
    fireEvent.click(screen.getByRole('button', { name: 'Qidirish' }));
    fireEvent.click(await screen.findByRole('button', { name: 'Tasdiqlash va yuborish' }));
    // Ariza kelgusi (bahorgi) davrga tushadi.
    expect(await screen.findByText('01.02–15.03.2027')).toBeInTheDocument();
    expect(screen.getByText('Tekshiruvda')).toBeInTheDocument();
  });

  it("kelgusi davr: ariza allaqachon yuborilgan → tugma yo'q, holat ko'rinadi", async () => {
    setPeriodGap('upcoming');
    setMockPlace({ ...mockPlace, status: 'submitted', contract: null });
    renderApp('/');

    expect(await screen.findByText('Tekshiruvda')).toBeInTheDocument();
    expect(screen.getByRole('link', { name: 'Tech Solutions MChJ' })).not.toHaveAttribute('href');
    expect(
      screen.queryByRole('link', { name: 'Amaliyot joyini yuborish' }),
    ).not.toBeInTheDocument();
  });

  it('faqat tugagan davr: "Amaliyot davri tugagan", portfolio shu ekranda ostida', async () => {
    setPeriodGap('ended');
    renderApp('/');

    expect(
      await screen.findByRole('heading', { name: 'Amaliyot davri tugagan: Kuzgi amaliyot 2026' }),
    ).toBeInTheDocument();
    expect(screen.getByText(/quyidagi portfolioda saqlanadi/)).toBeInTheDocument();
    expect(screen.getByRole('heading', { name: 'Portfolio', level: 2 })).toBeInTheDocument();
    expect(screen.queryByRole('link', { name: /Portfolio/ })).not.toBeInTheDocument();
    expect(screen.queryByRole('button', { name: /KELDIM|KETDIM/ })).not.toBeInTheDocument();
    expect(screen.queryByText(/kun qoldi/)).not.toBeInTheDocument();
  });

  it("davr yo'q (period null) → avvalgidek CheckinCard, server note bilan", async () => {
    server.use(
      http.get('/api/student/today', () =>
        HttpResponse.json({
          ...mockToday,
          period: null,
          place: null,
          window: { ...mockToday.window, isOpen: false },
          checkin: { ...mockToday.checkin, note: "Faol amaliyot davri yo'q." },
        }),
      ),
    );
    renderApp('/');
    expect(await screen.findByText("Faol amaliyot davri yo'q.")).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'KELDIM' })).toBeDisabled();
  });

  it('eskirgan ekran: check-in 400 (davr boshlanmagan) matni ko‘rinadi', async () => {
    stubGeolocation();
    renderApp('/');
    await pressAndScan('KELDIM');
    // Ekran ochilgandan keyin server tanaffusga o'tdi — check-in 400 `detail` = today note.
    setPeriodGap('upcoming');
    fireEvent.click(await screen.findByRole('button', { name: 'Rasmsiz davom etish' }));

    expect(await screen.findByRole('alert')).toHaveTextContent(UPCOMING_NOTE);
    expect(todayMocks.lastCheckinPhoto).toBeNull();
  });
});
