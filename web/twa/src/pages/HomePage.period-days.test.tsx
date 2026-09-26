import { http, HttpResponse } from 'msw';
import { fireEvent, screen, waitFor, within } from '@testing-library/react';
import { MOCK_SPRING_PERIOD } from '@/features/period/mocks';
import { mockPeriodDays, setPeriodDaysVariant } from '@/features/period-days/mocks';
import { setCheckinQrRequired } from '@/features/today/mocks';
import { server } from '@/mocks/server';
import { removeGeolocation, renderApp, stubGeolocation } from '@/test/render-app';

/**
 * Bosh ekran — amaliyot davri (mock: "Kuzgi amaliyot 2026", 31.08–14.10.2026, bugun 12.10.2026 dushanba;
 * 6 kunlik hafta, 01.09 — "Mustaqillik kuni"). Kunlar accordion: faqat bugungi avtomatik ochiq.
 */
const row = (name: RegExp) => screen.getByRole('button', { name });
const panelOf = (name: RegExp) => within(screen.getByRole('region', { name }));
const TODAY = /^12\.10 · Dushanba/;

async function renderHome() {
  renderApp('/');
  await screen.findByRole('list', { name: 'Amaliyot kunlari' });
}

afterEach(() => removeGeolocation());

describe('Bosh ekran — davr kartasi', () => {
  it('nom, sanalar, holat, o‘tgan ish kunlari', async () => {
    await renderHome();
    const card = within(screen.getByRole('region', { name: 'Kuzgi amaliyot 2026' }));
    expect(card.getByText('31.08.2026 – 14.10.2026')).toBeInTheDocument();
    expect(card.getByText('Davom etmoqda')).toBeInTheDocument();
    expect(card.getByText(/Ish kunlari:/)).toHaveTextContent("Ish kunlari: 34 / 38 o'tdi");
  });

  it("portfolio, yakuniy baho va xulosa bosh ekranda yo'q", async () => {
    await renderHome();
    expect(screen.queryByRole('heading', { name: /Portfolio/ })).not.toBeInTheDocument();
    expect(
      screen.queryByText(/Yakuniy baho|Tyutor xulosasi|Portfolio PDF/),
    ).not.toBeInTheDocument();
  });
});

