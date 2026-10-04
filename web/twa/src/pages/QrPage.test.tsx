import { http, HttpResponse } from 'msw';
import { act, fireEvent, screen, waitFor, within } from '@testing-library/react';
// `lastCheckin*`/`mockToday` mock ichida qayta tayinlanadi — namespace orqali o'qiladi.
import * as todayMocks from '@/features/today/mocks';
import * as faceMocks from '@/features/face/mocks';
import { setCheckinPhotoRequired, setPeriodGap } from '@/features/today/mocks';
import { setPeriodDaysVariant } from '@/features/period-days/mocks';
import { problem } from '@/mocks/problem';
import { server } from '@/mocks/server';
import { removeGeolocation, renderApp, stubGeolocation } from '@/test/render-app';
import { qrPopup } from '@/test/telegram-stub';

afterEach(() => removeGeolocation());

const mockTodayNow = () => todayMocks.mockToday;

/** Kamera bergan fayl (jsdom'da canvas yo'q — `compressImage` asl faylni qaytaradi). */
function photoFile(name = 'selfie.jpg', type = 'image/jpeg', bytes = 64): File {
  return new File([new Uint8Array(bytes)], name, { type });
}

/** Yashirin `input[capture=user]` ga fayl berish — old kamera rasm qaytargani bilan bir xil. */
function capturePhoto(file: File = photoFile()) {
  fireEvent.change(screen.getByLabelText('Selfie olish'), { target: { files: [file] } });
}

const steps = () => within(screen.getByRole('list', { name: 'Belgilanish qadamlari' }));
const stepState = (label: string) => steps().getByText(label).closest('li');

/** "Kelganini/Ketganini belgilash" → Telegram QR popup (1-qadam) → mock korxona QR'i. */
async function startAndScan(name: 'Kelganini belgilash' | 'Ketganini belgilash') {
  fireEvent.click(await screen.findByRole('button', { name }));
  await waitFor(() => expect(qrPopup.isOpen).toBe(true));
  act(() => qrPopup.scan(todayMocks.MOCK_CHECKIN_QR));
  expect(await screen.findByText('QR tasdiqlandi')).toBeInTheDocument();
}

async function selfieAndSend(file?: File) {
  capturePhoto(file);
  fireEvent.click(await screen.findByRole('button', { name: 'Tasdiqlash va yuborish' }));
}

