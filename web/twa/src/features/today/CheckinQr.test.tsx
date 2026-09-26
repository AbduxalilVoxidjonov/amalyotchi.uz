import { http } from 'msw';
import { act, fireEvent, render, screen, waitFor, within } from '@testing-library/react';
import * as todayMocks from '@/features/today/mocks';
import { setCheckinQrRequired } from '@/features/today/mocks';
import { problem } from '@/mocks/problem';
import { server } from '@/mocks/server';
import { removeGeolocation, renderApp, stubGeolocation } from '@/test/render-app';
import { qrPopup, webAppStub } from '@/test/telegram-stub';
import { CameraQrScanner } from './components/CameraQrScanner';
import { CAMERA_MESSAGES } from './qr-scanner';
import { MOCK_TEST_QR, parseCheckinQr, QR_MESSAGES } from './qr';

afterEach(() => removeGeolocation());

const WRONG_TOKEN_QR = 'AMLQR:1:00000000000000000000000000000000';

function photoFile(): File {
  return new File([new Uint8Array(64)], 'selfie.jpg', { type: 'image/jpeg' });
}

function capturePhoto() {
  fireEvent.change(screen.getByLabelText('Selfie olish'), { target: { files: [photoFile()] } });
}

const KELDIM = 'Kelganini belgilash';
const SUCCESS = 'Kelganingiz belgilandi · 09:02';

/** QR sahifasida "Kelganini belgilash" → Telegram QR popup ochiladi (1-qadam). */
async function pressKeldim() {
  fireEvent.click(await screen.findByRole('button', { name: KELDIM }));
  await waitFor(() => expect(qrPopup.isOpen).toBe(true));
}

function steps() {
  return within(screen.getByRole('list', { name: 'Belgilanish qadamlari' }));
}

describe('parseCheckinQr', () => {
  it('faqat AMLQR:1: prefiksli payload qabul qilinadi', () => {
    expect(parseCheckinQr(`  ${MOCK_TEST_QR} `)).toBe(MOCK_TEST_QR);
    expect(parseCheckinQr('https://example.com')).toBeNull();
    expect(parseCheckinQr('AMLQR:2:abc')).toBeNull();
    expect(parseCheckinQr('AMLQR:1:')).toBeNull();
    expect(parseCheckinQr(null)).toBeNull();
  });
});

