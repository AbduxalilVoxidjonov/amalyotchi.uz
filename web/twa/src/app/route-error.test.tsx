import { act, fireEvent, screen, within } from '@testing-library/react';
import {
  CHUNK_RELOAD_KEY,
  chunkReloadDeps,
  resetChunkReloadState,
} from '@/shared/lib/chunk-reload';
import { renderApp } from '@/test/render-app';

// QR sahifasi testda boshqariladigan xato bilan yiqiladi.
const crash = vi.hoisted(() => ({ error: null as Error | null }));
vi.mock('@/pages/QrPage', () => ({
  default: function CrashingQr() {
    if (crash.error) throw crash.error;
    return <p>QR ishlayapti</p>;
  },
}));

function clickTab(name: string) {
  const nav = screen.getByRole('navigation', { name: "Bo'limlar" });
  fireEvent.click(within(nav).getByRole('link', { name }));
}

describe('Route errorElement', () => {
  let reload: ReturnType<typeof vi.fn<() => void>>;

  beforeEach(() => {
    resetChunkReloadState();
    reload = vi.fn<() => void>();
    vi.spyOn(chunkReloadDeps, 'reload').mockImplementation(reload);
    // Xato ekrani console.error yozadi (React + RouteError) — test chiqishini toza saqlash.
    vi.spyOn(console, 'error').mockImplementation(() => undefined);
    vi.spyOn(console, 'warn').mockImplementation(() => undefined);
  });
  afterEach(() => {
    crash.error = null;
    vi.restoreAllMocks();
    resetChunkReloadState();
  });

  it("sahifa yiqilsa — do'stona ekran, tab-bar qoladi, boshqa tabga o'tish ishlaydi", async () => {
    crash.error = new TypeError("Cannot read properties of null (reading 'days')");
    renderApp('/');
    await screen.findByRole('heading', { name: 'Bosh ekran', level: 1 });

    await act(async () => clickTab('QR orqali belgilash'));
    expect(
      await screen.findByRole('heading', { name: "Sahifani ochib bo'lmadi" }),
    ).toBeInTheDocument();
    expect(screen.getByText(/Kutilmagan xato yuz berdi/)).toBeInTheDocument();
    // Texnik tafsilot yig'iladigan <details> ichida.
    const details = screen.getByText('Texnik tafsilot').closest('details')!;
    expect(details).not.toHaveAttribute('open');
    expect(within(details).getByText(/reading 'days'/)).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Qayta yuklash' })).toBeInTheDocument();
    // Shell saqlanadi: header sarlavhasi va tab-bar.
    expect(
      screen.getByRole('heading', { name: 'QR orqali belgilash', level: 1 }),
    ).toBeInTheDocument();
    expect(screen.getByRole('navigation', { name: "Bo'limlar" })).toBeInTheDocument();
    expect(reload).not.toHaveBeenCalled();

    await act(async () => clickTab('Profil'));
    expect(await screen.findByRole('heading', { name: 'Profil', level: 1 })).toBeInTheDocument();
    expect(screen.queryByText("Sahifani ochib bo'lmadi")).not.toBeInTheDocument();
  });

  it('"Bosh ekranga" tugmasi bosh ekranni ochadi', async () => {
    crash.error = new Error('boom');
    const router = renderApp('/qr');
    await screen.findByRole('heading', { name: "Sahifani ochib bo'lmadi" });
    await act(async () => fireEvent.click(screen.getByRole('button', { name: 'Bosh ekranga' })));
    expect(
      await screen.findByRole('heading', { name: 'Bosh ekran', level: 1 }),
    ).toBeInTheDocument();
    expect(router.state.location.pathname).toBe('/');
  });

  it('chunk yuklash xatosi → sahifa bir marta avtomatik qayta yuklanadi', async () => {
    crash.error = new TypeError(
      'Failed to fetch dynamically imported module: https://app.example/assets/QrPage-old.js',
    );
    renderApp('/qr');
    expect(await screen.findByRole('status', { name: 'Ilova yangilanmoqda…' })).toBeInTheDocument();
    expect(reload).toHaveBeenCalledTimes(1);
    expect(window.sessionStorage.getItem(CHUNK_RELOAD_KEY)).not.toBeNull();
  });

  it("chunk xatosi, lekin yaqinda reload bo'lgan → loop yo'q, xato ekrani ko'rsatiladi", async () => {
    window.sessionStorage.setItem(CHUNK_RELOAD_KEY, String(Date.now()));
    crash.error = new TypeError('Importing a module script failed.');
    renderApp('/qr');
    expect(
      await screen.findByRole('heading', { name: "Sahifani ochib bo'lmadi" }),
    ).toBeInTheDocument();
    expect(screen.getByText(/yangi versiyasi chiqdi/)).toBeInTheDocument();
    expect(reload).not.toHaveBeenCalled();
  });
});
