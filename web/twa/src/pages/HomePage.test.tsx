import { http, HttpResponse } from 'msw';
import { cleanup, fireEvent, screen, within } from '@testing-library/react';
// `mockToday` mock ichida qayta tayinlanadi — joriy qiymat namespace orqali.
import * as todayMocks from '@/features/today/mocks';
import { mockToday, setPeriodGap } from '@/features/today/mocks';
import { setPeriodDaysVariant } from '@/features/period-days/mocks';
import { mockPlace, setMockPlace } from '@/features/place/mocks';
import { server } from '@/mocks/server';
import { renderApp } from '@/test/render-app';

/**
 * Bugungi (12.10.2026, dushanba) kun paneli — accordion'da avtomatik ochiq. Holat yozuvlari
 * ("Kutilmoqda", "Keldi"…) qator chip'larida ham bor — bugungi qisqa holat shu panel ichida tekshiriladi.
 */
const todayPanel = () => within(screen.getByRole('region', { name: /^12\.10 · Dushanba/ }));

/** Kontent (main) ichidagi "QR orqali belgilash" tugmasi — tab-bar'dagi shu nomli tabdan farqli. */
const mainQrLink = () =>
  within(screen.getByRole('main')).queryByRole('link', { name: 'QR orqali belgilash' });

describe('HomePage (isTalaba) — bugungi davomat (qisqa holat, belgilash QR sahifasida)', () => {
  it("oyna ochiq → qisqa holat va 'QR orqali belgilash' → /qr (belgilash oqimi bosh ekranda yo'q)", async () => {
    const router = renderApp('/');

    expect(await screen.findByText('Belgilanish oynasi ochiq')).toBeInTheDocument();
    expect(screen.getByText('Bugun · 12.10.2026')).toBeInTheDocument();
    // To'liq oqim (QR/selfi/joylashuv) faqat QR sahifasida.
    expect(screen.queryByRole('button', { name: /KELDIM|KETDIM|Kelganini belgilash/ })).toBeNull();
    expect(screen.queryByRole('list', { name: 'Belgilanish qadamlari' })).not.toBeInTheDocument();
    expect(screen.queryByLabelText('Selfie olish')).not.toBeInTheDocument();

    const link = todayPanel().getByRole('link', { name: 'QR orqali belgilash' });
    expect(link).not.toHaveAttribute('href');
    fireEvent.click(link);
    expect(
      await screen.findByRole('heading', { name: 'QR orqali belgilash', level: 1 }),
    ).toBeInTheDocument();
    expect(router.state.location.pathname).toBe('/qr');
    expect(await screen.findByRole('button', { name: 'Kelganini belgilash' })).toBeInTheDocument();
  });

  it('kelgan, check-out oynasi ochiq → "Belgilandingiz" + QR tugmasi (ketishni belgilash uchun)', async () => {
    todayMocks.mockToday.checkin = {
      ...todayMocks.mockToday.checkin,
      status: 'present',
      checkInAt: '2026-10-12T03:58:00Z',
    };
    renderApp('/');
    expect(await screen.findByText('Belgilandingiz · 08:58')).toBeInTheDocument();
    expect(todayPanel().getByRole('link', { name: 'QR orqali belgilash' })).toBeInTheDocument();
  });

  it("server holatlari: autoClosed → 'Kun avtomatik yakunlandi'; absent → QR tugmasi yo'q", async () => {
    server.use(
      http.get('/api/student/today', () =>
        HttpResponse.json({
          ...mockToday,
          checkin: {
            ...mockToday.checkin,
            status: 'late',
            checkInAt: '2026-10-12T04:40:00Z',
            autoClosed: true,
            suspicious: true,
          },
          place: null,
        }),
      ),
    );
    renderApp('/');
    expect(await screen.findByText('Kun avtomatik yakunlandi')).toBeInTheDocument();
    expect(todayPanel().getByRole('note')).toHaveTextContent('shubhali');
    expect(todayPanel().queryByRole('link', { name: 'QR orqali belgilash' })).toBeNull();

    server.use(
      http.get('/api/student/today', () =>
        HttpResponse.json({
          ...mockToday,
          window: { ...mockToday.window, isOpen: false },
          checkin: {
            ...mockToday.checkin,
            status: 'absent',
            note: 'Bugungi belgilanish oynasi yopilgan.',
          },
        }),
      ),
    );
    cleanup();
    renderApp('/');
    expect(await screen.findByText('Bugun belgilanmadingiz')).toBeInTheDocument();
    expect(screen.getByText('Bugungi belgilanish oynasi yopilgan.')).toBeInTheDocument();
    expect(todayPanel().queryByRole('link', { name: 'QR orqali belgilash' })).toBeNull();
  });

  it("bugungi kundalik: 'Kundalik yozilmagan' + 'Kundalik yozish' → /kundalik (href'siz)", async () => {
    const router = renderApp('/');
    const panel = within(await screen.findByRole('region', { name: /^12\.10 · Dushanba/ }));
    expect(await panel.findByText('Kundalik yozilmagan')).toBeInTheDocument();
    // Kundalik formasi bosh ekranda yo'q — `/kundalik` da.
    expect(screen.queryByText('Bugungi kundalik')).not.toBeInTheDocument();
    const write = panel.getByRole('link', { name: 'Kundalik yozish' });
    // Telegram rejimi: ichki havola `href`siz (Telegram-Android `<a href>` ni tashqi havola deb ushlaydi).
    expect(write).not.toHaveAttribute('href');
    fireEvent.click(write);
    expect(
      await screen.findByRole('heading', { name: 'Kundaligim', level: 1 }),
    ).toBeInTheDocument();
    expect(router.state.location.pathname).toBe('/kundalik');
  });

  it('bugungi kundalik yuborilgan → holat, yozish tugmasi yo‘q', async () => {
    todayMocks.markDiarySubmitted();
    renderApp('/');
    const panel = within(await screen.findByRole('region', { name: /^12\.10 · Dushanba/ }));
    expect(await panel.findByText('Yuborilgan')).toBeInTheDocument();
    expect(panel.queryByRole('link', { name: 'Kundalik yozish' })).not.toBeInTheDocument();
  });
});

