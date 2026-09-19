import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MapPicker } from './MapPicker';

/**
 * Leaflet qatlami (`MapPickerCanvas`) jsdom'da ishlamaydi va alohida `lazy` chunk —
 * shuning uchun stub bilan almashtiriladi, test faqat qobiq mantig'ini (geolokatsiya
 * tugmasi, xato/aniqlik xabarlari) tekshiradi.
 */
vi.mock('./MapPickerCanvas', () => ({
  default: () => <div data-testid="map-canvas" />,
}));

const LOCATE = 'Hozirgi joylashuvim';

/** `navigator.geolocation` ni berilgan `getCurrentPosition` bilan almashtiradi. */
function stubGeolocation(getCurrentPosition: unknown) {
  Object.defineProperty(navigator, 'geolocation', {
    configurable: true,
    value: { getCurrentPosition, watchPosition: vi.fn(), clearWatch: vi.fn() },
  });
}

/** `navigator.geolocation` ni butunlay olib tashlaydi (eski/qat'iy brauzerlar). */
function removeGeolocation() {
  Object.defineProperty(navigator, 'geolocation', { configurable: true, value: undefined });
}

function position(lat: number, lng: number, accuracy = 12) {
  return { coords: { latitude: lat, longitude: lng, accuracy }, timestamp: Date.now() };
}

beforeEach(() => {
  // jsdom'da http://localhost — tugma xavfsiz kontekst tekshiruvidan o'tsin.
  vi.stubGlobal('isSecureContext', true);
});

afterEach(() => {
  vi.unstubAllGlobals();
  removeGeolocation();
});

describe('MapPicker — "Hozirgi joylashuvim"', () => {
  it('muvaffaqiyatda onChange aniqlangan koordinata bilan chaqiriladi', async () => {
    const getCurrentPosition = vi.fn((ok: (p: unknown) => void) => ok(position(41.2995, 69.2401)));
    stubGeolocation(getCurrentPosition);
    const onChange = vi.fn();

    render(<MapPicker value={null} onChange={onChange} label="Korxona joylashuvi" />);
    await userEvent.click(screen.getByRole('button', { name: LOCATE }));

    expect(onChange).toHaveBeenCalledWith({ lat: 41.2995, lng: 69.2401 });
    expect(getCurrentPosition).toHaveBeenCalledWith(expect.any(Function), expect.any(Function), {
      enableHighAccuracy: true,
      timeout: 10_000,
      maximumAge: 0,
    });
    expect(screen.queryByRole('alert')).not.toBeInTheDocument();
  });

  it('PERMISSION_DENIED — ruxsat xabari chiqadi, onChange chaqirilmaydi', async () => {
    stubGeolocation(
      vi.fn((_ok: unknown, fail: (e: unknown) => void) =>
        fail({ code: 1, message: 'User denied Geolocation' }),
      ),
    );
    const onChange = vi.fn();

    render(<MapPicker value={null} onChange={onChange} label="Korxona joylashuvi" />);
    await userEvent.click(screen.getByRole('button', { name: LOCATE }));

    expect(screen.getByRole('alert')).toHaveTextContent(
      'Joylashuvga ruxsat berilmadi — brauzer sozlamalaridan ruxsat bering yoki xaritadan belgilang.',
    );
    expect(onChange).not.toHaveBeenCalled();
  });

  it('yomon aniqlik (850 m) — markerni to‘g‘rilash taklifi chiqadi', async () => {
    stubGeolocation(vi.fn((ok: (p: unknown) => void) => ok(position(41.31, 69.28, 850))));
    const onChange = vi.fn();

    render(<MapPicker value={null} onChange={onChange} label="Korxona joylashuvi" />);
    await userEvent.click(screen.getByRole('button', { name: LOCATE }));

    expect(onChange).toHaveBeenCalledTimes(1);
    expect(
      screen.getByText('Aniqlik ~850 m — kerak bo‘lsa markerni sudrab to‘g‘rilang.'),
    ).toBeInTheDocument();
  });

  it('navigator.geolocation yo‘q — qo‘llab-quvvatlamaslik xabari', async () => {
    removeGeolocation();
    const onChange = vi.fn();

    render(<MapPicker value={null} onChange={onChange} label="Korxona joylashuvi" />);
    await userEvent.click(screen.getByRole('button', { name: LOCATE }));

    expect(screen.getByRole('alert')).toHaveTextContent(
      'Brauzer joylashuvni aniqlashni qo‘llab-quvvatlamaydi.',
    );
    expect(onChange).not.toHaveBeenCalled();
  });

  it('xavfsiz kontekst emas — https xabari, so‘rov yuborilmaydi', async () => {
    vi.stubGlobal('isSecureContext', false);
    const getCurrentPosition = vi.fn();
    stubGeolocation(getCurrentPosition);

    render(<MapPicker value={null} onChange={vi.fn()} label="Korxona joylashuvi" />);
    await userEvent.click(screen.getByRole('button', { name: LOCATE }));

    expect(screen.getByRole('alert')).toHaveTextContent(
      'Joylashuv faqat xavfsiz ulanishda (https) ishlaydi — xaritadan qo‘lda belgilang.',
    );
    expect(getCurrentPosition).not.toHaveBeenCalled();
  });

  it('disabled — tugma ham o‘chirilgan', () => {
    stubGeolocation(vi.fn());
    render(<MapPicker value={null} onChange={vi.fn()} label="Korxona joylashuvi" disabled />);
    expect(screen.getByRole('button', { name: LOCATE })).toBeDisabled();
  });
});