describe('QR sahifasi — kelganini belgilash (QR → selfi → joylashuv)', () => {
  it("qadamlar tartibi: QR → Selfi → Joylashuv; muvaffaqiyat ekrani va 'Bosh ekranga'", async () => {
    const geo = stubGeolocation();
    const router = renderApp('/qr');

    expect(
      await screen.findByRole('heading', { name: 'Kelganini belgilash', level: 2 }),
    ).toBeInTheDocument();
    fireEvent.click(screen.getByRole('button', { name: 'Kelganini belgilash' }));
    await waitFor(() => expect(qrPopup.isOpen).toBe(true));

    // 1-qadam: QR. Qadamlar ro'yxati aynan shu tartibda.
    expect(
      steps()
        .getAllByRole('listitem')
        .map((li) => li.textContent),
    ).toEqual([
      expect.stringContaining('QR'),
      expect.stringContaining('Selfi'),
      expect.stringContaining('Joylashuv'),
    ]);
    expect(stepState('QR')).toHaveAttribute('aria-current', 'step');
    expect(stepState('Selfi')).toHaveAttribute('data-state', 'todo');
    expect(stepState('Joylashuv')).toHaveAttribute('data-state', 'todo');
    expect(geo).not.toHaveBeenCalled();

    // 2-qadam: selfi (oval yo'naltiruvchi). Joylashuv fonda so'raladi, lekin qadam hali "navbatda".
    act(() => qrPopup.scan(todayMocks.MOCK_CHECKIN_QR));
    expect(await screen.findByText('Selfie bilan tasdiqlang')).toBeInTheDocument();
    expect(screen.getByText('Yuzingiz oval ichida, yorug‘ joyda bo‘lsin')).toBeInTheDocument();
    expect(stepState('QR')).toHaveAttribute('data-state', 'done');
    expect(stepState('Selfi')).toHaveAttribute('aria-current', 'step');
    expect(stepState('Joylashuv')).toHaveAttribute('data-state', 'todo');
    expect(geo).toHaveBeenCalledTimes(1);
    expect(screen.getByLabelText('Selfie olish')).toHaveAttribute('capture', 'user');

    capturePhoto();
    expect(await screen.findByAltText('Olingan selfie')).toBeInTheDocument();
    expect(stepState('Selfi')).toHaveAttribute('data-state', 'done');

    // 3-qadam: joylashuv → yuborish.
    fireEvent.click(screen.getByRole('button', { name: 'Tasdiqlash va yuborish' }));
    expect(
      await screen.findByRole('heading', { name: 'Kelganingiz belgilandi · 09:02' }),
    ).toBeInTheDocument();
    expect(screen.getByRole('status')).toHaveTextContent('Kelganingiz belgilandi');
    expect(screen.getByText('Keldi')).toBeInTheDocument();
    expect(todayMocks.lastCheckinQr).toBe(todayMocks.MOCK_CHECKIN_QR);
    expect(todayMocks.lastCheckinPhoto).toMatchObject({ type: 'image/jpeg', size: 64 });
    // Fondagi joylashuv qayta ishlatildi.
    expect(geo).toHaveBeenCalledTimes(1);

    // Bosh ekranga — bugungi qator "Keldi" (period-days invalidate), qisqa holat.
    const home = screen.getByRole('link', { name: 'Bosh ekranga' });
    expect(home).not.toHaveAttribute('href');
    fireEvent.click(home);
    expect(
      await screen.findByRole('heading', { name: 'Bosh ekran', level: 1 }),
    ).toBeInTheDocument();
    expect(router.state.location.pathname).toBe('/');
    const todayRow = await screen.findByRole('button', { name: /^12\.10 · Dushanba/ });
    expect(await within(todayRow).findByText('Keldi')).toBeInTheDocument();
    expect(await screen.findByText('Belgilandingiz · 09:02')).toBeInTheDocument();
  });

  it('09:15 dan keyin → "Kech keldi"', async () => {
    stubGeolocation({ at: '2026-10-12T04:31:00Z' }); // 09:31 Toshkent
    renderApp('/qr');
    await startAndScan('Kelganini belgilash');
    await selfieAndSend();
    expect(
      await screen.findByRole('heading', { name: 'Kelganingiz belgilandi · 09:31' }),
    ).toBeInTheDocument();
    expect(screen.getByText('Kech keldi')).toBeInTheDocument();
  });

  it('5 MB dan katta rasm rad etiladi — preview chiqmaydi, so‘rov ketmaydi', async () => {
    stubGeolocation();
    renderApp('/qr');
    await startAndScan('Kelganini belgilash');
    capturePhoto(photoFile('katta.jpg', 'image/jpeg', 5 * 1024 * 1024 + 1));

    expect(await screen.findByText(/Rasm hajmi 5 MB dan oshmasligi kerak/)).toBeInTheDocument();
    expect(screen.queryByAltText('Olingan selfie')).not.toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Rasmga olish' })).toBeEnabled();
    expect(todayMocks.lastCheckinPhoto).toBeNull();
  });

  it("rasm bo'lmagan fayl va qo'llab-quvvatlanmaydigan format rad etiladi", async () => {
    stubGeolocation();
    renderApp('/qr');
    await startAndScan('Kelganini belgilash');
    capturePhoto(photoFile('hujjat.pdf', 'application/pdf'));
    expect(await screen.findByText(/Faqat rasm yuborish mumkin/)).toBeInTheDocument();
    capturePhoto(photoFile('rasm.gif', 'image/gif'));
    expect(await screen.findByText(/Rasm formati qo‘llab-quvvatlanmaydi/)).toBeInTheDocument();
    expect(screen.queryByAltText('Olingan selfie')).not.toBeInTheDocument();
  });

  it('selfi majburiy emas → "Rasmsiz davom etish" (joylashuv qadami baribir)', async () => {
    stubGeolocation();
    renderApp('/qr');
    await startAndScan('Kelganini belgilash');
    fireEvent.click(await screen.findByRole('button', { name: 'Rasmsiz davom etish' }));

    expect(
      await screen.findByRole('heading', { name: 'Kelganingiz belgilandi · 09:02' }),
    ).toBeInTheDocument();
    expect(todayMocks.lastCheckinPhoto).toBeNull();
  });

  it("checkinPhotoRequired=true → 'Rasmsiz davom etish' yo'q, selfi bilan qabul qilinadi", async () => {
    stubGeolocation();
    setCheckinPhotoRequired(true);
    renderApp('/qr');
    await startAndScan('Kelganini belgilash');

    expect(await screen.findByRole('button', { name: 'Rasmga olish' })).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Rasmsiz davom etish' })).not.toBeInTheDocument();
    await selfieAndSend();
    expect(
      await screen.findByRole('heading', { name: 'Kelganingiz belgilandi · 09:02' }),
    ).toBeInTheDocument();
    expect(todayMocks.lastCheckinPhoto).toMatchObject({ type: 'image/jpeg' });
  });

  it('eskirgan flag (photoRequired=false, server majbur) → 400 errors.Photo → selfi qadamiga', async () => {
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
    renderApp('/qr');
    await startAndScan('Kelganini belgilash');
    fireEvent.click(await screen.findByRole('button', { name: 'Rasmsiz davom etish' }));

    expect(await screen.findByText('Check-in uchun rasm majburiy.')).toBeInTheDocument();
    expect(stepState('Selfi')).toHaveAttribute('aria-current', 'step');
    // QR saqlangan — rasm olib qayta yuborilsa qabul qilinadi.
    expect(screen.getByText('QR tasdiqlandi')).toBeInTheDocument();
    await selfieAndSend();
    expect(
      await screen.findByRole('heading', { name: 'Kelganingiz belgilandi · 09:02' }),
    ).toBeInTheDocument();
  });
});