describe("HomePage — ikki davr oralig'i (v3.5 §4.6)", () => {
  it('kelgusi davr: kartochka (nom, sana, qolgan kun) + ariza tugmasi → forma bahorgi davr uchun', async () => {
    setPeriodGap('upcoming');
    renderApp('/');

    // Davr kartasi (sukut — bahorgi) va tanaffus kartasi — ikkalasida ham davr nomi.
    expect(
      (await screen.findAllByRole('heading', { name: 'Bahorgi amaliyot 2027' })).length,
    ).toBeGreaterThan(0);
    expect(screen.getByText('Rejalashtirilgan')).toBeInTheDocument();
    expect(await screen.findByText('01.02.2027 dan boshlanadi')).toBeInTheDocument();
    expect(screen.getByText('43 kun qoldi')).toBeInTheDocument();
    expect(mainQrLink()).toBeNull();
    // Bugun davrdan tashqarida — hech bir kun ochilmaydi, kundalik tugmasi yo'q.
    expect(screen.queryByRole('link', { name: 'Kundalik yozish' })).not.toBeInTheDocument();
    expect(screen.getAllByRole('button', { expanded: false }).length).toBeGreaterThan(0);
    expect(screen.queryByRole('button', { expanded: true })).not.toBeInTheDocument();

    // Bahorgi davrga ariza yo'q (GET place → 404) → "Amaliyot joyini yuborish".
    const link = await screen.findByRole('link', { name: 'Amaliyot joyini yuborish' });
    expect(link.tagName).toBe('BUTTON');
    expect(link).not.toHaveAttribute('href');
    fireEvent.click(link);

    expect(await screen.findByText('Amaliyot joyini tanlash')).toBeInTheDocument();
    expect(screen.getByText('Bahorgi amaliyot 2027 uchun')).toBeInTheDocument();
    fireEvent.change(screen.getByLabelText('Korxona STIR raqami'), {
      target: { value: '305881204' },
    });
    fireEvent.click(screen.getByRole('button', { name: 'Qidirish' }));
    fireEvent.click(await screen.findByRole('button', { name: 'Tasdiqlash va yuborish' }));
    // Ariza kelgusi (bahorgi) davrga tushadi.
    expect(await screen.findByText('01.02–15.03.2027')).toBeInTheDocument();
    expect(screen.getByText('Tekshiruvda')).toBeInTheDocument();
  });

  it("kelgusi davr: ariza allaqachon yuborilgan → tugma yo'q, holat ko'rinadi", async () => {
    setPeriodGap('upcoming');
    setMockPlace({ ...mockPlace, status: 'submitted', contract: null });
    renderApp('/');

    expect(await screen.findByText('Tekshiruvda')).toBeInTheDocument();
    expect(screen.getByRole('link', { name: 'Tech Solutions MChJ' })).not.toHaveAttribute('href');
    expect(
      screen.queryByRole('link', { name: 'Amaliyot joyini yuborish' }),
    ).not.toBeInTheDocument();
  });

  it('faqat tugagan davr: "Amaliyot davri tugagan", portfolio yo‘q, hech bir kun ochiq emas', async () => {
    setPeriodGap('ended');
    renderApp('/');

    expect(
      await screen.findByRole('heading', { name: 'Amaliyot davri tugagan: Kuzgi amaliyot 2026' }),
    ).toBeInTheDocument();
    expect(screen.queryByText(/portfolio/i)).not.toBeInTheDocument();
    expect(screen.getByText('Tugagan')).toBeInTheDocument();
    expect(screen.queryByRole('button', { expanded: true })).not.toBeInTheDocument();
    expect(mainQrLink()).toBeNull();
    expect(screen.queryByText(/kun qoldi/)).not.toBeInTheDocument();
  });

  it("davr biriktirilmagan → bo'sh holat, check-in va kunlar yo'q", async () => {
    setPeriodDaysVariant('none');
    renderApp('/');
    expect(await screen.findByText('Amaliyot davri biriktirilmagan')).toBeInTheDocument();
    expect(mainQrLink()).toBeNull();
    expect(screen.queryByRole('list', { name: 'Amaliyot kunlari' })).not.toBeInTheDocument();
  });
});
