import type * as DiagNs from './diag';

vi.mock('@/shared/lib/env', () => ({
  env: {
    apiUrl: '',
    useMocks: false,
    isDev: false,
    isTest: false,
    devInitData: '',
    forceWebLogin: false,
  },
}));

type DiagModule = typeof DiagNs;

describe('diag (production beacon)', () => {
  let beacon: ReturnType<typeof vi.fn>;
  let mod: DiagModule;

  const payloads = () =>
    beacon.mock.calls.map(([url]) => {
      const u = new URL(String(url), 'https://app.example');
      expect(u.pathname).toBe('/api/__diag');
      return JSON.parse(u.searchParams.get('d')!) as Record<string, unknown>;
    });

  beforeEach(async () => {
    vi.resetModules();
    beacon = vi.fn(() => true);
    Object.defineProperty(navigator, 'sendBeacon', { configurable: true, value: beacon });
    mod = await import('./diag');
  });
  afterEach(() => {
    Object.defineProperty(navigator, 'sendBeacon', { configurable: true, value: undefined });
  });

  it("boot: navigatsiya turi, boot soni, UA; body bo'sh (faqat query)", () => {
    mod.installDiagnostics();
    expect(beacon).toHaveBeenCalledTimes(1);
    expect(beacon.mock.calls[0]).toHaveLength(1);
    const [boot] = payloads();
    expect(boot).toMatchObject({ e: 'boot', p: '/', b: 1, n: 1 });
    expect(boot).toHaveProperty('ua');
  });

  it('route va xato hodisalari; stack 300 belgigacha', () => {
    mod.installDiagnostics();
    mod.diagRoute('/kundalik');
    const err = new Error('x'.repeat(1000));
    mod.diagError('route-error', err);
    window.dispatchEvent(
      new PromiseRejectionEvent('unhandledrejection', {
        promise: Promise.resolve(),
        reason: new Error('rej'),
      }),
    );
    const [, route, routeErr, rejection] = payloads();
    expect(route).toMatchObject({ e: 'route', to: '/kundalik' });
    expect(routeErr).toMatchObject({ e: 'route-error' });
    expect(String(routeErr!['m']).length).toBeLessThanOrEqual(300);
    expect(String(routeErr!['s']).length).toBeLessThanOrEqual(300);
    expect(rejection).toMatchObject({ e: 'rejection', m: 'Error: rej' });
  });

  it(`sessiyada ko'pi bilan ${30} ta beacon; payload ≤ 1500 belgi`, () => {
    for (let i = 0; i < 40; i += 1) mod.diag('spam', { big: 'y'.repeat(3000) });
    expect(beacon).toHaveBeenCalledTimes(mod.MAX_BEACONS);
    for (const [url] of beacon.mock.calls) {
      const d = new URL(String(url), 'https://app.example').searchParams.get('d')!;
      expect(d.length).toBeLessThanOrEqual(1500);
    }
  });

  it("shaxsiy ma'lumot (JWT, initData, telefon, parol) beacon query'siga tushmaydi", () => {
    mod.installDiagnostics();
    const jwt = 'eyJhbGciOiJIUzI1NiJ9.eyJzdWIiOiIxMjM0NSJ9.c2lnbmF0dXJlLXZhbHVl';
    mod.diagError(
      'route-error',
      new Error(
        `Bearer ${jwt} query_id=AAH1&user=%7B%22id%22%3A1%7D&hash=abc123 tel +998 90 123-45-67 {"password":"Sir123"}`,
      ),
    );
    const [, err] = payloads();
    expect(err).toBeDefined();
    const m = String(err!['m']);
    expect(m).not.toContain(jwt);
    expect(m).not.toContain('abc123');
    expect(m).not.toContain('%7B%22id');
    expect(m).not.toContain('123-45-67');
    expect(m).not.toContain('Sir123');
    expect(m).toContain('[phone]');
  });

  it('redactSensitive: oddiy matn o‘zgarmaydi', () => {
    expect(mod.redactSensitive('TypeError: x is undefined at /kundalik')).toBe(
      'TypeError: x is undefined at /kundalik',
    );
  });

  it('shortUserAgent: Telegram Android WebView', () => {
    expect(
      mod.shortUserAgent(
        'Mozilla/5.0 (Linux; Android 10; Redmi Note 8 Pro Build/QP1A; wv) AppleWebKit/537.36 (KHTML, like Gecko) Version/4.0 Chrome/153.0.0.0 Mobile Safari/537.36 Telegram-Android/12.10.3',
      ),
    ).toBe('Android 10|wv|Version/4.0|Chrome/153|Telegram-Android/12.10.3');
  });
});

describe('diag test rejimida', () => {
  it("o'chiq (beacon yuborilmaydi)", async () => {
    vi.resetModules();
    vi.doUnmock('@/shared/lib/env');
    const beacon = vi.fn(() => true);
    Object.defineProperty(navigator, 'sendBeacon', { configurable: true, value: beacon });
    const mod = await import('./diag');
    mod.installDiagnostics();
    mod.diag('x');
    expect(beacon).not.toHaveBeenCalled();
    Object.defineProperty(navigator, 'sendBeacon', { configurable: true, value: undefined });
  });
});
