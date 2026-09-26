import { act, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { http, HttpResponse } from 'msw';
import { ACCOUNT_ENDPOINTS } from '@/features/auth/api';
import { issueSession, mockUsers } from '@/mocks/data';
import { server } from '@/mocks/server';
import { useAuthStore } from '@/shared/auth/store';
import { problemResponse } from '../../shared/mockProblem';
import { renderWithProviders } from '../../shared/renderWithProviders';
import { resetSettingsMock } from '../mocks';
import { SettingsPage } from '../SettingsPage';
import { LOGIN_FORMAT, LOGIN_TAKEN, resetAccountSecurityMock } from './mocks';

const admin = mockUsers[0]!;
const ADMIN_PASSWORD = admin.password;

function signIn() {
  useAuthStore.getState().setSession(issueSession(admin));
}

function renderSecurity() {
  return renderWithProviders(<SettingsPage />, ['/admin/settings?tab=security']);
}

function passwordCard() {
  return screen.getByRole('region', { name: 'Parolni almashtirish' });
}

function loginCard() {
  return screen.getByRole('region', { name: 'Loginni almashtirish' });
}

/** `login-available` so'rovlaridagi `login` qiymatlari (javob — asosiy mock'da). */
function captureAvailability(): string[] {
  const logins: string[] = [];
  server.use(
    http.get(ACCOUNT_ENDPOINTS.loginAvailable, ({ request }) => {
      logins.push(new URL(request.url).searchParams.get('login') ?? '');
      return undefined;
    }),
  );
  return logins;
}

describe('Sozlamalar · Hisob xavfsizligi', () => {
  beforeEach(() => signIn());
  afterEach(() => {
    resetAccountSecurityMock();
    resetSettingsMock();
  });

  it("varaqlar: default — tizim sozlamalari; 'Hisob xavfsizligi' → ikki karta", async () => {
    const user = userEvent.setup();
    renderWithProviders(<SettingsPage />, ['/admin/settings']);
    expect(await screen.findByRole('region', { name: 'Davomat va geofence' })).toBeInTheDocument();
    expect(screen.queryByRole('region', { name: 'Parolni almashtirish' })).not.toBeInTheDocument();

    await user.click(screen.getByRole('tab', { name: 'Hisob xavfsizligi' }));
    expect(screen.getByRole('tab', { name: 'Hisob xavfsizligi' })).toHaveAttribute(
      'aria-selected',
      'true',
    );
    expect(passwordCard()).toBeInTheDocument();
    expect(loginCard()).toBeInTheDocument();
    expect(screen.queryByRole('region', { name: 'Davomat va geofence' })).not.toBeInTheDocument();
  });

  describe('Parolni almashtirish', () => {
    it("mijoz tekshiruvi: bo'sh, qisqa, takror mos emas, joriy bilan bir xil", async () => {
      const user = userEvent.setup();
      const bodies: unknown[] = [];
      server.use(
        http.post(ACCOUNT_ENDPOINTS.changePassword, async ({ request }) => {
          bodies.push(await request.clone().json());
          return undefined;
        }),
      );
      renderSecurity();
      const card = passwordCard();
      const submit = within(card).getByRole('button', { name: 'Parolni almashtirish' });

      await user.click(submit);
      expect(within(card).getByText('Joriy parolni kiriting.')).toBeInTheDocument();
      expect(within(card).getByText('Yangi parolni kiriting.')).toBeInTheDocument();
      expect(within(card).getByText('Yangi parolni takrorlang.')).toBeInTheDocument();

      await user.type(within(card).getByLabelText('Joriy parol'), 'admin12345');
      await user.type(within(card).getByLabelText('Yangi parol'), 'short');
      await user.type(within(card).getByLabelText('Yangi parolni takrorlang'), 'other');
      await user.click(submit);
      expect(
        within(card).getByText("Parol 8–128 ta belgidan iborat bo'lishi kerak."),
      ).toBeInTheDocument();
      expect(within(card).getByText('Parollar mos kelmadi.')).toBeInTheDocument();

      await user.clear(within(card).getByLabelText('Yangi parol'));
      await user.type(within(card).getByLabelText('Yangi parol'), 'admin12345');
      await user.clear(within(card).getByLabelText('Yangi parolni takrorlang'));
      await user.type(within(card).getByLabelText('Yangi parolni takrorlang'), 'admin12345');
      await user.click(submit);
      expect(
        within(card).getByText('Yangi parol joriy paroldan farq qilishi kerak.'),
      ).toBeInTheDocument();
      expect(bodies).toEqual([]);
    });

    it('muvaffaqiyat → refresh token bilan yuboriladi, forma tozalanadi, sessiya saqlanadi', async () => {
      const user = userEvent.setup();
      const bodies: unknown[] = [];
      server.use(
        http.post(ACCOUNT_ENDPOINTS.changePassword, async ({ request }) => {
          bodies.push(await request.clone().json());
          return undefined;
        }),
      );
      renderSecurity();
      const card = passwordCard();
      const { refreshToken, accessToken } = useAuthStore.getState();

      await user.click(within(card).getByRole('button', { name: "Parollarni ko'rsatish" }));
      expect(within(card).getByLabelText('Yangi parol')).toHaveAttribute('type', 'text');

      await user.type(within(card).getByLabelText('Joriy parol'), ADMIN_PASSWORD);
      await user.type(within(card).getByLabelText('Yangi parol'), 'yangiParol2026');
      await user.type(within(card).getByLabelText('Yangi parolni takrorlang'), 'yangiParol2026');
      await user.click(within(card).getByRole('button', { name: 'Parolni almashtirish' }));

      expect(await within(card).findByText('Parol almashtirildi.')).toBeInTheDocument();
      expect(bodies).toEqual([
        { currentPassword: ADMIN_PASSWORD, newPassword: 'yangiParol2026', refreshToken },
      ]);
      expect(within(card).getByLabelText('Joriy parol')).toHaveValue('');
      expect(within(card).getByLabelText('Yangi parol')).toHaveValue('');
      expect(within(card).getByLabelText('Yangi parolni takrorlang')).toHaveValue('');
      const state = useAuthStore.getState();
      expect(state.status).toBe('authenticated');
      expect(state.accessToken).toBe(accessToken);
      expect(state.refreshToken).toBe(refreshToken);
    });

    it("noto'g'ri joriy parol → 400 errors.CurrentPassword maydon ostida", async () => {
      const user = userEvent.setup();
      renderSecurity();
      const card = passwordCard();
      await user.type(within(card).getByLabelText('Joriy parol'), 'notogri123');
      await user.type(within(card).getByLabelText('Yangi parol'), 'yangiParol2026');
      await user.type(within(card).getByLabelText('Yangi parolni takrorlang'), 'yangiParol2026');
      await user.click(within(card).getByRole('button', { name: 'Parolni almashtirish' }));

      expect(await within(card).findByText("Joriy parol noto'g'ri.")).toBeInTheDocument();
      expect(within(card).getByLabelText('Joriy parol')).toHaveAttribute('aria-invalid', 'true');
      expect(within(card).queryByText('Parol almashtirildi.')).not.toBeInTheDocument();
    });
  });

  describe('Loginni almashtirish', () => {
    it("joriy login ko'rsatiladi; bo'sh login → ✓ va Saqlash yoqiladi", async () => {
      const user = userEvent.setup();
      renderSecurity();
      const card = loginCard();
      expect(within(card).getByLabelText('Joriy login')).toHaveValue(admin.hemisId);
      const save = within(card).getByRole('button', { name: 'Saqlash' });
      expect(save).toBeDisabled();

      await user.type(within(card).getByLabelText('Yangi login'), '555555');
      expect(within(card).getByText('Tekshirilmoqda…')).toBeInTheDocument();
      expect(save).toBeDisabled();
      expect(await within(card).findByText("✓ Login bo'sh")).toBeInTheDocument();
      expect(save).toBeEnabled();
    });

    it("normalized farq qilsa — '… sifatida saqlanadi'", async () => {
      const user = userEvent.setup();
      server.use(
        http.get(ACCOUNT_ENDPOINTS.loginAvailable, () =>
          HttpResponse.json({ available: true, normalized: '0555555', reason: null }),
        ),
      );
      renderSecurity();
      const card = loginCard();
      await user.type(within(card).getByLabelText('Yangi login'), '555555');
      expect(await within(card).findByText(/sifatida saqlanadi/)).toHaveTextContent(
        "✓ Login bo'sh — 0555555 sifatida saqlanadi",
      );
    });

    it("band login → ✗ sabab, Saqlash o'chiq", async () => {
      const user = userEvent.setup();
      renderSecurity();
      const card = loginCard();
      await user.type(within(card).getByLabelText('Yangi login'), '100000000002');
      expect(await within(card).findByText(`✗ ${LOGIN_TAKEN}`)).toBeInTheDocument();
      expect(within(card).getByRole('button', { name: 'Saqlash' })).toBeDisabled();
    });

    it("noto'g'ri format → ✗ reason (200 + reason), Saqlash o'chiq", async () => {
      const user = userEvent.setup();
      renderSecurity();
      const card = loginCard();
      await user.type(within(card).getByLabelText('Yangi login'), '12ab');
      expect(await within(card).findByText(`✗ ${LOGIN_FORMAT}`)).toBeInTheDocument();
      expect(within(card).getByRole('button', { name: 'Saqlash' })).toBeDisabled();
    });

    it("o'zining joriy logini → so'rovsiz ✗, Saqlash o'chiq", async () => {
      const user = userEvent.setup();
      const logins = captureAvailability();
      renderSecurity();
      const card = loginCard();
      await user.type(within(card).getByLabelText('Yangi login'), admin.hemisId!);
      expect(within(card).getByText('✗ Bu sizning joriy loginingiz.')).toBeInTheDocument();
      await act(() => new Promise((r) => setTimeout(r, 500)));
      expect(logins).toEqual([]);
      expect(within(card).getByRole('button', { name: 'Saqlash' })).toBeDisabled();
    });

    it("debounce: tez yozilganda faqat oxirgi qiymat so'raladi", async () => {
      const user = userEvent.setup();
      const logins = captureAvailability();
      renderSecurity();
      const card = loginCard();
      await user.type(within(card).getByLabelText('Yangi login'), '7777777');
      expect(await within(card).findByText("✓ Login bo'sh")).toBeInTheDocument();
      expect(logins).toEqual(['7777777']);
    });

    it("409 → maydon ostida 'band' xabari, availability qayta so'raladi, Saqlash o'chiq", async () => {
      const user = userEvent.setup();
      const logins: string[] = [];
      let taken = false;
      server.use(
        http.get(ACCOUNT_ENDPOINTS.loginAvailable, ({ request }) => {
          const login = new URL(request.url).searchParams.get('login') ?? '';
          logins.push(login);
          return HttpResponse.json(
            taken
              ? { available: false, normalized: login, reason: LOGIN_TAKEN }
              : { available: true, normalized: login, reason: null },
          );
        }),
        http.post(ACCOUNT_ENDPOINTS.changeLogin, () => {
          taken = true;
          return problemResponse(409, 'Konflikt', LOGIN_TAKEN);
        }),
      );
      renderSecurity();
      const card = loginCard();
      await user.type(within(card).getByLabelText('Yangi login'), '888888');
      await within(card).findByText("✓ Login bo'sh");
      await user.type(within(card).getByLabelText('Joriy parol'), ADMIN_PASSWORD);
      await user.click(within(card).getByRole('button', { name: 'Saqlash' }));

      expect(await within(card).findByRole('alert')).toHaveTextContent(LOGIN_TAKEN);
      expect(within(card).getByLabelText('Yangi login')).toHaveAttribute('aria-invalid', 'true');
      await waitFor(() => expect(logins).toEqual(['888888', '888888']));
      expect(within(card).getByRole('button', { name: 'Saqlash' })).toBeDisabled();
    });

    it("noto'g'ri joriy parol → 400 errors.CurrentPassword", async () => {
      const user = userEvent.setup();
      renderSecurity();
      const card = loginCard();
      await user.type(within(card).getByLabelText('Yangi login'), '999999');
      await within(card).findByText("✓ Login bo'sh");
      await user.type(within(card).getByLabelText('Joriy parol'), 'notogri123');
      await user.click(within(card).getByRole('button', { name: 'Saqlash' }));
      expect(await within(card).findByText("Joriy parol noto'g'ri.")).toBeInTheDocument();
      expect(within(card).getByLabelText('Joriy parol')).toHaveAttribute('aria-invalid', 'true');
      expect(admin.hemisId).toBe('100000000001');
    });

    it("joriy parolsiz saqlash → mijoz xatosi, so'rov yuborilmaydi", async () => {
      const user = userEvent.setup();
      let calls = 0;
      server.use(
        http.post(ACCOUNT_ENDPOINTS.changeLogin, () => {
          calls += 1;
          return undefined;
        }),
      );
      renderSecurity();
      const card = loginCard();
      await user.type(within(card).getByLabelText('Yangi login'), '999999');
      await within(card).findByText("✓ Login bo'sh");
      await user.click(within(card).getByRole('button', { name: 'Saqlash' }));
      expect(within(card).getByText('Joriy parolni kiriting.')).toBeInTheDocument();
      expect(calls).toBe(0);
    });

    it('muvaffaqiyat → joriy login va auth store yangilanadi, forma tozalanadi', async () => {
      const user = userEvent.setup();
      renderSecurity();
      const card = loginCard();
      await user.type(within(card).getByLabelText('Yangi login'), '123123123');
      await within(card).findByText("✓ Login bo'sh");
      await user.type(within(card).getByLabelText('Joriy parol'), ADMIN_PASSWORD);
      await user.click(within(card).getByRole('button', { name: 'Saqlash' }));

      expect(
        await within(card).findByText(
          'Login almashtirildi. Keyingi kirishda yangi logindan foydalaning.',
        ),
      ).toBeInTheDocument();
      expect(within(card).getByLabelText('Joriy login')).toHaveValue('123123123');
      expect(within(card).getByLabelText('Yangi login')).toHaveValue('');
      expect(within(card).getByLabelText('Joriy parol')).toHaveValue('');
      expect(useAuthStore.getState().user?.hemisId).toBe('123123123');
      expect(useAuthStore.getState().status).toBe('authenticated');
    });
  });
});
