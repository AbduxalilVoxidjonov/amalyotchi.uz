import { http, HttpResponse } from 'msw';
import { act, cleanup, fireEvent, screen, waitFor } from '@testing-library/react';
import { mockToday } from '@/features/today/mocks';
import { server } from '@/mocks/server';
import { removeGeolocation, renderApp, stubGeolocation } from '@/test/render-app';

afterEach(() => removeGeolocation());

describe('HomePage (isTalaba)', () => {
  it('KELDIM → geolokatsiya → belgilandi → KETDIM → kun yakunlandi (toggle)', async () => {
    const geo = stubGeolocation();
    renderApp('/');

    expect(await screen.findByText('Belgilanish oynasi ochiq')).toBeInTheDocument();
    expect(screen.getByText('Bugun · 12.10.2026')).toBeInTheDocument();
    expect(screen.getByText('Kutilmoqda')).toBeInTheDocument();
    expect(screen.getByText('45 m / 150 m')).toBeInTheDocument();

    fireEvent.click(screen.getByRole('button', { name: 'KELDIM' }));
    expect(await screen.findByText('Belgilandingiz · 09:02')).toBeInTheDocument();
    expect(geo).toHaveBeenCalledTimes(1);
    expect(screen.getByText('Keldi')).toBeInTheDocument();
    expect(screen.getByText(/Korxonadan \d+ m masofada qayd etildi/)).toBeInTheDocument();

    fireEvent.click(screen.getByRole('button', { name: 'KETDIM' }));
    expect(await screen.findByText('Kun yakunlandi · 09:02')).toBeInTheDocument();
    // v2: status `present` qoladi (check-out alohida maydon), "Yakunlandi" badge.
    expect(screen.getByText('Keldi')).toBeInTheDocument();
    expect(screen.getByText('Yakunlandi')).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: /KELDIM|KETDIM/ })).not.toBeInTheDocument();
  });

  it('09:15 dan keyin → Kech keldi', async () => {
    stubGeolocation({ at: '2026-10-12T04:31:00Z' }); // 09:31 Toshkent
    renderApp('/');
    fireEvent.click(await screen.findByRole('button', { name: 'KELDIM' }));
    expect(await screen.findByText('Kech keldi')).toBeInTheDocument();
  });

  it("joylashuvga ruxsat yo'q → xabar, holat o'zgarmaydi", async () => {
    stubGeolocation({ errorCode: 1 });
    renderApp('/');
    fireEvent.click(await screen.findByRole('button', { name: 'KELDIM' }));
    expect(await screen.findByRole('alert')).toHaveTextContent('Joylashuvga ruxsat berilmadi');
    expect(screen.getByRole('button', { name: 'KELDIM' })).toBeInTheDocument();
    expect(screen.getByText('Kutilmoqda')).toBeInTheDocument();
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

  it('radius tashqarisi → server 409 xabari', async () => {
    stubGeolocation({ lat: 41.32, lng: 69.29 }); // ~1 km
    renderApp('/');
    fireEvent.click(await screen.findByRole('button', { name: 'KELDIM' }));
    expect(await screen.findByRole('alert')).toHaveTextContent(/Korxona radiusidan tashqaridasiz/);
    expect(screen.getByRole('button', { name: 'KELDIM' })).toBeInTheDocument();
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