describe('Check-in (QR sahifasi): QR → selfi → joylashuv → yuborish', () => {
  it("to'liq oqim: QR skaner → selfi (joylashuv fonda) → joylashuv → FormData'da qr", async () => {
    const geo = stubGeolocation();
    renderApp('/qr');
    await pressKeldim();

    expect(webAppStub.showScanQrPopup).toHaveBeenCalledWith(
      { text: QR_MESSAGES.scanPrompt },
      expect.any(Function),
    );
    expect(screen.getByText('Amaliyot joyidagi QR kodni skanerlang')).toBeInTheDocument();
    expect(steps().getByText('QR').closest('li')).toHaveAttribute('aria-current', 'step');
    expect(
      steps()
        .getAllByRole('listitem')
        .map((li) => li.textContent),
    ).toEqual([
      expect.stringContaining('QR'),
      expect.stringContaining('Selfi'),
      expect.stringContaining('Joylashuv'),
    ]);
    // Joylashuv QR'dan OLDIN so'ralmaydi.
    expect(geo).not.toHaveBeenCalled();

    act(() => qrPopup.scan(MOCK_TEST_QR));
    expect(await screen.findByText('QR tasdiqlandi')).toBeInTheDocument();
    expect(qrPopup.isOpen).toBe(false);
    expect(geo).toHaveBeenCalledTimes(1);
    expect(await screen.findByText('Selfie bilan tasdiqlang')).toBeInTheDocument();
    expect(steps().getByText('QR').closest('li')).toHaveAttribute('data-state', 'done');
    expect(steps().getByText('Selfi').closest('li')).toHaveAttribute('aria-current', 'step');
    // Joylashuv fonda olingan bo'lsa ham qadam sifatida selfidan KEYIN.
    expect(steps().getByText('Joylashuv').closest('li')).toHaveAttribute('data-state', 'todo');

    capturePhoto();
    fireEvent.click(await screen.findByRole('button', { name: 'Tasdiqlash va yuborish' }));
    expect(await screen.findByText(SUCCESS)).toBeInTheDocument();
    expect(todayMocks.lastCheckinQr).toBe(MOCK_TEST_QR);
    expect(todayMocks.lastCheckinPhoto).toMatchObject({ type: 'image/jpeg', size: 64 });
    expect(geo).toHaveBeenCalledTimes(1);
  });

  it("begona QR (prefiks yo'q) rad etiladi — serverga yuborilmaydi, qayta skanerlash mumkin", async () => {
    const geo = stubGeolocation();
    renderApp('/qr');
    await pressKeldim();

    act(() => qrPopup.scan('https://example.com/menu'));
    expect(await screen.findByRole('alert')).toHaveTextContent('Bu amaliyot joyining QR kodi emas');
    expect(screen.queryByText('QR tasdiqlandi')).not.toBeInTheDocument();
    expect(screen.queryByText('Selfie bilan tasdiqlang')).not.toBeInTheDocument();
    expect(geo).not.toHaveBeenCalled();
    expect(todayMocks.lastCheckinQr).toBeNull();

    fireEvent.click(screen.getByRole('button', { name: 'QR kodni skanerlash' }));
    await waitFor(() => expect(qrPopup.isOpen).toBe(true));
    expect(webAppStub.showScanQrPopup).toHaveBeenCalledTimes(2);
    act(() => qrPopup.scan(MOCK_TEST_QR));
    expect(await screen.findByText('QR tasdiqlandi')).toBeInTheDocument();
  });

  it('skaner bekor qilinsa — xabar, oqim QR qadamida qoladi; "Bekor qilish" → boshlash tugmasi', async () => {
    const geo = stubGeolocation();
    renderApp('/qr');
    await pressKeldim();

    act(() => qrPopup.close());
    expect(await screen.findByRole('alert')).toHaveTextContent(QR_MESSAGES.cancelled);
    expect(screen.getByRole('button', { name: 'QR kodni skanerlash' })).toBeEnabled();
    expect(geo).not.toHaveBeenCalled();

    fireEvent.click(screen.getByRole('button', { name: 'Bekor qilish' }));
    expect(await screen.findByRole('button', { name: KELDIM })).toBeInTheDocument();
    expect(screen.queryByRole('list', { name: 'Belgilanish qadamlari' })).not.toBeInTheDocument();
  });

  it('eski Telegram (6.4 dan past) → "Telegram ilovasini yangilang", skaner ochilmaydi', async () => {
    stubGeolocation();
    webAppStub.version = '6.2';
    renderApp('/qr');
    fireEvent.click(await screen.findByRole('button', { name: KELDIM }));

    expect(await screen.findByRole('alert')).toHaveTextContent(
      'QR skanerlash uchun Telegram ilovasini yangilang',
    );
    expect(webAppStub.showScanQrPopup).not.toHaveBeenCalled();
    expect(screen.queryByRole('button', { name: 'QR kodni skanerlash' })).not.toBeInTheDocument();
  });

  it("server 409 (begona korxona QR'i) → detail ko'rsatiladi, rasm saqlanadi, qayta skanerlab yuboriladi", async () => {
    stubGeolocation();
    renderApp('/qr');
    await pressKeldim();
    act(() => qrPopup.scan(WRONG_TOKEN_QR));
    expect(await screen.findByText('QR tasdiqlandi')).toBeInTheDocument();

    capturePhoto();
    fireEvent.click(await screen.findByRole('button', { name: 'Tasdiqlash va yuborish' }));

    expect(await screen.findByRole('alert')).toHaveTextContent(
      'QR kod bu amaliyot joyiga tegishli emas.',
    );
    expect(screen.getByText('Amaliyot joyidagi QR kodni skanerlang')).toBeInTheDocument();

    fireEvent.click(screen.getByRole('button', { name: 'QR kodni skanerlash' }));
    await waitFor(() => expect(qrPopup.isOpen).toBe(true));
    act(() => qrPopup.scan(MOCK_TEST_QR));
    // Rasm avval olingan — to'g'ridan-to'g'ri preview'ga qaytiladi.
    expect(await screen.findByAltText('Olingan selfie')).toBeInTheDocument();
    fireEvent.click(screen.getByRole('button', { name: 'Tasdiqlash va yuborish' }));
    expect(await screen.findByText(SUCCESS)).toBeInTheDocument();
    expect(todayMocks.lastCheckinQr).toBe(MOCK_TEST_QR);
  });

  it('server 400 errors.Qr → xabar QR qadamida', async () => {
    stubGeolocation();
    server.use(
      http.post('/api/student/checkin', () =>
        problem(400, "Ma'lumotlar noto'g'ri", "Kiritilgan ma'lumotlarda xatolik bor.", {
          errors: { Qr: ['Amaliyot joyidagi QR kodni skanerlang.'] },
        }),
      ),
    );
    renderApp('/qr');
    await pressKeldim();
    act(() => qrPopup.scan(MOCK_TEST_QR));
    capturePhoto();
    fireEvent.click(await screen.findByRole('button', { name: 'Tasdiqlash va yuborish' }));

    expect(await screen.findByRole('alert')).toHaveTextContent(
      'Amaliyot joyidagi QR kodni skanerlang.',
    );
    expect(screen.queryByText('QR tasdiqlandi')).not.toBeInTheDocument();
  });

  it('qrRequired=false → QR qadamisiz: boshlash → selfi → joylashuv → yuborish, qr yuborilmaydi', async () => {
    const geo = stubGeolocation();
    setCheckinQrRequired(false);
    renderApp('/qr');
    fireEvent.click(await screen.findByRole('button', { name: KELDIM }));

    expect(await screen.findByText('Selfie bilan tasdiqlang')).toBeInTheDocument();
    expect(webAppStub.showScanQrPopup).not.toHaveBeenCalled();
    expect(steps().queryByText('QR')).not.toBeInTheDocument();
    expect(steps().getAllByRole('listitem')).toHaveLength(2);
    expect(steps().getByText('Selfi').closest('li')).toHaveAttribute('aria-current', 'step');
    expect(screen.queryByText('QR tasdiqlandi')).not.toBeInTheDocument();
    expect(geo).toHaveBeenCalledTimes(1);

    capturePhoto();
    fireEvent.click(await screen.findByRole('button', { name: 'Tasdiqlash va yuborish' }));
    expect(await screen.findByText(SUCCESS)).toBeInTheDocument();
    expect(todayMocks.lastCheckinQr).toBeNull();
  });

  it("ketganini belgilash ham QR bilan (FormData'da qr)", async () => {
    stubGeolocation();
    renderApp('/qr');
    await pressKeldim();
    act(() => qrPopup.scan(MOCK_TEST_QR));
    capturePhoto();
    fireEvent.click(await screen.findByRole('button', { name: 'Tasdiqlash va yuborish' }));
    expect(await screen.findByText(SUCCESS)).toBeInTheDocument();

    // Muvaffaqiyat ekrani → bosh ekran → yana QR bo'limi: endi "Ketganini belgilash".
    fireEvent.click(screen.getByRole('link', { name: 'Bosh ekranga' }));
    await screen.findByRole('heading', { name: 'Bosh ekran', level: 1 });
    fireEvent.click(
      within(screen.getByRole('navigation', { name: "Bo'limlar" })).getByRole('link', {
        name: 'QR orqali belgilash',
      }),
    );
    fireEvent.click(await screen.findByRole('button', { name: 'Ketganini belgilash' }));
    await waitFor(() => expect(qrPopup.isOpen).toBe(true));
    act(() => qrPopup.scan(MOCK_TEST_QR));
    capturePhoto();
    fireEvent.click(await screen.findByRole('button', { name: 'Tasdiqlash va yuborish' }));
    expect(await screen.findByText('Ketganingiz belgilandi · 09:02')).toBeInTheDocument();
    expect(todayMocks.lastCheckinQr).toBe(MOCK_TEST_QR);
  });
});