describe('QR sahifasi — joylashuv (3-qadam) xatolari', () => {
  it("joylashuvga ruxsat yo'q → aniq xabar va 'Qayta urinish'; ruxsat berilgach yuboriladi", async () => {
    stubGeolocation({ errorCode: 1 });
    renderApp('/qr');
    await startAndScan('Kelganini belgilash');
    // Selfi paytida fondagi xato ko'rsatilmaydi — 3-qadamda.
    expect(screen.queryByText(/Joylashuvga ruxsat berilmadi/)).not.toBeInTheDocument();
    await selfieAndSend();

    expect(await screen.findByText(/Joylashuvga ruxsat berilmadi/)).toBeInTheDocument();
    expect(stepState('Joylashuv')).toHaveAttribute('data-state', 'error');
    expect(stepState('Selfi')).toHaveAttribute('data-state', 'done');
    expect(todayMocks.lastCheckinQr).toBeNull();

    const geo = stubGeolocation();
    fireEvent.click(screen.getByRole('button', { name: 'Qayta urinish' }));
    expect(
      await screen.findByRole('heading', { name: 'Kelganingiz belgilandi · 09:02' }),
    ).toBeInTheDocument();
    expect(geo).toHaveBeenCalledTimes(1);
    expect(todayMocks.lastCheckinPhoto).toMatchObject({ type: 'image/jpeg' });
  });

  it('radius tashqarisi → 409 → joylashuv qadamida xabar; qayta urinishda yangi joylashuv', async () => {
    stubGeolocation({ lat: 41.32, lng: 69.29 }); // ~1 km
    renderApp('/qr');
    await startAndScan('Kelganini belgilash');
    await selfieAndSend();

    expect(await screen.findByText(/Korxona radiusidan tashqaridasiz/)).toBeInTheDocument();
    expect(screen.getByRole('heading', { name: 'Joylashuv tasdiqlanmadi' })).toBeInTheDocument();
    expect(stepState('Joylashuv')).toHaveAttribute('data-state', 'error');
    expect(stepState('QR')).toHaveAttribute('data-state', 'done');

    const geo = stubGeolocation(); // korxona hududiga kirdi
    fireEvent.click(screen.getByRole('button', { name: 'Qayta urinish' }));
    expect(
      await screen.findByRole('heading', { name: 'Kelganingiz belgilandi · 09:02' }),
    ).toBeInTheDocument();
    expect(geo).toHaveBeenCalledTimes(1);
  });

  it('GPS aniqligi yetarli emas (400) → joylashuv qadamida', async () => {
    stubGeolocation({ accuracy: 250 });
    server.use(
      http.post('/api/student/checkin', () =>
        problem(
          400,
          "Noto'g'ri amal",
          "GPS aniqligi yetarli emas. Ochiq joyga chiqib qayta urinib ko'ring.",
        ),
      ),
    );
    renderApp('/qr');
    await startAndScan('Kelganini belgilash');
    await selfieAndSend();

    expect(await screen.findByText(/GPS aniqligi yetarli emas/)).toBeInTheDocument();
    expect(stepState('Joylashuv')).toHaveAttribute('data-state', 'error');
    expect(screen.getByRole('button', { name: 'Qayta urinish' })).toBeEnabled();
  });

  it('tarmoq xatosi → o‘zbekcha xabar, "Qayta urinish" bilan yuboriladi', async () => {
    stubGeolocation();
    server.use(http.post('/api/student/checkin', () => HttpResponse.error(), { once: true }));
    renderApp('/qr');
    await startAndScan('Kelganini belgilash');
    await selfieAndSend();

    expect(await screen.findByText(/Server bilan aloqa yo.q/)).toBeInTheDocument();
    fireEvent.click(screen.getByRole('button', { name: 'Qayta urinish' }));
    expect(
      await screen.findByRole('heading', { name: 'Kelganingiz belgilandi · 09:02' }),
    ).toBeInTheDocument();
  });
});

