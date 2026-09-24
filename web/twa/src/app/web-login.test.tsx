import { act, fireEvent, screen, waitFor } from '@testing-library/react';
import { http, HttpResponse } from 'msw';
import { server } from '@/mocks/server';
import { useSessionFlags } from '@/shared/auth/session';
import { useAuthStore } from '@/shared/auth/store';
import { renderApp } from '@/test/render-app';

async function submitLogin(hemisId: string, password: string) {
  await screen.findByRole('heading', { name: 'Tizimga kirish' });
  fireEvent.change(screen.getByLabelText('HEMIS ID'), { target: { value: hemisId } });
  fireEvent.change(screen.getByLabelText('Parol'), { target: { value: password } });
  fireEvent.click(screen.getByRole('button', { name: 'Kirish' }));
}

function fillChangePassword(current: string, next: string, confirm = next) {
  fireEvent.change(screen.getByLabelText('Joriy parol'), { target: { value: current } });
  fireEvent.change(screen.getByLabelText('Yangi parol'), { target: { value: next } });
  fireEvent.change(screen.getByLabelText('Yangi parolni takrorlang'), {
    target: { value: confirm },
  });
}

describe('Web login (Telegram tashqarisida)', () => {
  it("muvaffaqiyatli kirish → so'ralgan sahifa shell ichida ochiladi", async () => {
    const telegram = vi.fn();
    server.use(
      http.post('/api/auth/telegram', () => {
        telegram();
        return HttpResponse.json({}, { status: 500 });
      }),
    );
    renderApp('/kundalik', { initData: '' });
    await submitLogin('341030', 'talaba12345');

    expect(
      await screen.findByRole('heading', { name: 'Kundaligim', level: 1 }),
    ).toBeInTheDocument();
    expect(useAuthStore.getState().status).toBe('authenticated');
    expect(useAuthStore.getState().user?.hemisId).toBe('341030');
    expect(telegram).not.toHaveBeenCalled(); // web rejimida Telegram-login chaqirilmaydi
  });

  it("403 (noto'g'ri parol, kontrakt v3.8) → backend `detail` ko'rsatiladi, sessiya yo'q", async () => {
    renderApp('/', { initData: '' });
    await submitLogin('341030', 'notogri-parol');

    expect(await screen.findByRole('alert')).toHaveTextContent("HEMIS ID yoki parol noto'g'ri.");
    expect(useAuthStore.getState().status).toBe('anonymous');
  });

  it("401 body'siz → umumiy tushunarli xabar", async () => {
    server.use(http.post('/api/auth/login', () => new HttpResponse(null, { status: 401 })));
    renderApp('/', { initData: '' });
    await submitLogin('341030', 'x');
    expect(await screen.findByRole('alert')).toHaveTextContent("HEMIS ID yoki parol noto'g'ri.");
  });

  it('403 faol emas → formada `detail`, Telegram "bog\'lanmagan" ekrani bilan adashtirilmaydi', async () => {
    server.use(
      http.post('/api/auth/login', () =>
        HttpResponse.json(
          {
            status: 403,
            title: "Ruxsat yo'q",
            detail: 'Hisobingiz faol emas. Tyutoringizga murojaat qiling.',
          },
          { status: 403, headers: { 'Content-Type': 'application/problem+json' } },
        ),
      ),
    );
    renderApp('/', { initData: '' });
    await submitLogin('341030', 'talaba12345');
    expect(await screen.findByRole('alert')).toHaveTextContent(
      'Hisobingiz faol emas. Tyutoringizga murojaat qiling.',
    );
    expect(screen.getByRole('heading', { name: 'Tizimga kirish' })).toBeInTheDocument();
    expect(
      screen.queryByRole('heading', { name: "Telegram hisobingiz bog'lanmagan" }),
    ).not.toBeInTheDocument();
  });

  it('backend 400 errors.Password (PascalCase) → maydon ostida', async () => {
    server.use(
      http.post('/api/auth/login', () =>
        HttpResponse.json(
          {
            status: 400,
            title: "Ma'lumotlar noto'g'ri",
            errors: { Password: ['Parol juda qisqa.'] },
          },
          { status: 400, headers: { 'Content-Type': 'application/problem+json' } },
        ),
      ),
    );
    renderApp('/', { initData: '' });
    await submitLogin('341030', 'qisqa');
    expect(await screen.findByText('Parol juda qisqa.')).toBeInTheDocument();
    expect(screen.queryByText("Ma'lumotlar noto'g'ri")).not.toBeInTheDocument();
  });

  it("klient validatsiyasi: bo'sh / raqam bo'lmagan HEMIS ID — so'rov yuborilmaydi", async () => {
    const login = vi.fn();
    server.use(
      http.post('/api/auth/login', () => {
        login();
        return new HttpResponse(null, { status: 401 });
      }),
    );
    renderApp('/', { initData: '' });
    await submitLogin('abc', '');
    expect(
      await screen.findByText("HEMIS ID faqat raqamlardan iborat bo'lishi kerak (5–20 ta)."),
    ).toBeInTheDocument();
    expect(screen.getByText('Parolni kiriting.')).toBeInTheDocument();
    expect(login).not.toHaveBeenCalled();
  });

  it('xodim (tyutor) hisobi → rad etiladi, sessiya saqlanmaydi, server sessiyasi bekor qilinadi', async () => {
    const logout = vi.fn();
    server.use(
      http.post('/api/auth/logout', ({ request }) => {
        logout(request.headers.get('authorization'));
        return new HttpResponse(null, { status: 204 });
      }),
    );
    renderApp('/', { initData: '' });
    await submitLogin('100000000002', 'tyutor123');

    expect(await screen.findByRole('alert')).toHaveTextContent(
      "Bu ilova faqat talabalar uchun. Xodimlar dashboard'dan kiradi.",
    );
    const state = useAuthStore.getState();
    expect(state.status).toBe('anonymous');
    expect(state.accessToken).toBeNull();
    expect(state.refreshToken).toBeNull();
    expect(window.sessionStorage.getItem('amaliyotchi.twa.refreshToken')).toBeNull();
    await waitFor(() => expect(logout).toHaveBeenCalledWith(expect.stringMatching(/^Bearer /)));
    expect(screen.getByRole('heading', { name: 'Tizimga kirish' })).toBeInTheDocument();
  });

  it("refresh token bor (sahifa yangilandi) → login so'ralmaydi, sessiya tiklanadi", async () => {
    renderApp('/', { initData: '' });
    await submitLogin('341030', 'talaba12345');
    await screen.findByRole('heading', { name: 'Bosh ekran', level: 1 });
    const refreshToken = useAuthStore.getState().refreshToken!;

    // Sahifa yangilanishini taqlid qilish: xotira tozalanadi, refresh token saqlanib qoladi.
    act(() => {
      useAuthStore.setState({ status: 'restoring', accessToken: null, user: null, refreshToken });
    });
    expect(
      await screen.findByRole('heading', { name: 'Bosh ekran', level: 1 }),
    ).toBeInTheDocument();
    await waitFor(() => expect(useAuthStore.getState().status).toBe('authenticated'));
    expect(useAuthStore.getState().refreshToken).not.toBe(refreshToken); // rotatsiya
  });
});

