type Listener = (params?: unknown) => unknown;
const listeners = new Map<string, Set<Listener>>();

/** Bot API versiyalarini solishtirish ("6.10" > "6.4"). */
function versionAtLeast(current: string, required: string): boolean {
  const a = current.split('.').map(Number);
  const b = required.split('.').map(Number);
  for (let i = 0; i < Math.max(a.length, b.length); i += 1) {
    const x = a[i] ?? 0;
    const y = b[i] ?? 0;
    if (x !== y) return x > y;
  }
  return true;
}

/** `BackButton.onClick` obunachilari. */
const backClicks = new Set<Listener>();

/** Telegram "orqaga" tugmasi (yoki Android apparat tugmasi) bosildi. */
export function pressTelegramBack() {
  backClicks.forEach((cb) => cb());
}

/** Ochiq QR popup callback'i (`showScanQrPopup`) — `qrPopup` orqali boshqariladi. */
let qrCallback: ((text: string) => void | true) | null = null;

/** `@twa-dev/sdk` uchun test stub'i — `initData` ni testda o'zgartirish mumkin. */
export const webAppStub = {
  initData: '',
  initDataUnsafe: {},
  colorScheme: 'light' as 'light' | 'dark',
  themeParams: {},
  version: '8.0',
  isVersionAtLeast: (v: string) => versionAtLeast(webAppStub.version, v),
  ready: () => undefined,
  expand: () => undefined,
  disableVerticalSwipes: () => undefined,
  setHeaderColor: () => undefined,
  setBackgroundColor: () => undefined,
  setBottomBarColor: () => undefined,
  onEvent: (name: string, cb: Listener) => {
    if (!listeners.has(name)) listeners.set(name, new Set());
    listeners.get(name)!.add(cb);
  },
  offEvent: (name: string, cb: Listener) => {
    listeners.get(name)?.delete(cb);
  },
  showScanQrPopup: vi.fn((_params: { text?: string }, cb?: (text: string) => void | true) => {
    qrCallback = cb ?? null;
  }),
  closeScanQrPopup: vi.fn(() => {
    if (!qrCallback) return;
    qrCallback = null;
    emit('scanQrPopupClosed');
  }),
  openLink: () => undefined,
  BackButton: {
    isVisible: false,
    show: vi.fn(() => {
      webAppStub.BackButton.isVisible = true;
    }),
    hide: vi.fn(() => {
      webAppStub.BackButton.isVisible = false;
    }),
    onClick: vi.fn((cb: Listener) => {
      backClicks.add(cb);
    }),
    offClick: vi.fn((cb: Listener) => {
      backClicks.delete(cb);
    }),
  },
  HapticFeedback: { notificationOccurred: () => undefined },
};

function emit(name: string) {
  listeners.get(name)?.forEach((cb) => cb());
}

export function setInitData(value: string) {
  webAppStub.initData = value;
}

/** Telegram native QR popup'ini testda boshqarish. */
export const qrPopup = {
  get isOpen() {
    return qrCallback !== null;
  },
  /** Kamera QR'ni o'qidi — callback `true` qaytarsa popup yopiladi (Telegram ham `scanQrPopupClosed` chiqaradi). */
  scan(text: string) {
    const cb = qrCallback;
    if (!cb) throw new Error('QR popup ochiq emas');
    if (cb(text) === true) {
      qrCallback = null;
      emit('scanQrPopupClosed');
    }
  },
  /** Foydalanuvchi popup'ni yopdi (bekor qilish). */
  close() {
    qrCallback = null;
    emit('scanQrPopupClosed');
  },
};

export function resetTelegramStub() {
  webAppStub.version = '8.0';
  qrCallback = null;
  listeners.clear();
  backClicks.clear();
  webAppStub.BackButton.isVisible = false;
  webAppStub.BackButton.show.mockClear();
  webAppStub.BackButton.hide.mockClear();
  webAppStub.BackButton.onClick.mockClear();
  webAppStub.BackButton.offClick.mockClear();
  webAppStub.showScanQrPopup.mockClear();
  webAppStub.closeScanQrPopup.mockClear();
}