describe('QR sahifasi — server xatosidan keyin qaysi qadamga qaytiladi', () => {
  it('409 (QR boshqa joyniki) → QR qadamiga; selfi saqlanadi, qayta skanerlab yuboriladi', async () => {
    stubGeolocation();
    renderApp('/qr');
    fireEvent.click(await screen.findByRole('button', { name: 'Kelganini belgilash' }));
    await waitFor(() => expect(qrPopup.isOpen).toBe(true));
    act(() => qrPopup.scan('AMLQR:1:00000000000000000000000000000000'));
    await selfieAndSend();

    expect(await screen.findByRole('alert')).toHaveTextContent(
      'QR kod bu amaliyot joyiga tegishli emas.',
    );
    expect(screen.getByText('Amaliyot joyidagi QR kodni skanerlang')).toBeInTheDocument();
    expect(stepState('QR')).toHaveAttribute('aria-current', 'step');

    fireEvent.click(screen.getByRole('button', { name: 'QR kodni skanerlash' }));
    await waitFor(() => expect(qrPopup.isOpen).toBe(true));
    act(() => qrPopup.scan(todayMocks.MOCK_CHECKIN_QR));
    // Selfi avval olingan — to'g'ridan-to'g'ri preview'ga qaytiladi.
    expect(await screen.findByAltText('Olingan selfie')).toBeInTheDocument();
    fireEvent.click(screen.getByRole('button', { name: 'Tasdiqlash va yuborish' }));
    expect(
      await screen.findByRole('heading', { name: 'Kelganingiz belgilandi · 09:02' }),
    ).toBeInTheDocument();
  });

  it('409 (allaqachon belgilangan) → ekran yangilanadi: ketishni belgilash holati + xabar', async () => {
    stubGeolocation();
    server.use(
      http.post('/api/student/checkin', () => {
        todayMocks.mockToday.checkin = {
          ...todayMocks.mockToday.checkin,
          status: 'present',
          checkInAt: '2026-10-12T03:58:00Z',
        };
        return problem(409, 'Ziddiyat', 'Bugun allaqachon belgilangansiz.');
      }),
    );
    renderApp('/qr');
    await startAndScan('Kelganini belgilash');
    await selfieAndSend();

    expect(await screen.findByRole('button', { name: 'Ketganini belgilash' })).toBeInTheDocument();
    expect(screen.getByRole('alert')).toHaveTextContent('Bugun allaqachon belgilangansiz.');
    expect(screen.queryByRole('list', { name: 'Belgilanish qadamlari' })).not.toBeInTheDocument();
  });

  it('400 (oyna yopildi) → bo‘sh holat va xabar', async () => {
    stubGeolocation();
    server.use(
      http.post('/api/student/checkin', () => {
        todayMocks.mockToday.window = { ...todayMocks.mockToday.window, isOpen: false };
        return problem(400, "Noto'g'ri amal", 'Bugungi belgilanish oynasi yopilgan.');
      }),
    );
    renderApp('/qr');
    await startAndScan('Kelganini belgilash');
    await selfieAndSend();

    expect(await screen.findByText('Belgilanish oynasi yopiq')).toBeInTheDocument();
    expect(screen.getByRole('alert')).toHaveTextContent('Bugungi belgilanish oynasi yopilgan.');
    expect(screen.queryByRole('button', { name: 'Kelganini belgilash' })).not.toBeInTheDocument();
  });
});

