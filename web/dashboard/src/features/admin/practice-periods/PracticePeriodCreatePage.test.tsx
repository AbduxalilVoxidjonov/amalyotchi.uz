import { act, fireEvent, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { delay, http, HttpResponse } from 'msw';
import { server } from '@/mocks/server';
import { SETTINGS_ENDPOINT } from '../settings/api';
import { mockSettings } from '../settings/mocks';
import { renderHierarchyPage } from '../shared/renderHierarchyPage';
import { PRACTICE_PERIODS_ENDPOINT } from './api';
import { resetPracticePeriodsMock } from './mocks';
import { PracticePeriodCreatePage } from './PracticePeriodCreatePage';

type User = ReturnType<typeof userEvent.setup>;

function renderPage() {
  return renderHierarchyPage(<PracticePeriodCreatePage />, '/admin/practice-periods/new', [
    '/admin/practice-periods/new',
  ]);
}

function setDates(start: string, end: string) {
  fireEvent.change(screen.getByLabelText('Boshlanish sanasi'), { target: { value: start } });
  fireEvent.change(screen.getByLabelText('Tugash sanasi'), { target: { value: end } });
}

/** Kaskad: Fakultet → Kafedra → Yo'nalish (har bosqich serverdan kelishini kutadi). */
async function pickDirection(user: User, faculty: string, department: string, direction: string) {
  await waitFor(() => expect(screen.getByLabelText('Fakultet')).toBeEnabled());
  await user.selectOptions(screen.getByLabelText('Fakultet'), faculty);
  await waitFor(() => expect(screen.getByRole('option', { name: department })).toBeInTheDocument());
  await user.selectOptions(screen.getByLabelText('Kafedra'), department);
  await waitFor(() => expect(screen.getByRole('option', { name: direction })).toBeInTheDocument());
  await user.selectOptions(screen.getByLabelText("Yo'nalish"), direction);
  await screen.findByRole('group', { name: `${direction} guruhlari` });
}

function selectedPanel() {
  return within(screen.getByRole('region', { name: 'Tanlangan guruhlar' }));
}

describe('PracticePeriodCreatePage', () => {
  afterEach(() => resetPracticePeriodsMock());

  it('kaskad: yuqori daraja o‘zgarsa pastkilari tozalanadi', async () => {
    const user = userEvent.setup();
    renderPage();
    await pickDirection(
      user,
      'Axborot texnologiyalari',
      'Kompyuter injiniringi kafedrasi',
      'Kompyuter injiniringi',
    );
    expect(screen.getByRole('checkbox', { name: /412-22/ })).toBeInTheDocument();

    await user.selectOptions(screen.getByLabelText('Fakultet'), 'Iqtisodiyot va moliya');
    expect(screen.getByLabelText('Kafedra')).toHaveValue('');
    expect(screen.getByLabelText("Yo'nalish")).toHaveValue('');
    expect(screen.getByLabelText("Yo'nalish")).toBeDisabled();
    expect(screen.queryByRole('checkbox', { name: /412-22/ })).not.toBeInTheDocument();
    // Faol bo'lmagan kafedra (d4) ro'yxatda yo'q.
    await screen.findByRole('option', { name: 'Moliya kafedrasi' });
    expect(screen.queryByRole('option', { name: 'Iqtisodiyot kafedrasi' })).not.toBeInTheDocument();
  });

  it('tanlov turli yo‘nalish va fakultetlar bo‘ylab saqlanadi; chip × va tozalash', async () => {
    const user = userEvent.setup();
    renderPage();
    setDates('2026-11-02', '2026-12-15');
    expect(screen.getByText('≈ 44 kalendar kun')).toBeInTheDocument();

    await pickDirection(
      user,
      'Axborot texnologiyalari',
      'Kompyuter injiniringi kafedrasi',
      'Kompyuter injiniringi',
    );
    // p1 bilan kesishmaydi — tanlash mumkin, lekin info ko'rsatiladi.
    const g2 = screen.getByRole('checkbox', { name: /413-22/ });
    expect(g2).toBeEnabled();
    expect(g2).toHaveAccessibleDescription(/Boshqa davrda: Ishlab chiqarish amaliyoti 2026/);
    await user.click(g2);

    await user.selectOptions(screen.getByLabelText("Yo'nalish"), 'Dasturiy injiniring');
    await user.click(await screen.findByRole('checkbox', { name: /421-23/ }));

    await pickDirection(user, 'Iqtisodiyot va moliya', 'Moliya kafedrasi', 'Bank ishi');
    await user.click(screen.getByRole('checkbox', { name: /221-23/ }));

    const panel = selectedPanel();
    expect(panel.getByRole('heading', { name: /Tanlangan guruhlar \(3\)/ })).toBeInTheDocument();
    expect(panel.getByText('Axborot texnologiyalari › Kompyuter injiniringi')).toBeInTheDocument();
    expect(panel.getByText('Axborot texnologiyalari › Dasturiy injiniring')).toBeInTheDocument();
    expect(panel.getByText('Iqtisodiyot va moliya › Bank ishi')).toBeInTheDocument();

    await user.click(panel.getByRole('button', { name: '413-22 guruhini olib tashlash' }));
    expect(panel.getByRole('heading', { name: /Tanlangan guruhlar \(2\)/ })).toBeInTheDocument();

    await user.click(panel.getByRole('button', { name: 'Hammasini tozalash' }));
    expect(panel.getByRole('heading', { name: /Tanlangan guruhlar \(0\)/ })).toBeInTheDocument();
    expect(screen.getByRole('checkbox', { name: /221-23/ })).not.toBeChecked();
  });

  it('band guruh o‘chiq, "Hammasini tanlash" faqat bo‘shlarini oladi (indeterminate)', async () => {
    const user = userEvent.setup();
    renderPage();
    setDates('2026-09-01', '2026-10-30');
    await pickDirection(
      user,
      'Axborot texnologiyalari',
      'Kompyuter injiniringi kafedrasi',
      'Kompyuter injiniringi',
    );

    const g1 = screen.getByRole('checkbox', { name: /412-22/ });
    expect(g1).toBeDisabled();
    expect(
      screen.getAllByText('Band: Ishlab chiqarish amaliyoti 2026 (31.08 — 14.10)'),
    ).toHaveLength(2);
    // Hamma guruh band — "Hammasini tanlash" ham o'chiq.
    expect(screen.getByRole('checkbox', { name: 'Hammasini tanlash' })).toBeDisabled();

    // Oraliq p1 dan keyinga surilsa — tanlash mumkin; bittasi tanlansa indeterminate.
    setDates('2026-10-15', '2026-11-30');
    expect(g1).toBeEnabled();
    await user.click(g1);
    const all = screen.getByRole('checkbox', { name: 'Hammasini tanlash' });
    expect(all).toBePartiallyChecked();
    await user.click(all);
    expect(all).toBeChecked();
    expect(selectedPanel().getByRole('heading', { name: /\(2\)/ })).toBeInTheDocument();
  });

  it('forma to‘g‘ri va kamida 1 guruh bo‘lmaguncha "Davr yaratish" o‘chiq; tugash < boshlanish xatosi', async () => {
    const user = userEvent.setup();
    renderPage();
    const submit = screen.getByRole('button', { name: 'Davr yaratish' });
    expect(submit).toBeDisabled();

    await user.type(screen.getByLabelText('Nomi'), 'Kuzgi amaliyot');
    setDates('2026-11-10', '2026-11-01');
    expect(
      screen.getByText("Tugash sanasi boshlanish sanasidan oldin bo'lishi mumkin emas."),
    ).toBeInTheDocument();
    expect(submit).toBeDisabled();

    setDates('2026-11-01', '2026-11-30');
    expect(screen.getByText('Kamida bitta guruh tanlang.')).toBeInTheDocument();
    expect(submit).toBeDisabled();
    expect(screen.getByRole('link', { name: 'Sozlamalarni ochish' })).toHaveAttribute(
      'href',
      '/admin/settings',
    );
  });

  it('409 (ustma-ust): server detail forma tepasida ko‘rinadi', async () => {
    const user = userEvent.setup();
    renderPage();
    await user.type(screen.getByLabelText('Nomi'), 'Qishki qo‘shimcha');
    setDates('2027-01-15', '2027-02-01');
    await pickDirection(
      user,
      'Axborot texnologiyalari',
      'Kompyuter injiniringi kafedrasi',
      'Dasturiy injiniring',
    );
    await user.click(screen.getByRole('checkbox', { name: /421-23/ }));
    await user.click(screen.getByRole('button', { name: 'Davr yaratish' }));

    const alert = await screen.findByRole('alert');
    expect(alert).toHaveTextContent("Davrni yaratib bo'lmadi");
    expect(alert).toHaveTextContent(
      'Quyidagi guruhlar shu sanalarda boshqa davrga biriktirilgan: 421-23 (Qishki amaliyot 2027)',
    );
    expect(screen.queryByTestId('location')).not.toBeInTheDocument();
  });

  it('muvaffaqiyat: davr sahifasiga o‘tadi', async () => {
    const user = userEvent.setup();
    renderPage();
    await user.type(screen.getByLabelText('Nomi'), 'Kuzgi amaliyot 2026');
    setDates('2026-11-02', '2026-12-15');
    await pickDirection(user, 'Iqtisodiyot va moliya', 'Moliya kafedrasi', 'Bank ishi');
    await user.click(screen.getByRole('checkbox', { name: /221-23/ }));
    await user.click(screen.getByRole('button', { name: 'Davr yaratish' }));

    expect(await screen.findByTestId('location')).toHaveTextContent(
      /^\/admin\/practice-periods\/p\d+$/,
    );
  });

  describe('ish kunlari va ish vaqti', () => {
    function workDaysGroup() {
      return within(screen.getByRole('group', { name: 'Ish kunlari' }));
    }

    function withGlobalWorkDays(value: string) {
      server.use(
        http.get(SETTINGS_ENDPOINT, () =>
          HttpResponse.json({
            ...mockSettings,
            settings: mockSettings.settings.map((s) =>
              s.key === 'workDays' ? { ...s, value } : s,
            ),
          }),
        ),
      );
    }

    it('standart: 09:00–17:00; ish kunlari global sozlamadan olinadi', async () => {
      withGlobalWorkDays('1,2,3,4,5');
      renderPage();
      expect(screen.getByLabelText('Boshlanishi')).toHaveValue('09:00');
      expect(screen.getByLabelText('Tugashi')).toHaveValue('17:00');
      // Sozlama yuklanguncha — zaxira qiymat (Du–Sh).
      expect(workDaysGroup().getByRole('button', { name: 'Shanba' })).toHaveAttribute(
        'aria-pressed',
        'true',
      );
      await waitFor(() =>
        expect(workDaysGroup().getByRole('button', { name: 'Shanba' })).toHaveAttribute(
          'aria-pressed',
          'false',
        ),
      );
      expect(workDaysGroup().getByRole('button', { name: 'Juma' })).toHaveAttribute(
        'aria-pressed',
        'true',
      );
      expect(workDaysGroup().getByRole('button', { name: 'Yakshanba' })).toHaveAttribute(
        'aria-pressed',
        'false',
      );
    });

    it('sozlama kechikib kelsa, foydalanuvchi tanlovi ustidan yozilmaydi', async () => {
      let release: () => void = () => {};
      const gate = new Promise<void>((r) => (release = r));
      server.use(
        http.get(SETTINGS_ENDPOINT, async () => {
          await gate;
          await delay(0);
          return HttpResponse.json({
            ...mockSettings,
            settings: mockSettings.settings.map((s) =>
              s.key === 'workDays' ? { ...s, value: '1,2,3' } : s,
            ),
          });
        }),
      );
      const user = userEvent.setup();
      renderPage();
      await user.click(workDaysGroup().getByRole('button', { name: 'Yakshanba' }));
      // Sozlama javobi kelib, so'rov holati yangilanishini kutamiz.
      await act(async () => {
        release();
        await new Promise((r) => setTimeout(r, 30));
      });
      expect(workDaysGroup().getByRole('button', { name: 'Yakshanba' })).toHaveAttribute(
        'aria-pressed',
        'true',
      );
      expect(workDaysGroup().getByRole('button', { name: 'Shanba' })).toHaveAttribute(
        'aria-pressed',
        'true',
      );
    });

    it('kun chip’i sichqoncha va klaviatura bilan almashadi; hammasi o‘chsa — xato', async () => {
      const user = userEvent.setup();
      renderPage();
      await waitFor(() =>
        expect(workDaysGroup().getByRole('button', { name: 'Shanba' })).toHaveAttribute(
          'aria-pressed',
          'true',
        ),
      );
      const sunday = workDaysGroup().getByRole('button', { name: 'Yakshanba' });
      await user.click(sunday);
      expect(sunday).toHaveAttribute('aria-pressed', 'true');

      // Klaviatura: fokus + Space / Enter.
      sunday.focus();
      await user.keyboard(' ');
      expect(sunday).toHaveAttribute('aria-pressed', 'false');
      await user.keyboard('{Enter}');
      expect(sunday).toHaveAttribute('aria-pressed', 'true');

      for (const name of [
        'Dushanba',
        'Seshanba',
        'Chorshanba',
        'Payshanba',
        'Juma',
        'Shanba',
        'Yakshanba',
      ]) {
        await user.click(workDaysGroup().getByRole('button', { name }));
      }
      expect(screen.getByText('Kamida bitta ish kunini tanlang.')).toBeInTheDocument();
      expect(screen.getByRole('group', { name: 'Ish kunlari' })).toHaveAccessibleDescription(
        'Kamida bitta ish kunini tanlang.',
      );
    });

    it('vaqt: tugash boshlanishdan oldin bo‘lsa — xato va yuborib bo‘lmaydi', async () => {
      renderPage();
      fireEvent.change(screen.getByLabelText('Tugashi'), { target: { value: '08:30' } });
      expect(
        screen.getByText("Ish tugash vaqti boshlanish vaqtidan keyin bo'lishi kerak."),
      ).toBeInTheDocument();
      expect(screen.getByLabelText('Tugashi')).toHaveAttribute('aria-invalid', 'true');

      fireEvent.change(screen.getByLabelText('Boshlanishi'), { target: { value: '' } });
      expect(screen.getByText('Boshlanish vaqtini kiriting (masalan, 09:00).')).toBeInTheDocument();
      expect(screen.getByRole('button', { name: 'Davr yaratish' })).toBeDisabled();

      fireEvent.change(screen.getByLabelText('Boshlanishi'), { target: { value: '08:00' } });
      expect(
        screen.queryByText("Ish tugash vaqti boshlanish vaqtidan keyin bo'lishi kerak."),
      ).not.toBeInTheDocument();
    });

    it('≈ N ish kuni: sanalar va ish kunlari bo‘yicha', async () => {
      const user = userEvent.setup();
      renderPage();
      await waitFor(() =>
        expect(workDaysGroup().getByRole('button', { name: 'Shanba' })).toHaveAttribute(
          'aria-pressed',
          'true',
        ),
      );
      // 2026-11-02 dushanba … 2026-11-15 yakshanba: Du–Sh → 12.
      setDates('2026-11-02', '2026-11-15');
      expect(screen.getByText('≈ 14 kalendar kun')).toBeInTheDocument();
      expect(screen.getByText('≈ 12 ish kuni')).toBeInTheDocument();
      await user.click(workDaysGroup().getByRole('button', { name: 'Shanba' }));
      expect(screen.getByText('≈ 10 ish kuni')).toBeInTheDocument();
    });

    it('yaratish so‘rovida dailyStart, dailyEnd va workDays yuboriladi; javobda saqlanadi', async () => {
      let sent: Record<string, unknown> | null = null;
      server.use(
        http.post(PRACTICE_PERIODS_ENDPOINT, async ({ request }) => {
          sent = (await request.clone().json()) as Record<string, unknown>;
          // undefined → keyingi (asosiy mock) handler'ga o'tadi.
        }),
      );
      const user = userEvent.setup();
      renderPage();
      await user.type(screen.getByLabelText('Nomi'), 'Kuzgi amaliyot 2026');
      setDates('2026-11-02', '2026-12-15');
      await waitFor(() =>
        expect(workDaysGroup().getByRole('button', { name: 'Shanba' })).toHaveAttribute(
          'aria-pressed',
          'true',
        ),
      );
      await user.click(workDaysGroup().getByRole('button', { name: 'Shanba' }));
      fireEvent.change(screen.getByLabelText('Boshlanishi'), { target: { value: '08:30' } });
      fireEvent.change(screen.getByLabelText('Tugashi'), { target: { value: '16:00' } });
      await pickDirection(user, 'Iqtisodiyot va moliya', 'Moliya kafedrasi', 'Bank ishi');
      await user.click(screen.getByRole('checkbox', { name: /221-23/ }));
      await user.click(screen.getByRole('button', { name: 'Davr yaratish' }));

      expect(await screen.findByTestId('location')).toHaveTextContent(
        /^\/admin\/practice-periods\/p\d+$/,
      );
      expect(sent).toMatchObject({
        name: 'Kuzgi amaliyot 2026',
        startDate: '2026-11-02',
        endDate: '2026-12-15',
        dailyStart: '08:30',
        dailyEnd: '16:00',
        workDays: '1,2,3,4,5',
      });
    });

    it('server 400 maydon xatolari (dailyEnd, workDays) tegishli maydon ostida', async () => {
      server.use(
        http.post(PRACTICE_PERIODS_ENDPOINT, () =>
          HttpResponse.json(
            {
              type: 'about:blank',
              title: "Ma'lumotlar noto'g'ri",
              status: 400,
              detail: "Kiritilgan ma'lumotlarda xatolik bor.",
              errors: {
                // Backend kalitlari PascalCase — `fieldError` capitalize fallback bilan topadi.
                DailyEnd: ['Serverdan: tugash vaqti noto‘g‘ri.'],
                WorkDays: ['Serverdan: ish kunlari noto‘g‘ri.'],
              },
            },
            { status: 400, headers: { 'Content-Type': 'application/problem+json' } },
          ),
        ),
      );
      const user = userEvent.setup();
      renderPage();
      await user.type(screen.getByLabelText('Nomi'), 'Kuzgi amaliyot 2026');
      setDates('2026-11-02', '2026-12-15');
      await pickDirection(user, 'Iqtisodiyot va moliya', 'Moliya kafedrasi', 'Bank ishi');
      await user.click(screen.getByRole('checkbox', { name: /221-23/ }));
      await user.click(screen.getByRole('button', { name: 'Davr yaratish' }));

      expect(await screen.findByLabelText('Tugashi')).toHaveAccessibleDescription(
        'Serverdan: tugash vaqti noto‘g‘ri.',
      );
      expect(screen.getByRole('group', { name: 'Ish kunlari' })).toHaveAccessibleDescription(
        'Serverdan: ish kunlari noto‘g‘ri.',
      );
    });
  });
});
