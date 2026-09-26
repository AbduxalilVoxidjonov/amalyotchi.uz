import { act, fireEvent, screen, waitFor, within } from '@testing-library/react';
import { http, HttpResponse } from 'msw';
import {
  mockAutumnPractice,
  mockProfile,
  mockProfileNoPractice,
  mockProfileSinglePractice,
  mockSummerPractice,
} from '@/features/profile/mocks';
import { server } from '@/mocks/server';
import { useSessionFlags } from '@/shared/auth/session';
import { useAuthStore } from '@/shared/auth/store';
import { renderApp } from '@/test/render-app';

function profileSection(name: string) {
  return screen.getByRole('region', { name });
}

/** "Amaliyot xulosasi" bo'limidagi davr kartalari (ekrandagi tartibda). */
function periodCards() {
  return within(profileSection('Amaliyot xulosasi')).getAllByRole('article');
}

function mockProfileResponse(body: Record<string, unknown>) {
  server.use(http.get('/api/student/profile', () => HttpResponse.json(body)));
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

    const cards = periodCards();
    expect(cards).toHaveLength(2);
    const autumn = within(profileSection('Amaliyot xulosasi')).getByRole('article', {
      name: 'Kuzgi amaliyot 2026',
    });
    expect(autumn).toHaveAttribute('data-active', 'true');
    expect(autumn).toHaveTextContent('Faol');
    expect(autumn).toHaveTextContent('01.10–15.11.2026');
    expect(autumn).toHaveTextContent('Tech Solutions MChJ');
    expect(autumn).toHaveTextContent("Buyuk Ipak Yo'li 24");
    expect(within(autumn).getByRole('progressbar', { name: 'Davomat' })).toHaveAttribute(
      'aria-valuenow',
      '88',
    );
    expect(autumn).toHaveTextContent('Davomat88%');
    expect(autumn).toHaveTextContent('Ish kunlari17');
    expect(autumn).toHaveTextContent('Joriy ball62,5');
    expect(autumn).toHaveTextContent('Baho: 4 (joriy)');
    expect(autumn).toHaveTextContent('Shubhali kunlar: 1');
    expect(within(autumn).getByText('Joriy hisob')).toBeInTheDocument();

    const account = profileSection('Hisob');
    expect(account).toHaveTextContent("Bog'langan");
    expect(account).not.toHaveTextContent('Bot tayyor bo');
  });

  it('ikki davr: faol birinchi (ajralgan), yopilgan — yakuniy ball/baho, "Joriy hisob" yo\'q', async () => {
    renderApp('/profil');
    await screen.findByRole('heading', { name: 'Aliyev Akmal' });
    expect(profileSection('Amaliyot xulosasi')).toHaveTextContent('2 ta davr');

    const [first, second] = periodCards() as [HTMLElement, HTMLElement];
    expect(first).toHaveAccessibleName('Kuzgi amaliyot 2026');
    expect(second).toHaveAccessibleName('Yozgi amaliyot 2026');
    expect(second).not.toHaveAttribute('data-active');

    expect(second).toHaveTextContent('Yakunlangan');
    expect(second).toHaveTextContent('01.06–11.07.2026');
    expect(second).toHaveTextContent('Digital Soft MChJ');
    expect(second).toHaveTextContent('Davomat94%');
    expect(second).toHaveTextContent('Yakuniy ball91,0');
    expect(second).toHaveTextContent('Baho: 5');
    expect(second).not.toHaveTextContent('(joriy)');
    expect(within(second).queryByText('Joriy hisob')).not.toBeInTheDocument();
    expect(within(second).queryByRole('progressbar')).not.toBeInTheDocument();
  });

  it("server tartibidan qat'i nazar: faol davr birinchi, keyin startDate kamayish tartibida", async () => {
    const spring = {
      ...mockSummerPractice,
      period: {
        id: 'cccccccc-0000-4000-8000-000000000002',
        name: 'Bahorgi amaliyot 2027',
        status: 'planned' as const,
        startDate: '2027-02-01',
        endDate: '2027-03-15',
      },
      company: null,
      elapsedWorkDays: 0,
      attendancePct: 0,
      total: 0,
      grade: null,
      finalized: false,
    };
    mockProfileResponse({
      ...mockProfile,
      practices: [mockSummerPractice, spring, mockAutumnPractice],
    });
    renderApp('/profil');
    await screen.findByRole('heading', { name: 'Aliyev Akmal' });

    const names = periodCards().map((c) => within(c).getByRole('heading').textContent);
    expect(names).toEqual(['Kuzgi amaliyot 2026', 'Bahorgi amaliyot 2027', 'Yozgi amaliyot 2026']);

    const planned = periodCards()[1]!;
    expect(planned).toHaveTextContent('Rejada');
    expect(planned).toHaveTextContent('Korxona biriktirilmagan');
    expect(planned).toHaveTextContent('Davr hali boshlanmagan.');
    expect(planned).not.toHaveTextContent('Baho:');
    expect(within(planned).queryByRole('progressbar')).not.toBeInTheDocument();
  });

  it('bitta davr ham karta ko\'rinishida; korxonasiz davr → "Korxona biriktirilmagan"', async () => {
    mockProfileResponse({
      ...mockProfileSinglePractice,
      practices: [{ ...mockAutumnPractice, company: null }],
    });
    renderApp('/profil');
    await screen.findByRole('heading', { name: 'Aliyev Akmal' });

    const section = profileSection('Amaliyot xulosasi');
    expect(section).not.toHaveTextContent('ta davr');
    const cards = periodCards();
    expect(cards).toHaveLength(1);
    expect(cards[0]).toHaveAccessibleName('Kuzgi amaliyot 2026');
    expect(cards[0]).toHaveAttribute('data-active', 'true');
    expect(cards[0]).toHaveTextContent('Korxona biriktirilmagan');
    expect(cards[0]).not.toHaveTextContent('Tech Solutions MChJ');
  });

  it("eski server (practices yo'q) → sukut `practice` yagona davr sifatida", async () => {
    const legacy: Record<string, unknown> = { ...mockProfile };
    delete legacy.practices;
    mockProfileResponse(legacy);
    renderApp('/profil');
    await screen.findByRole('heading', { name: 'Aliyev Akmal' });

    const cards = periodCards();
    expect(cards).toHaveLength(1);
    expect(cards[0]).toHaveAccessibleName('Kuzgi amaliyot 2026');
    expect(cards[0]).toHaveTextContent('Tech Solutions MChJ');
  });

  it("eski server: practices yo'q va practice null → bo'sh holat", async () => {
    const legacy: Record<string, unknown> = { ...mockProfileNoPractice };
    delete legacy.practices;
    mockProfileResponse(legacy);
    renderApp('/profil');
    await screen.findByRole('heading', { name: 'Karimova Dilnoza' });
    expect(profileSection('Amaliyot xulosasi')).toHaveTextContent(
      'Amaliyot davri biriktirilmagan.',
    );
  });

  it("davr yo'q, tyutor yo'q, Telegram bog'lanmagan", async () => {
    server.use(http.get('/api/student/profile', () => HttpResponse.json(mockProfileNoPractice)));
    renderApp('/profil');
    await screen.findByRole('heading', { name: 'Karimova Dilnoza' });

    const personal = profileSection('Karimova Dilnoza');
    expect(personal).toHaveTextContent('TyutorBiriktirilmagan');
    expect(personal).toHaveTextContent("Ko'rsatilmagan");
    expect(within(personal).queryByRole('link')).not.toBeInTheDocument();

    const practice = profileSection('Amaliyot xulosasi');
    expect(practice).toHaveTextContent('Amaliyot davri biriktirilmagan.');
    expect(within(practice).queryByRole('article')).not.toBeInTheDocument();
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