describe('QR sahifasi — eskirgan ekran', () => {
  it('oqim davomida davr tugadi (tanaffus) → 400 → davr holati va server matni', async () => {
    stubGeolocation();
    renderApp('/qr');
    await startAndScan('Kelganini belgilash');
    // Ekran ochilgandan keyin server tanaffusga o'tdi — check-in 400 `detail` = today note.
    setPeriodGap('upcoming');
    fireEvent.click(await screen.findByRole('button', { name: 'Rasmsiz davom etish' }));

    expect(await screen.findByRole('alert')).toHaveTextContent(
      'Amaliyot davri hali boshlanmagan: Bahorgi amaliyot 2027, 01.02.2027 dan boshlanadi.',
    );
    expect(await screen.findByText('01.02.2027 dan boshlanadi')).toBeInTheDocument();
    expect(todayMocks.lastCheckinPhoto).toBeNull();
    expect(screen.queryByRole('list', { name: 'Belgilanish qadamlari' })).not.toBeInTheDocument();
  });
});

describe('QR sahifasi — ketganini belgilash va yakuniy holat', () => {
  it('check-in bor → "Ketganini belgilash" (QR → selfi → joylashuv) → yakuniy holat', async () => {
    stubGeolocation({ at: '2026-10-12T12:05:00Z' }); // 17:05 Toshkent
    todayMocks.mockToday.checkin = {
      ...todayMocks.mockToday.checkin,
      status: 'present',
      checkInAt: '2026-10-12T03:58:00Z',
    };
    renderApp('/qr');

    expect(
      await screen.findByRole('heading', { name: 'Ketganini belgilash', level: 2 }),
    ).toBeInTheDocument();
    expect(screen.getByText(/Kelgan vaqtingiz: 08:58/)).toBeInTheDocument();
    await startAndScan('Ketganini belgilash');
    expect(await screen.findByText('Ketishni selfie bilan tasdiqlang')).toBeInTheDocument();
    await selfieAndSend(photoFile('ketdim.jpg', 'image/jpeg', 128));

    expect(
      await screen.findByRole('heading', { name: 'Ketganingiz belgilandi · 17:05' }),
    ).toBeInTheDocument();
    expect(todayMocks.lastCheckinPhoto?.size).toBe(128);
    expect(todayMocks.lastCheckinQr).toBe(todayMocks.MOCK_CHECKIN_QR);
    expect(todayMocks.mockToday.checkin.checkOutAt).toBe('2026-10-12T12:05:00.000Z');
  });

  it('check-in bor, check-out oynasi hali yopiq → kutish holati (tugma yo‘q)', async () => {
    server.use(
      http.get('/api/student/today', () =>
        HttpResponse.json({
          ...mockTodayNow(),
          window: { ...mockTodayNow().window, isOpen: false },
          checkin: {
            ...mockTodayNow().checkin,
            status: 'present',
            checkInAt: '2026-10-12T03:58:00Z',
          },
        }),
      ),
    );
    renderApp('/qr');
    expect(
      await screen.findByRole('heading', { name: 'Kelganingiz belgilangan · 08:58' }),
    ).toBeInTheDocument();
    expect(screen.getByText('Ketganingizni belgilash 17:00 dan ochiladi.')).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: /belgilash$/ })).not.toBeInTheDocument();
  });

  it('kelgan va ketgan → yakuniy holat: vaqtlar va holat chip', async () => {
    server.use(
      http.get('/api/student/today', () =>
        HttpResponse.json({
          ...mockTodayNow(),
          checkin: {
            ...mockTodayNow().checkin,
            status: 'late',
            checkInAt: '2026-10-12T04:20:00Z',
            checkOutAt: '2026-10-12T12:10:00Z',
          },
        }),
      ),
    );
    renderApp('/qr');
    expect(
      await screen.findByRole('heading', { name: 'Bugungi davomat yakunlangan' }),
    ).toBeInTheDocument();
    expect(screen.getByText('09:20')).toBeInTheDocument();
    expect(screen.getByText('17:10')).toBeInTheDocument();
    expect(screen.getByText('Kech keldi')).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: /belgilash$/ })).not.toBeInTheDocument();
  });

  it('avtomatik yopilgan + shubhali → "Kun avtomatik yakunlandi"', async () => {
    server.use(
      http.get('/api/student/today', () =>
        HttpResponse.json({
          ...mockTodayNow(),
          checkin: {
            ...mockTodayNow().checkin,
            status: 'present',
            checkInAt: '2026-10-12T03:58:00Z',
            autoClosed: true,
            suspicious: true,
          },
        }),
      ),
    );
    renderApp('/qr');
    expect(
      await screen.findByRole('heading', { name: 'Kun avtomatik yakunlandi' }),
    ).toBeInTheDocument();
    expect(screen.getByText('Avtomatik yopildi')).toBeInTheDocument();
    expect(screen.getByRole('note')).toHaveTextContent('shubhali');
  });
});

