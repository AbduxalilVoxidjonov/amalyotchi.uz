import { http, HttpResponse } from 'msw';
import { act, cleanup, fireEvent, screen, waitFor } from '@testing-library/react';
// `lastCheckinPhoto` mock ichida qayta tayinlanadi — namespace orqali o'qiladi.
import * as todayMocks from '@/features/today/mocks';
import { mockToday, setCheckinPhotoRequired } from '@/features/today/mocks';
import { server } from '@/mocks/server';
import { removeGeolocation, renderApp, stubGeolocation } from '@/test/render-app';

afterEach(() => removeGeolocation());

/** Kamera bergan fayl (jsdom'da canvas yo'q — `compressImage` asl faylni qaytaradi). */
function photoFile(name = 'selfie.jpg', type = 'image/jpeg', bytes = 64): File {
  return new File([new Uint8Array(bytes)], name, { type });
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
    fireEvent.click(screen.getByRole('button', { name: 'KELDIM' }));
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
    fireEvent.click(screen.getByRole('button', { name: 'KETDIM' }));
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
    fireEvent.click(await screen.findByRole('button', { name: 'KELDIM' }));
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
    fireEvent.click(await screen.findByRole('button', { name: 'KELDIM' }));
    capturePhoto(photoFile('hujjat.pdf', 'application/pdf'));

    expect(await screen.findByText(/Faqat rasm yuborish mumkin/)).toBeInTheDocument();
    expect(screen.queryByAltText('Olingan selfie')).not.toBeInTheDocument();
  });

  it("qo'llab-quvvatlanmaydigan rasm formati rad etiladi (siqish mumkin bo'lmaganda)", async () => {
    stubGeolocation();
    renderApp('/');
    fireEvent.click(await screen.findByRole('button', { name: 'KELDIM' }));
    capturePhoto(photoFile('rasm.gif', 'image/gif'));

    expect(await screen.findByText(/Rasm formati qo‘llab-quvvatlanmaydi/)).toBeInTheDocument();
  });

  it("joylashuvga ruxsat yo'q → darhol xabar, belgilanish bo'lmaydi", async () => {
    stubGeolocation({ errorCode: 1 });
    renderApp('/');
    fireEvent.click(await screen.findByRole('button', { name: 'KELDIM' }));

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
    fireEvent.click(await screen.findByRole('button', { name: 'KELDIM' }));
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
    fireEvent.click(await screen.findByRole('button', { name: 'KELDIM' }));
    capturePhoto();
    fireEvent.click(await screen.findByRole('button', { name: 'Tasdiqlash va yuborish' }));

    expect(await screen.findByText(/Server bilan aloqa yo.q/)).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Tasdiqlash va yuborish' })).toBeEnabled();
  });

  it('rasmsiz davom etish — sozlama majbur qilmasa qabul qilinadi', async () => {
    stubGeolocation();
    renderApp('/');
    fireEvent.click(await screen.findByRole('button', { name: 'KELDIM' }));
    fireEvent.click(await screen.findByRole('button', { name: 'Rasmsiz davom etish' }));

    expect(await screen.findByText('Belgilandingiz · 09:02')).toBeInTheDocument();
    expect(todayMocks.lastCheckinPhoto).toBeNull();
  });

  it('checkinPhotoRequired=true → rasmsiz yuborish 400 (errors.Photo) bilan rad etiladi', async () => {
    stubGeolocation();
    setCheckinPhotoRequired(true);
    renderApp('/');
    fireEvent.click(await screen.findByRole('button', { name: 'KELDIM' }));
    fireEvent.click(await screen.findByRole('button', { name: 'Rasmsiz davom etish' }));

    expect(await screen.findByText('Check-in uchun rasm majburiy.')).toBeInTheDocument();
    expect(screen.getByText('Kutilmoqda')).toBeInTheDocument();

    // Rasm olib qayta yuborilsa — qabul qilinadi.
    capturePhoto();
    fireEvent.click(await screen.findByRole('button', { name: 'Tasdiqlash va yuborish' }));
    expect(await screen.findByText('Belgilandingiz · 09:02')).toBeInTheDocument();
  });

  it('09:15 dan keyin → Kech keldi', async () => {
    stubGeolocation({ at: '2026-10-12T04:31:00Z' }); // 09:31 Toshkent
    renderApp('/');
    fireEvent.click(await screen.findByRole('button', { name: 'KELDIM' }));
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

  it('kundalik: hisoblagich va amaliyot joyi qisqachasi', async () => {
    renderApp('/');
    expect(await screen.findByText('Bugungi kundalik')).toBeInTheDocument();
    expect(screen.getByText('0 / 150 belgi')).toBeInTheDocument();
    const ta = screen.getByPlaceholderText('Bugun bajarilgan ishlar — kamida 150 belgi');
    await act(() => {
      fireEvent.change(ta, { target: { value: 'Salom dunyo' } });
    });
    expect(screen.getByText('11 / 150 belgi')).toBeInTheDocument();
    expect(screen.getByRole('link', { name: 'Tech Solutions MChJ' })).toHaveAttribute(
      'href',
      '/joyim',
    );
    expect(screen.getByText('94%')).toBeInTheDocument();
    expect(screen.getByText('4,2')).toBeInTheDocument();
    await waitFor(() => expect(screen.getByRole('button', { name: 'KELDIM' })).toBeEnabled());
  });
});