describe('Bosh ekran — kunlar accordion', () => {
  it('faqat bugun ochiq ("Bugun" belgisi), unga bir marta scroll qilinadi', async () => {
    const scroll = vi.fn();
    Element.prototype.scrollIntoView = scroll;
    try {
      await renderHome();
      const expanded = screen.getAllByRole('button', { expanded: true });
      expect(expanded).toHaveLength(1);
      expect(expanded[0]).toHaveAccessibleName('12.10 · Dushanba, bugun, Kutilmoqda');
      expect(expanded[0]).toHaveAttribute('aria-controls', 'day-2026-10-12-panel');
      // 45 kun — faqat bugungi panel mazmuni render qilingan.
      expect(screen.getAllByRole('button', { expanded: false })).toHaveLength(44);
      expect(screen.getAllByRole('region', { name: /^\d\d\.\d\d · / })).toHaveLength(1);
      expect(
        await panelOf(TODAY).findByRole('link', { name: 'QR orqali belgilash' }),
      ).toBeInTheDocument();

      expect(scroll).toHaveBeenCalledTimes(1);
      expect(scroll).toHaveBeenCalledWith({ block: 'start' });
      expect(scroll.mock.contexts[0]).toBe(row(TODAY).closest('li'));

      // Boshqa davrni tanlab qaytish — qayta scroll yo'q.
      fireEvent.click(screen.getByRole('button', { name: /Bahorgi amaliyot 2027/ }));
      await screen.findByText('Rejalashtirilgan');
      fireEvent.click(screen.getByRole('button', { name: /Kuzgi amaliyot 2026/ }));
      await waitFor(() => expect(row(TODAY)).toHaveAttribute('aria-expanded', 'true'));
      expect(scroll).toHaveBeenCalledTimes(1);
    } finally {
      delete (Element.prototype as Partial<Element>).scrollIntoView;
    }
  });

  it('boshqa kunni ochish/yopish — bir nechtasi bir vaqtda ochiq', async () => {
    await renderHome();
    const day = row(/^24\.09 · Payshanba/);
    expect(day).toHaveAttribute('aria-expanded', 'false');
    expect(screen.queryByText('08:48')).not.toBeInTheDocument();

    fireEvent.click(day);
    expect(day).toHaveAttribute('aria-expanded', 'true');
    expect(panelOf(/^24\.09 · Payshanba/).getByText('08:48')).toBeInTheDocument();
    // Bugungi ham ochiq qoladi.
    expect(row(TODAY)).toHaveAttribute('aria-expanded', 'true');
    expect(screen.getAllByRole('button', { expanded: true })).toHaveLength(2);

    fireEvent.click(day);
    expect(day).toHaveAttribute('aria-expanded', 'false');
    expect(screen.queryByText('08:48')).not.toBeInTheDocument();
  });

  it('holat chip’lari — kalendar yorliqlari bilan', async () => {
    await renderHome();
    expect(within(row(/^31\.08 · Dushanba/)).getByText('Keldi')).toBeInTheDocument();
    expect(within(row(/^05\.09 · Shanba/)).getByText('Kech keldi')).toBeInTheDocument();
    expect(within(row(/^09\.09 · Chorshanba/)).getByText('Kelmadi')).toBeInTheDocument();
    expect(within(row(/^15\.09 · Seshanba/)).getByText('Sababli')).toBeInTheDocument();
    expect(within(row(/^06\.09 · Yakshanba/)).getByText('Dam olish')).toBeInTheDocument();
    expect(within(row(TODAY)).getByText('Kutilmoqda')).toBeInTheDocument();
    expect(within(row(/^13\.10 · Seshanba/)).getByText('Kelgusi kun')).toBeInTheDocument();
    const holiday = row(/^01\.09 · Seshanba/);
    expect(within(holiday).getByText('Mustaqillik kuni')).toBeInTheDocument();
    expect(within(holiday).getByText('Dam olish')).toBeInTheDocument();
    expect(holiday.closest('li')).toHaveAttribute('data-muted', 'true');
    expect(row(/^02\.09/).closest('li')).not.toHaveAttribute('data-muted');
  });

  it("o'tgan kun tafsiloti: vaqtlar, belgilar, kundalik holati", async () => {
    await renderHome();
    // 04.09 — avtomatik yopilgan (ketish — yopilish vaqti), kundalik tyutor ko'rgan.
    fireEvent.click(row(/^04\.09/));
    let p = panelOf(/^04\.09/);
    expect(p.getByText('08:51')).toBeInTheDocument();
    expect(p.getByText('18:00')).toBeInTheDocument();
    expect(p.getByText('Avtomatik yopilgan')).toBeInTheDocument();
    expect(p.getByText("Tyutor ko'rdi")).toBeInTheDocument();

    // 05.09 — kech keldi, kundalik tasdiqlangan + ball.
    fireEvent.click(row(/^05\.09/));
    p = panelOf(/^05\.09/);
    expect(p.getByText('09:24')).toBeInTheDocument();
    expect(p.getByText('17:04')).toBeInTheDocument();
    expect(p.getByText('Kech keldi')).toBeInTheDocument();
    expect(p.getByText('Tasdiqlangan')).toBeInTheDocument();
    expect(p.getByText('5 ball')).toBeInTheDocument();

    fireEvent.click(row(/^07\.09/));
    expect(panelOf(/^07\.09/).getByText('Kundalik yozilmagan')).toBeInTheDocument();
    fireEvent.click(row(/^11\.09/));
    expect(panelOf(/^11\.09/).getByText('Shubhali')).toBeInTheDocument();
    fireEvent.click(row(/^12\.09/));
    expect(panelOf(/^12\.09/).getByText('Qayta yozish kerak')).toBeInTheDocument();
    fireEvent.click(row(/^15\.09/));
    expect(panelOf(/^15\.09/).getByText("Qo'lda tuzatilgan")).toBeInTheDocument();
    fireEvent.click(row(/^10\.10/));
    expect(panelOf(/^10\.10/).getByText('Yuborilgan')).toBeInTheDocument();
  });

  it('kelgusi kun va dam olish/bayram paneli', async () => {
    await renderHome();
    fireEvent.click(row(/^13\.10/));
    expect(panelOf(/^13\.10/).getByText('Kelgusi ish kuni')).toBeInTheDocument();
    fireEvent.click(row(/^01\.09/));
    expect(panelOf(/^01\.09/).getByText('Dam olish kuni — Mustaqillik kuni')).toBeInTheDocument();
    fireEvent.click(row(/^06\.09/));
    expect(panelOf(/^06\.09/).getByText('Dam olish kuni')).toBeInTheDocument();
  });

  it('QR sahifasida belgilangach → bosh ekranda bugungi qator va qisqa holat yangilanadi', async () => {
    stubGeolocation();
    setCheckinQrRequired(false);
    await renderHome();
    fireEvent.click(await panelOf(TODAY).findByRole('link', { name: 'QR orqali belgilash' }));
    fireEvent.click(await screen.findByRole('button', { name: 'Kelganini belgilash' }));
    fireEvent.click(await screen.findByRole('button', { name: 'Rasmsiz davom etish' }));
    expect(await screen.findByText('Kelganingiz belgilandi · 09:02')).toBeInTheDocument();

    fireEvent.click(screen.getByRole('link', { name: 'Bosh ekranga' }));
    await screen.findByRole('list', { name: 'Amaliyot kunlari' });
    expect(await within(row(TODAY)).findByText('Keldi')).toBeInTheDocument();
    expect(await panelOf(TODAY).findByText('Belgilandingiz · 09:02')).toBeInTheDocument();
    // Ketishni belgilash ham QR sahifasida.
    expect(panelOf(TODAY).getByRole('link', { name: 'QR orqali belgilash' })).toBeInTheDocument();
  });

  it('bugungi panelni yopib-ochish — qisqa holat qayta chiziladi', async () => {
    await renderHome();
    await panelOf(TODAY).findByText('Belgilanish oynasi ochiq');
    fireEvent.click(row(TODAY));
    expect(screen.queryByText('Belgilanish oynasi ochiq')).not.toBeInTheDocument();
    fireEvent.click(row(TODAY));
    expect(await panelOf(TODAY).findByText('Belgilanish oynasi ochiq')).toBeInTheDocument();
  });
});