describe('CameraQrScanner (brauzer, Telegram tashqarisida)', () => {
  const originalMediaDevices = navigator.mediaDevices;

  afterEach(() => {
    Object.defineProperty(navigator, 'mediaDevices', {
      configurable: true,
      value: originalMediaDevices,
    });
    delete (globalThis as { BarcodeDetector?: unknown }).BarcodeDetector;
    vi.restoreAllMocks();
  });

  function stubCamera(getUserMedia: () => Promise<MediaStream>) {
    Object.defineProperty(navigator, 'mediaDevices', {
      configurable: true,
      value: { getUserMedia: vi.fn(getUserMedia) },
    });
  }

  it('kameraga ruxsat berilmasa — tushunarli xabar', async () => {
    stubCamera(() => Promise.reject(new DOMException('denied', 'NotAllowedError')));
    const onDone = vi.fn();
    render(<CameraQrScanner onDone={onDone} />);
    await waitFor(() =>
      expect(onDone).toHaveBeenCalledWith({ kind: 'error', message: CAMERA_MESSAGES.denied }),
    );
  });

  it("kamera yo'q — tushunarli xabar", async () => {
    stubCamera(() => Promise.reject(new DOMException('none', 'NotFoundError')));
    const onDone = vi.fn();
    render(<CameraQrScanner onDone={onDone} />);
    await waitFor(() =>
      expect(onDone).toHaveBeenCalledWith({ kind: 'error', message: CAMERA_MESSAGES.notFound }),
    );
  });

  it("orqa kamera + BarcodeDetector: QR topilgach oqim to'xtatiladi", async () => {
    const stop = vi.fn();
    const stream = { getTracks: () => [{ stop }] } as unknown as MediaStream;
    stubCamera(() => Promise.resolve(stream));
    vi.spyOn(HTMLMediaElement.prototype, 'play').mockResolvedValue(undefined);
    (globalThis as { BarcodeDetector?: unknown }).BarcodeDetector = class {
      static getSupportedFormats = () => Promise.resolve(['qr_code']);
      detect = () => Promise.resolve([{ rawValue: MOCK_TEST_QR }]);
    };
    const onDone = vi.fn();
    render(<CameraQrScanner onDone={onDone} />);

    expect(screen.getByRole('dialog', { name: 'QR skaner' })).toBeInTheDocument();
    await waitFor(() =>
      expect(onDone).toHaveBeenCalledWith({ kind: 'scanned', text: MOCK_TEST_QR }),
    );
    expect(navigator.mediaDevices.getUserMedia).toHaveBeenCalledWith({
      audio: false,
      video: { facingMode: { ideal: 'environment' } },
    });
    expect(stop).toHaveBeenCalled();
  });

  it('"Bekor qilish" → cancelled', async () => {
    stubCamera(() => new Promise<MediaStream>(() => undefined));
    const onDone = vi.fn();
    render(<CameraQrScanner onDone={onDone} />);
    fireEvent.click(screen.getByRole('button', { name: 'Bekor qilish' }));
    expect(onDone).toHaveBeenCalledWith({ kind: 'cancelled' });
  });
});