describe("Majburiy parol o'zgartirish (mustChangePassword)", () => {
  it("vaqtinchalik parol → ilovaga o'tkazilmaydi; yangi parol → ilova ochiladi", async () => {
    renderApp('/', { initData: '' });
    await submitLogin('341031', 'vaqtincha1');

    expect(
      await screen.findByRole('heading', { name: "Yangi parol o'rnating" }),
    ).toBeInTheDocument();
    expect(screen.queryByRole('navigation', { name: "Bo'limlar" })).not.toBeInTheDocument();
    expect(useSessionFlags.getState().mustChangePassword).toBe(true);

    // Klient validatsiyasi: takror mos emas.
    fillChangePassword('vaqtincha1', 'yangiParol1', 'boshqa123');
    fireEvent.click(screen.getByRole('button', { name: 'Parolni saqlash va davom etish' }));
    expect(await screen.findByText('Parollar mos kelmadi.')).toBeInTheDocument();

    // Server 400: joriy parol noto'g'ri → maydon ostida.
    fillChangePassword('xato-parol', 'yangiParol1');
    fireEvent.click(screen.getByRole('button', { name: 'Parolni saqlash va davom etish' }));
    expect(await screen.findByText("Joriy parol noto'g'ri.")).toBeInTheDocument();

    const refreshToken = useAuthStore.getState().refreshToken;
    fillChangePassword('vaqtincha1', 'yangiParol1');
    fireEvent.click(screen.getByRole('button', { name: 'Parolni saqlash va davom etish' }));
    expect(
      await screen.findByRole('heading', { name: 'Bosh ekran', level: 1 }),
    ).toBeInTheDocument();
    expect(useSessionFlags.getState().mustChangePassword).toBe(false);
    // Joriy refresh token yuborilgan → backend uni saqlaydi (boshqa sessiyalar bekor).
    const { mockSessions } = await import('@/mocks/data');
    expect(mockSessions.has(refreshToken!)).toBe(true);
  });

  it('PascalCase validatsiya kalitlari (errors.NewPassword) ham maydon ostida', async () => {
    server.use(
      http.post('/api/auth/change-password', () =>
        HttpResponse.json(
          {
            status: 400,
            title: "Ma'lumotlar noto'g'ri",
            errors: { NewPassword: ['Parol juda oddiy.'] },
          },
          { status: 400, headers: { 'Content-Type': 'application/problem+json' } },
        ),
      ),
    );
    renderApp('/', { initData: '' });
    await submitLogin('341031', 'vaqtincha1');
    await screen.findByRole('heading', { name: "Yangi parol o'rnating" });
    fillChangePassword('vaqtincha1', '12345678');
    fireEvent.click(screen.getByRole('button', { name: 'Parolni saqlash va davom etish' }));
    expect(await screen.findByText('Parol juda oddiy.')).toBeInTheDocument();
    expect(useSessionFlags.getState().mustChangePassword).toBe(true);
  });

  it('Telegram-login javobida mustChangePassword → ham parol ekrani', async () => {
    server.use(
      http.post('/api/auth/telegram', async () => {
        const { issueSession, mockStudent } = await import('@/mocks/data');
        return HttpResponse.json({ ...issueSession(mockStudent), mustChangePassword: true });
      }),
    );
    renderApp('/', { initData: 'valid' });
    expect(
      await screen.findByRole('heading', { name: "Yangi parol o'rnating" }),
    ).toBeInTheDocument();
  });
});

describe('Telegram ichida', () => {
  it("avtomatik kirish o'zgarmagan: initData → /api/auth/telegram, login sahifasi yo'q", async () => {
    const login = vi.fn();
    server.use(
      http.post('/api/auth/login', () => {
        login();
        return new HttpResponse(null, { status: 401 });
      }),
    );
    renderApp('/', { initData: 'query_id=abc&user=%7B%22id%22%3A1%7D&hash=x' });
    expect(
      await screen.findByRole('heading', { name: 'Bosh ekran', level: 1 }),
    ).toBeInTheDocument();
    expect(screen.queryByRole('heading', { name: 'Tizimga kirish' })).not.toBeInTheDocument();
    expect(login).not.toHaveBeenCalled();
  });
});
