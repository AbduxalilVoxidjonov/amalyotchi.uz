import { fireEvent, screen, waitFor, within } from '@testing-library/react';
import { renderAdminRoute } from '../shared/renderAdminRoute';

/** Leaflet jsdom'da ishlamaydi — xarita qatlami stub (MapPicker testidagi kabi). */
vi.mock('./components/TrackMapCanvas', () => ({
  default: ({ pings }: { pings: unknown[] }) => (
    <div data-testid="track-map">{pings.length} nuqta</div>
  ),
}));

const PATH = '/admin/today/students/s-341030/location?name=Aliyev+Akmal&date=2026-10-12';

function fact(grid: HTMLElement, label: string): string | null {
  const dt = within(grid).getByText(label, { selector: 'dt' });
  return dt.nextElementSibling?.textContent ?? null;
}

describe('LocationTrackPage', () => {
  it("summary, xarita va tashqarida bo'lgan oraliq (chiqib qaytgan talaba)", async () => {
    renderAdminRoute(PATH);
    expect(
      await screen.findByRole('heading', { name: 'Aliyev Akmal — joylashuv izi' }),
    ).toBeInTheDocument();
    expect(screen.getByRole('heading', { level: 1 })).toHaveTextContent('Bugun');

    const summary = await screen.findByLabelText('Kun xulosasi', { selector: 'dl' });
    // 09:05–13:00 har 5 daqiqada = 48 ping; 10:20–10:40 — 5 ta tashqarida.
    expect(fact(summary, 'Jami ping')).toBe('48');
    expect(fact(summary, 'Ichkarida')).toBe('43');
    expect(fact(summary, 'Tashqarida')).toBe('5');
    expect(fact(summary, 'Tashqarida vaqt')).toBe('25 daq');
    expect(fact(summary, 'Eng uzun uzilish')).toBe('5 daq');
    expect(fact(summary, 'Oxirgi ping')).toBe('13:00');

    expect(await screen.findByTestId('track-map')).toHaveTextContent('48 nuqta');
    expect(
      screen.getByText(/Tech Solutions MChJ · radius 150 m · check-in 09:02/),
    ).toBeInTheDocument();

    const timeline = screen.getByRole('region', { name: "Vaqt chizig'i" });
    expect(within(timeline).getByText('10:20–10:45')).toBeInTheDocument();
    expect(within(timeline).getByText('25 daq')).toBeInTheDocument();
  });

  it('soxta ping belgisi va sana tanlagich → ?date=', async () => {
    const router = renderAdminRoute(
      '/admin/today/students/s-341033/location?name=Yusupova+Nilufar&date=2026-10-12',
    );
    expect(await screen.findByText('Soxta joylashuv aniqlangan · 5 ta ping')).toBeInTheDocument();

    const date = screen.getByLabelText('Sana');
    expect(date).toHaveValue('2026-10-12');
    fireEvent.change(date, { target: { value: '2026-10-09' } });
    await waitFor(() =>
      expect(new URLSearchParams(router.state.location.search).get('date')).toBe('2026-10-09'),
    );
  });

  it("rozilik bo'lmasa — ogohlantirish, ping yo'q", async () => {
    renderAdminRoute('/admin/today/students/s-341047/location?date=2026-10-12');
    expect(await screen.findByText('Talaba fonda kuzatuvga rozilik bermagan')).toBeInTheDocument();
    expect(screen.getByRole('heading', { name: 'Talaba — joylashuv izi' })).toBeInTheDocument();
    expect(screen.getByText('Talaba korxona hududidan chiqmagan.')).toBeInTheDocument();
  });
});