describe('Bosh ekran — tugagan davr yig‘indisi', () => {
  const stat = (label: string) =>
    within(screen.getByRole('region', { name: 'Davr yakuni' })).getByText(label, {
      selector: 'dt',
    }).nextElementSibling?.textContent;

  it('faol (sukut) davr — kunlar ro‘yxati bor, yig‘indi yo‘q (regressiya)', async () => {
    await renderHome();
    expect(screen.getByRole('heading', { name: 'Kunlar' })).toBeInTheDocument();
    expect(screen.queryByRole('region', { name: 'Davr yakuni' })).not.toBeInTheDocument();
  });

  it('yopilgan davr tanlansa — kunlar o‘rniga keldi/kech qoldi/kelmadi; qaytsa — kunlar', async () => {
    const scroll = vi.fn();
    Element.prototype.scrollIntoView = scroll;
    try {
      await renderHome();
      expect(scroll).toHaveBeenCalledTimes(1);
      fireEvent.click(screen.getByRole('button', { name: /Yozgi amaliyot 2026/ }));
      expect(await screen.findByRole('region', { name: 'Davr yakuni' })).toBeInTheDocument();
      expect(screen.getByText('Tugagan')).toBeInTheDocument();
      // Davr kartasida "Ish kunlari: X / Y o'tdi" yo'q — yig'indi yetarli.
      expect(screen.queryByText(/Ish kunlari:/)).not.toBeInTheDocument();
      expect(screen.queryByRole('list', { name: 'Amaliyot kunlari' })).not.toBeInTheDocument();
      expect(screen.queryByRole('heading', { name: 'Kunlar' })).not.toBeInTheDocument();
      expect(
        within(screen.getByRole('main')).queryByRole('link', { name: 'QR orqali belgilash' }),
      ).not.toBeInTheDocument();

      // Mock: 01.06–11.07.2026, 36 ish kuni — 3 kech, 1 kelmadi, 1 sababli, qolgani vaqtida.
      expect(stat('Keldi')).toBe('34');
      expect(stat('Kech qoldi')).toBe('3');
      expect(stat('Kelmadi')).toBe('1');
      expect(screen.getByText(/Sababli:/)).toHaveTextContent('Sababli: 1 kun');
      expect(screen.getByText(/ish kunidan/)).toHaveTextContent('36 ish kunidan 34 kun keldi');
      expect(screen.getByRole('progressbar', { name: 'Davomat 97%' })).toBeInTheDocument();

      fireEvent.click(screen.getByRole('button', { name: /Kuzgi amaliyot 2026/ }));
      expect(await screen.findByRole('list', { name: 'Amaliyot kunlari' })).toBeInTheDocument();
      expect(screen.queryByRole('region', { name: 'Davr yakuni' })).not.toBeInTheDocument();
      expect(screen.getByText(/Ish kunlari:/)).toHaveTextContent("Ish kunlari: 34 / 38 o'tdi");
      await waitFor(() => expect(row(TODAY)).toHaveAttribute('aria-expanded', 'true'));
      expect(scroll).toHaveBeenCalledTimes(1);
    } finally {
      delete (Element.prototype as Partial<Element>).scrollIntoView;
    }
  });

  it('yopilgan, lekin sanasi hali tugamagan davr (production) — yig‘indi, kunlar va scroll yo‘q', async () => {
    const scroll = vi.fn();
    Element.prototype.scrollIntoView = scroll;
    const base = mockPeriodDays(null)!;
    server.use(
      http.get('/api/student/period-days', () =>
        HttpResponse.json({ ...base, period: { ...base.period!, status: 'closed' } }),
      ),
    );
    try {
      renderApp('/');
      expect(await screen.findByRole('region', { name: 'Davr yakuni' })).toBeInTheDocument();
      expect(screen.queryByRole('list', { name: 'Amaliyot kunlari' })).not.toBeInTheDocument();
      expect(screen.queryByText(/Ish kunlari:/)).not.toBeInTheDocument();
      // Bugun (pending) va kelgusi kunlar hisobga kirmaydi.
      expect(stat('Keldi')).toBe('33');
      expect(scroll).not.toHaveBeenCalled();
    } finally {
      delete (Element.prototype as Partial<Element>).scrollIntoView;
    }
  });

  it('faol, lekin endDate o‘tgan davr — yig‘indi', async () => {
    const base = mockPeriodDays(null)!;
    server.use(
      http.get('/api/student/period-days', () =>
        HttpResponse.json({ ...base, today: '2026-10-20' }),
      ),
    );
    renderApp('/');
    expect(await screen.findByRole('region', { name: 'Davr yakuni' })).toBeInTheDocument();
    expect(screen.queryByRole('list', { name: 'Amaliyot kunlari' })).not.toBeInTheDocument();
  });
});