describe("QR sahifasi — bo'sh holatlar", () => {
  it.each([
    ['absent', 'Bugun belgilanmadingiz', 'Bugungi belgilanish oynasi yopilgan.'],
    ['dayOff', 'Bugun dam olish kuni', 'Bugun ish kuni emas.'],
    ['excused', 'Bugun sababli', 'Bu kunga ruxsat tasdiqlangan — belgilanish shart emas.'],
    ['pending', 'Belgilanish oynasi yopiq', 'Belgilanish oynasi hali ochilmagan.'],
  ] as const)('%s → "%s"', async (status, title, note) => {
    server.use(
      http.get('/api/student/today', () =>
        HttpResponse.json({
          ...mockTodayNow(),
          window: { ...mockTodayNow().window, isOpen: false },
          checkin: { ...mockTodayNow().checkin, status, note },
        }),
      ),
    );
    renderApp('/qr');
    expect(await screen.findByText(title)).toBeInTheDocument();
    expect(screen.getByText(note)).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Kelganini belgilash' })).not.toBeInTheDocument();
  });

  it('korxona biriktirilmagan → holat va "Amaliyot joyini ko‘rish" (href\'siz) → /joyim', async () => {
    server.use(
      http.get('/api/student/today', () =>
        HttpResponse.json({
          ...mockTodayNow(),
          place: null,
          checkin: { ...mockTodayNow().checkin, note: 'Amaliyot joyingiz hali tasdiqlanmagan.' },
        }),
      ),
    );
    const router = renderApp('/qr');
    expect(await screen.findByText('Korxona biriktirilmagan')).toBeInTheDocument();
    expect(screen.getByText('Amaliyot joyingiz hali tasdiqlanmagan.')).toBeInTheDocument();
    const link = screen.getByRole('link', { name: 'Amaliyot joyini ko‘rish' });
    expect(link).not.toHaveAttribute('href');
    fireEvent.click(link);
    await waitFor(() => expect(router.state.location.pathname).toBe('/joyim'));
  });

  it('davr biriktirilmagan → bo‘sh holat', async () => {
    server.use(
      http.get('/api/student/today', () =>
        HttpResponse.json({ ...mockTodayNow(), period: null, place: null }),
      ),
    );
    setPeriodDaysVariant('none');
    renderApp('/qr');
    expect(await screen.findByText('Amaliyot davri biriktirilmagan')).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Kelganini belgilash' })).not.toBeInTheDocument();
  });

  it('davr hali boshlanmagan → PeriodGapCard (qolgan kun)', async () => {
    setPeriodGap('upcoming');
    renderApp('/qr');
    expect(await screen.findByText('01.02.2027 dan boshlanadi')).toBeInTheDocument();
    expect(screen.getByText('43 kun qoldi')).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Kelganini belgilash' })).not.toBeInTheDocument();
  });

  it('davr tugagan → "Amaliyot davri tugagan"', async () => {
    setPeriodGap('ended');
    renderApp('/qr');
    expect(
      await screen.findByRole('heading', { name: 'Amaliyot davri tugagan: Kuzgi amaliyot 2026' }),
    ).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Kelganini belgilash' })).not.toBeInTheDocument();
  });
});

