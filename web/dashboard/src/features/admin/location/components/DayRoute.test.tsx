import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { render, screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { http, HttpResponse } from 'msw';
import { MemoryRouter } from 'react-router-dom';
import { server } from '@/mocks/server';
import { AttendanceDayTable } from '../../student-profile/components/AttendanceDayTable';
import type { StudentAttendanceDay } from '../../student-profile/types';
import type { LocationPingsResponse } from '../types';
import { DayRoute } from './DayRoute';

/** Leaflet jsdom'da ishlamaydi — xarita qatlami stub. */
vi.mock('./TrackMapCanvas', () => ({
  default: ({ pings, maxArrows }: { pings: unknown[]; maxArrows?: number }) => (
    <div data-testid="track-map">
      {pings.length} nuqta · {maxArrows} strelka
    </div>
  ),
}));

const URL_RE = '/api/admin/students/:studentId/location-pings';

function renderWithProviders(ui: React.ReactNode) {
  const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  return render(
    <QueryClientProvider client={queryClient}>
      <MemoryRouter>{ui}</MemoryRouter>
    </QueryClientProvider>,
  );
}

function response(over: Partial<LocationPingsResponse>): LocationPingsResponse {
  return {
    date: '2026-10-07',
    place: { name: 'Tech Solutions MChJ', lat: 41.3111, lng: 69.2797, radiusM: 150 },
    checkInAt: '2026-10-07T04:02:00Z',
    checkOutAt: null,
    consentGiven: true,
    pings: [],
    summary: {
      total: 0,
      inside: 0,
      outside: 0,
      inaccurate: 0,
      outsideMinutes: 0,
      longestGapMinutes: 0,
      lastPingAt: null,
    },
    ...over,
  };
}

describe('DayRoute', () => {
  it("ping'lar bor kun — xulosa, xarita va to'liq iz havolasi", async () => {
    // 2026-10-12 (dushanba) — Aliyev: 09:04–13:00, 10:20–10:44 tashqarida.
    renderWithProviders(
      <DayRoute studentId="s-341030" studentName="Aliyev Akmal" date="2026-10-12" />,
    );
    const section = screen.getByRole('region', { name: "Harakat yo'li" });
    expect(within(section).getByText('Joylashuv izi yuklanmoqda…')).toBeInTheDocument();
    expect(await within(section).findByTestId('track-map')).toHaveTextContent(
      '119 nuqta · 10 strelka',
    );
    expect(
      within(section).getByText('119 ta nuqta · 09:04 → 13:00 · tashqarida 24 daq'),
    ).toBeInTheDocument();
    expect(within(section).getByText('A — boshlanish')).toBeInTheDocument();
    expect(within(section).getByRole('link', { name: "To'liq izni ochish" })).toHaveAttribute(
      'href',
      '/admin/today/students/s-341030/location?date=2026-10-12&name=Aliyev+Akmal&from=student',
    );
  });

  it("ping yo'q kun (dam olish) — bo'sh holat, xarita yo'q", async () => {
    renderWithProviders(<DayRoute studentId="s-341030" date="2026-10-10" />);
    expect(await screen.findByText("Bu kun uchun joylashuv izi yo'q")).toBeInTheDocument();
    expect(screen.queryByTestId('track-map')).not.toBeInTheDocument();
  });

  it("rozilik bo'lmasa bo'sh holatda izoh", async () => {
    server.use(http.get(URL_RE, () => HttpResponse.json(response({ consentGiven: false }))));
    renderWithProviders(<DayRoute studentId="s-1" date="2026-10-07" />);
    expect(await screen.findByText("Bu kun uchun joylashuv izi yo'q")).toBeInTheDocument();
    expect(screen.getByText('Talaba fonda kuzatuvga rozilik bermagan.')).toBeInTheDocument();
  });

  it('bitta nuqta — ogohlantirish va xarita', async () => {
    server.use(
      http.get(URL_RE, () =>
        HttpResponse.json(
          response({
            pings: [
              {
                occurredAt: '2026-10-07T04:05:00Z',
                lat: 41.3112,
                lng: 69.2798,
                accuracyM: 10,
                distanceM: 15,
                status: 'inside',
                isMocked: false,
                source: 'foreground',
              },
            ],
          }),
        ),
      ),
    );
    renderWithProviders(<DayRoute studentId="s-1" date="2026-10-07" />);
    expect(await screen.findByTestId('track-map')).toHaveTextContent('1 nuqta');
    expect(screen.getByText('1 ta nuqta · 09:05')).toBeInTheDocument();
    expect(screen.getByText(/Faqat bitta nuqta qayd etilgan/)).toBeInTheDocument();
  });

  it('xato — xabar va qayta urinish', async () => {
    server.use(
      http.get(URL_RE, () =>
        HttpResponse.json(
          { status: 403, title: 'Ruxsat yo‘q', detail: "Bu talabaga ruxsatingiz yo'q." },
          { status: 403, headers: { 'Content-Type': 'application/problem+json' } },
        ),
      ),
    );
    renderWithProviders(<DayRoute studentId="s-1" date="2026-10-07" />);
    expect(await screen.findByRole('alert')).toHaveTextContent("Ma'lumotni yuklab bo'lmadi");
    expect(screen.getByRole('button', { name: 'Qayta urinish' })).toBeInTheDocument();
  });
});

describe('AttendanceDayTable · harakat yo‘li', () => {
  const day: StudentAttendanceDay = {
    date: '2026-10-12',
    status: 'present',
    isWorkDay: true,
    checkIn: null,
    checkOut: null,
    autoClosed: false,
    suspicious: false,
    suspiciousReason: null,
    manual: false,
    manualReason: null,
    leaveRequestId: null,
    diary: null,
    attempts: 0,
    rejectedAttempts: 0,
    events: [],
  };

  it('qator bosilsa kun oynasida o‘sha kun izi xaritasi chiqadi', async () => {
    const user = userEvent.setup();
    renderWithProviders(
      <AttendanceDayTable days={[day]} radiusM={150} studentId="s-341030" studentName="Aliyev" />,
    );
    await user.click(screen.getByText('12.10.2026'));
    const dialog = await screen.findByRole('dialog', { name: /12\.10\.2026 — kun tafsiloti/ });
    const section = within(dialog).getByRole('region', { name: "Harakat yo'li" });
    expect(await within(section).findByTestId('track-map')).toHaveTextContent('119 nuqta');
  });

  it("`studentId` berilmasa xarita bo'limi yo'q", async () => {
    const user = userEvent.setup();
    renderWithProviders(<AttendanceDayTable days={[day]} radiusM={150} />);
    await user.click(screen.getByText('12.10.2026'));
    const dialog = await screen.findByRole('dialog', { name: /kun tafsiloti/ });
    expect(within(dialog).queryByRole('region', { name: "Harakat yo'li" })).not.toBeInTheDocument();
  });
});