describe('Bosh ekran — davr tanlagichi va bo‘sh holat', () => {
  it('ikki davr: tanlagich; bahorgi tanlansa ?periodId= bilan, bugun davrdan tashqarida — hech biri ochiq emas', async () => {
    const urls: URL[] = [];
    server.events.on('request:start', ({ request }) => {
      const url = new URL(request.url);
      if (url.pathname === '/api/student/period-days') urls.push(url);
    });
    await renderHome();
    const picker = within(screen.getByRole('group', { name: 'Amaliyot davri' }));
    expect(picker.getAllByRole('button')).toHaveLength(3);
    expect(picker.getByRole('button', { name: /Kuzgi amaliyot 2026/ })).toHaveAttribute(
      'aria-pressed',
      'true',
    );
    expect(urls[0]?.searchParams.has('periodId')).toBe(false);

    fireEvent.click(picker.getByRole('button', { name: /Bahorgi amaliyot 2027/ }));
    expect(
      await screen.findByRole('heading', { name: 'Bahorgi amaliyot 2027', level: 2 }),
    ).toBeInTheDocument();
    expect(urls.at(-1)?.searchParams.get('periodId')).toBe(MOCK_SPRING_PERIOD.id);
    expect(screen.getByText('01.02.2027 – 15.03.2027')).toBeInTheDocument();
    expect(screen.getByText('Rejalashtirilgan')).toBeInTheDocument();
    expect(within(row(/^01\.02 · Dushanba/)).getByText('Kelgusi kun')).toBeInTheDocument();
    expect(screen.queryByRole('button', { expanded: true })).not.toBeInTheDocument();
    expect(screen.queryByText('Bugun')).not.toBeInTheDocument();
    server.events.removeAllListeners();
  });

  it('bitta davr → tanlagich yo‘q', async () => {
    setPeriodDaysVariant('single');
    await renderHome();
    expect(screen.queryByRole('group', { name: 'Amaliyot davri' })).not.toBeInTheDocument();
  });

  it('davr biriktirilmagan → bo‘sh holat', async () => {
    setPeriodDaysVariant('none');
    renderApp('/');
    expect(await screen.findByText('Amaliyot davri biriktirilmagan')).toBeInTheDocument();
    expect(screen.queryByRole('list', { name: 'Amaliyot kunlari' })).not.toBeInTheDocument();
  });

  it('server xatosi → qayta urinish', async () => {
    let fail = true;
    server.use(
      http.get('/api/student/period-days', () =>
        fail
          ? HttpResponse.json(
              { status: 500, title: 'Xato', detail: 'Vaqtincha xato.' },
              { status: 500 },
            )
          : HttpResponse.json({ today: '2026-10-12', period: null, periods: [], days: [] }),
      ),
    );
    renderApp('/');
    expect(await screen.findByText('Vaqtincha xato.')).toBeInTheDocument();
    fail = false;
    fireEvent.click(screen.getByRole('button', { name: 'Qayta urinish' }));
    expect(await screen.findByText('Amaliyot davri biriktirilmagan')).toBeInTheDocument();
  });
});