describe('QR sahifasi — yuz tekshiruvi (v3.27)', () => {
  it('faceRequired → "Rasmsiz davom etish" yo‘q, solishtirish izohi bor', async () => {
    stubGeolocation();
    faceMocks.setMockFace({ required: true, status: 'approved' });
    todayMocks.setFaceCheck({ required: true });
    renderApp('/qr');
    await startAndScan('Kelganini belgilash');
    expect(screen.getByText(/tasdiqlangan yuz rasmingiz bilan solishtiriladi/)).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Rasmsiz davom etish' })).not.toBeInTheDocument();
    await selfieAndSend();
    expect(
      await screen.findByRole('heading', { name: 'Kelganingiz belgilandi · 09:02' }),
    ).toBeInTheDocument();
  });

  it('faceMismatch → server xabari selfi qadamida, qayta olish so‘raladi', async () => {
    stubGeolocation();
    faceMocks.setMockFace({ required: true, status: 'approved' });
    todayMocks.setFaceCheck({ required: true, result: 'mismatch' });
    renderApp('/qr');
    await startAndScan('Kelganini belgilash');
    await selfieAndSend();
    expect(await screen.findByRole('alert')).toHaveTextContent(
      todayMocks.FACE_REJECT_MESSAGES.faceMismatch,
    );
    // Selfi qadami: rasm tashlab yuborilgan, "Rasmga olish" qayta.
    expect(screen.getByRole('button', { name: 'Rasmga olish' })).toBeInTheDocument();
    expect(screen.queryByAltText('Olingan selfie')).not.toBeInTheDocument();
    expect(todayMocks.mockToday.checkin.checkInAt).toBeNull();
  });

  it('faceNotDetected → selfi qadamiga qaytadi', async () => {
    stubGeolocation();
    faceMocks.setMockFace({ required: true, status: 'approved' });
    todayMocks.setFaceCheck({ required: true, result: 'notDetected' });
    renderApp('/qr');
    await startAndScan('Kelganini belgilash');
    await selfieAndSend();
    expect(await screen.findByRole('alert')).toHaveTextContent(
      todayMocks.FACE_REJECT_MESSAGES.faceNotDetected,
    );
    expect(screen.getByRole('button', { name: 'Rasmga olish' })).toBeInTheDocument();
  });

  it('faceNotEnrolled (etalon tekshirilmoqda) → xabar va "Yuzni tasdiqlash" → /face', async () => {
    stubGeolocation();
    faceMocks.setMockFace({
      required: true,
      status: 'pending',
      photoUrl: faceMocks.MOCK_FACE_PHOTO_URL,
    });
    todayMocks.setFaceCheck({ required: true });
    const router = renderApp('/qr');
    await startAndScan('Kelganini belgilash');
    await selfieAndSend();
    expect(await screen.findByRole('alert')).toHaveTextContent(
      todayMocks.FACE_REJECT_MESSAGES.faceNotEnrolled,
    );
    fireEvent.click(screen.getByRole('link', { name: 'Yuzni tasdiqlash' }));
    expect(
      await screen.findByRole('heading', { name: 'Yuzni tasdiqlash', level: 1 }),
    ).toBeInTheDocument();
    expect(router.state.location.pathname).toBe('/face');
  });

  it('rejectReason kengaytmasisiz matn bo‘yicha ham ajratiladi (409 mos kelmadi)', async () => {
    stubGeolocation();
    server.use(
      http.post('/api/student/checkin', () =>
        problem(409, 'Ziddiyat', 'Selfidagi yuz tasdiqlangan rasmingizga mos kelmadi.'),
      ),
    );
    renderApp('/qr');
    await startAndScan('Kelganini belgilash');
    await selfieAndSend();
    expect(await screen.findByRole('alert')).toHaveTextContent('mos kelmadi');
    expect(screen.getByRole('button', { name: 'Rasmga olish' })).toBeInTheDocument();
  });
});
