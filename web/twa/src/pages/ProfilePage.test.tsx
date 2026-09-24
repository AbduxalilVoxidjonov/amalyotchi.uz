import { act, fireEvent, screen, waitFor, within } from '@testing-library/react';
import { http, HttpResponse } from 'msw';
import { mockProfile, mockProfileNoPractice } from '@/features/profile/mocks';
import { server } from '@/mocks/server';
import { useSessionFlags } from '@/shared/auth/session';
import { useAuthStore } from '@/shared/auth/store';
import { renderApp } from '@/test/render-app';

function profileSection(name: string) {
  return screen.getByRole('region', { name });
}

describe('ProfilePage (/profil)', () => {
  it("davr bor: shaxsiy/o'quv ma'lumotlari, tyutor tel: havolasi, amaliyot xulosasi", async () => {
    renderApp('/profil');
    expect(await screen.findByRole('heading', { name: 'Profil', level: 1 })).toBeInTheDocument();
    expect(await screen.findByRole('heading', { name: 'Aliyev Akmal' })).toBeInTheDocument();

    const personal = profileSection('Aliyev Akmal');
    expect(personal).toHaveTextContent('341030');
    expect(personal).toHaveTextContent('+998 90 111 22 33');
    expect(personal).toHaveTextContent('Axborot texnologiyalari fakulteti');
    expect(personal).toHaveTextContent('Dasturiy injiniring kafedrasi');
    expect(personal).toHaveTextContent('412-22');
    expect(personal).toHaveTextContent('3-kurs');
    expect(personal).toHaveTextContent('Saidova Nodira');
    expect(within(personal).getByRole('link', { name: '+998 90 123 45 67' })).toHaveAttribute(
      'href',
      'tel:+998901234567',
    );

    const practice = profileSection('Amaliyot xulosasi');
    expect(practice).toHaveTextContent('Kuzgi amaliyot 2026');
    expect(practice).toHaveTextContent('Faol');
    expect(practice).toHaveTextContent('01.10–15.11.2026');
    expect(practice).toHaveTextContent('Tech Solutions MChJ');
    expect(practice).toHaveTextContent("Buyuk Ipak Yo'li 24");
    expect(within(practice).getByRole('progressbar', { name: 'Davomat' })).toHaveAttribute(
      'aria-valuenow',
      '88',
    );
    expect(practice).toHaveTextContent("O'tgan ish kunlari17");
    expect(practice).toHaveTextContent('Shubhali kunlar1');
    expect(practice).toHaveTextContent('Joriy ball62,5');
    expect(practice).toHaveTextContent('Baho: 4 (joriy)');
    expect(within(practice).getByText('Joriy hisob')).toBeInTheDocument();

    const account = profileSection('Hisob');
    expect(account).toHaveTextContent("Bog'langan");
    expect(account).not.toHaveTextContent('Bot tayyor bo');
  });

  it('yakunlangan davr → "Joriy hisob" belgisi yo\'q, "Yakuniy ball"', async () => {
    server.use(
      http.get('/api/student/profile', () =>
        HttpResponse.json({
          ...mockProfile,
          practice: {
            ...mockProfile.practice!,
            period: { ...mockProfile.practice!.period, status: 'closed' },
            finalized: true,
            grade: 5,
            total: 91,
          },
        }),
      ),
    );
    renderApp('/profil');
    await screen.findByRole('heading', { name: 'Aliyev Akmal' });
    const practice = profileSection('Amaliyot xulosasi');
    expect(practice).toHaveTextContent('Yakunlangan');
    expect(practice).toHaveTextContent('Yakuniy ball91,0');
    expect(practice).toHaveTextContent('Baho: 5');
    expect(within(practice).queryByText('Joriy hisob')).not.toBeInTheDocument();
  });

  it("davr yo'q, tyutor yo'q, Telegram bog'lanmagan", async () => {
    server.use(http.get('/api/student/profile', () => HttpResponse.json(mockProfileNoPractice)));
    renderApp('/profil');
    await screen.findByRole('heading', { name: 'Karimova Dilnoza' });

    const personal = profileSection('Karimova Dilnoza');
    expect(personal).toHaveTextContent('TyutorBiriktirilmagan');
    expect(personal).toHaveTextContent("Ko'rsatilmagan");
    expect(within(personal).queryByRole('link')).not.toBeInTheDocument();

    expect(profileSection('Amaliyot xulosasi')).toHaveTextContent(
      'Amaliyot davri biriktirilmagan.',
    );
    const account = profileSection('Hisob');
    expect(account).toHaveTextContent("Bog'lanmagan");
    expect(account).toHaveTextContent("Bot tayyor bo'lgach Telegram orqali ham kira olasiz.");
  });

  it("parolni o'zgartirish (ixtiyoriy) → forma, 204 → tasdiq xabari", async () => {
    renderApp('/profil');
    await screen.findByRole('heading', { name: 'Aliyev Akmal' });
    fireEvent.click(screen.getByRole('button', { name: "Parolni o'zgartirish" }));

    fireEvent.change(screen.getByLabelText('Joriy parol'), { target: { value: 'talaba12345' } });
    fireEvent.change(screen.getByLabelText('Yangi parol'), { target: { value: 'yangiParol9' } });
    fireEvent.change(screen.getByLabelText('Yangi parolni takrorlang'), {
      target: { value: 'yangiParol9' },
    });
    fireEvent.click(screen.getByRole('button', { name: 'Saqlash' }));

    expect(await screen.findByText("Parol o'zgartirildi.")).toBeInTheDocument();
    expect(screen.queryByLabelText('Joriy parol')).not.toBeInTheDocument();
  });

  it('Chiqish (web) → POST /api/auth/logout, store tozalanadi, login sahifasi', async () => {
    const logout = vi.fn();
    server.use(
      http.post('/api/auth/logout', async ({ request }) => {
        logout(await request.json());
        return new HttpResponse(null, { status: 204 });
      }),
    );
    // Web rejimida kirish (initData yo'q).
    renderApp('/profil', { initData: '' });
    fireEvent.change(await screen.findByLabelText('HEMIS ID'), { target: { value: '341030' } });
    fireEvent.change(screen.getByLabelText('Parol'), { target: { value: 'talaba12345' } });
    fireEvent.click(screen.getByRole('button', { name: 'Kirish' }));
    await screen.findByRole('heading', { name: 'Aliyev Akmal' });
    const refreshToken = useAuthStore.getState().refreshToken;

    fireEvent.click(screen.getByRole('button', { name: 'Chiqish' }));

    expect(await screen.findByRole('heading', { name: 'Tizimga kirish' })).toBeInTheDocument();
    expect(screen.getByText('Hisobdan chiqdingiz.')).toBeInTheDocument();
    expect(logout).toHaveBeenCalledWith({ refreshToken });
    expect(useAuthStore.getState().status).toBe('anonymous');
    expect(useAuthStore.getState().refreshToken).toBeNull();
  });

  it('Chiqish (Telegram ichida) → avtomatik qayta kirilmaydi; "Qayta kirish" bilan kiriladi', async () => {
    const telegram = vi.fn();
    server.use(
      http.post('/api/auth/telegram', async () => {
        telegram();
        const { issueSession, mockStudent } = await import('@/mocks/data');
        return HttpResponse.json(issueSession(mockStudent));
      }),
    );
    renderApp('/profil');
    await screen.findByRole('heading', { name: 'Aliyev Akmal' });
    expect(telegram).toHaveBeenCalledTimes(1);

    fireEvent.click(screen.getByRole('button', { name: 'Chiqish' }));
    expect(await screen.findByRole('heading', { name: 'Hisobdan chiqdingiz' })).toBeInTheDocument();
    expect(useSessionFlags.getState().loggedOut).toBe(true);
    // Bir oz kutamiz — avtomatik login chaqirilmasligi kerak.
    await act(() => new Promise((r) => setTimeout(r, 30)));
    expect(telegram).toHaveBeenCalledTimes(1);
    expect(useAuthStore.getState().status).toBe('anonymous');

    fireEvent.click(screen.getByRole('button', { name: 'Qayta kirish' }));
    await waitFor(() => expect(useAuthStore.getState().status).toBe('authenticated'));
    expect(telegram).toHaveBeenCalledTimes(2);
    expect(
      await screen.findByRole('heading', { name: 'Bosh ekran', level: 1 }),
    ).toBeInTheDocument();
    expect(useSessionFlags.getState().loggedOut).toBe(false);
  });
});
