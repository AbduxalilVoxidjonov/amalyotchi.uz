import { QueryClient } from '@tanstack/react-query';
import { act, fireEvent, render, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { createMemoryRouter, RouterProvider } from 'react-router-dom';
import { beforeAll, describe, expect, it, vi } from 'vitest';
import { UserRole } from '@amaliyotchi/shared/auth';
import { AppShell } from '@/app/layout';
import { AppProviders } from '@/app/providers';
import { issueSession, mockUsers } from '@/mocks/data';
import { useAuthStore } from '@/shared/auth/store';
import { RequireRole } from '@/shared/auth/RequireRole';
import {
  detailWithPeriods,
  ENDED_OPTIONS,
  ENDED_PERIOD,
  mockProfilePeriods,
  recordRequests,
} from './periodTestUtils';
import StudentDetailPage from './StudentDetailPage';

/**
 * `renderTutorRoute` naqshi, lekin marshrut shu yerda e'lon qilinadi:
 * `app/router.tsx` PM hududi (KONTRAKT §4.3), shuning uchun agent uni tahrirlamaydi.
 * PM `/tutor/students/:studentId` ni qo'shgach, bu helper `renderTutorRoute` bilan almashtiriladi.
 */
function renderStudentDetail(path: string) {
  useAuthStore.getState().setSession(issueSession(mockUsers[1]!));
  const router = createMemoryRouter(
    [
      {
        path: '/tutor',
        element: (
          <RequireRole roles={[UserRole.Tutor]}>
            <AppShell />
          </RequireRole>
        ),
        children: [{ path: 'students/:studentId', element: <StudentDetailPage /> }],
      },
    ],
    { initialEntries: [path] },
  );
  const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  render(
    <AppProviders queryClient={queryClient}>
      <RouterProvider router={router} />
    </AppProviders>,
  );
  return router;
}

// jsdom'da `createObjectURL` yo'q — `AuthImage` ning muvaffaqiyatli yo'lini sinash uchun stub.
beforeAll(() => {
  const url = globalThis.URL as unknown as {
    createObjectURL?: (b: Blob) => string;
    revokeObjectURL?: (u: string) => void;
  };
  url.createObjectURL = vi.fn(() => 'blob:mock-photo');
  url.revokeObjectURL = vi.fn();
});

describe('StudentDetailPage (/tutor/students/:studentId)', () => {
  it("profil, korxona, ariza va amaliyot davri bloklari ko'rinadi", async () => {
    renderStudentDetail('/tutor/students/s-341030');

    const profile = await screen.findByRole('article', { name: 'Talaba: Aliyev Akmal' });
    expect(screen.getByRole('heading', { level: 1 })).toHaveTextContent('Aliyev Akmal');
    expect(within(profile).getByText(/HEMIS 341030 · 412-22 · 3-kurs/)).toBeInTheDocument();
    expect(within(profile).getByText("O'qimoqda")).toHaveAttribute('data-status', 'ok');
    expect(within(profile).getByText('Raqamli iqtisodiyot')).toBeInTheDocument();
    expect(
      within(profile).getByRole('progressbar', { name: 'Aliyev Akmal davomati' }),
    ).toBeInTheDocument();

    const company = screen.getByRole('region', { name: 'Korxona' });
    expect(within(company).getAllByText('Tech Solutions MChJ').length).toBeGreaterThan(0);
    expect(within(company).getByText('304 512 889')).toBeInTheDocument();
    expect(within(company).getByText(/Buyuk Ipak Yo'li 12/)).toBeInTheDocument();
    expect(within(company).getByText(/Rustamov Jasur · \+998 90 111 22 33/)).toBeInTheDocument();
    expect(within(company).getAllByText('41.32451, 69.29712').length).toBeGreaterThan(0);
    expect(within(company).getByRole('img', { name: 'Korxona joylashuvi' })).toBeInTheDocument();

    const application = screen.getByRole('region', { name: 'Ariza' });
    expect(within(application).getByText('Tasdiqlangan')).toHaveAttribute('data-status', 'ok');
    expect(within(application).getByText('01.09.2026')).toBeInTheDocument();
    expect(
      within(application).getByRole('button', { name: 'shartnomani ochish' }),
    ).toBeInTheDocument();

    const period = screen.getByRole('region', { name: 'Amaliyot davri' });
    expect(within(period).getAllByText('3-kurs ishlab chiqarish amaliyoti').length).toBeGreaterThan(
      0,
    );
    expect(within(period).getByText('09:00 — 18:00')).toBeInTheDocument();
    expect(within(period).getByText('Du, Se, Ch, Pa, Ju, Sh')).toBeInTheDocument();
    expect(within(period).getByText('72 kun')).toBeInTheDocument();
  });

  it("kun-bakun davomat jadvali, check-in rasmi va sana oralig'i filtri", async () => {
    const user = userEvent.setup();
    renderStudentDetail('/tutor/students/s-341030');

    const table = await screen.findByRole('table', { name: 'Kundalik jadval' });
    // Davr boshi (07.09.2026) dan mock "bugun" (12.10.2026) gacha — 36 kun.
    expect(within(table).getByText('07.09.2026')).toBeInTheDocument();
    expect(within(table).getByText('12.10.2026')).toBeInTheDocument();
    expect(within(table).getAllByRole('row')).toHaveLength(37); // sarlavha + 36 kun
    expect(within(table).getAllByText('Dam olish').length).toBe(5); // yakshanbalar

    // Check-in rasmi — token bilan yuklanadi (blob), bosilganda modalda kattalashadi.
    const photoButtons = within(table).getAllByRole('button', { name: /check-in rasmi/ });
    expect(photoButtons.length).toBeGreaterThan(0);
    await waitFor(() =>
      expect(within(photoButtons[0]!).getByRole('img')).toHaveAttribute('src', 'blob:mock-photo'),
    );
    await user.click(photoButtons[0]!);
    const dialog = await screen.findByRole('dialog');
    expect(within(dialog).getByRole('img')).toHaveAttribute('src', 'blob:mock-photo');
    await user.click(within(dialog).getByRole('button', { name: 'Yopish' }));

    // Sana bosilsa — kun tafsiloti oynada (modal) ochiladi, keyin yopiladi.
    await user.click(within(table).getByText('07.09.2026'));
    const dayDialog = await screen.findByRole('dialog', { name: /07\.09\.2026 — kun tafsiloti/ });
    expect(within(dayDialog).getByText('Urinishlar')).toBeInTheDocument();
    await user.click(within(dayDialog).getByRole('button', { name: 'Yopish' }));
    await waitFor(() =>
      expect(
        screen.queryByRole('dialog', { name: /07\.09\.2026 — kun tafsiloti/ }),
      ).not.toBeInTheDocument(),
    );

    // Sana oralig'i: amaliyotdan oldingi oy → bo'sh holat.
    fireEvent.change(screen.getByLabelText('Dan'), { target: { value: '2026-01-01' } });
    fireEvent.change(screen.getByLabelText('Gacha'), { target: { value: '2026-01-31' } });
    expect(await screen.findByText("Tanlangan oraliqda davomat yozuvi yo'q.")).toBeInTheDocument();
  });

  it("kun qatori bosilsa — o'sha kunning lokatsiyasi, rasmi va kundaligi ko'rinadi", async () => {
    const user = userEvent.setup();
    renderStudentDetail('/tutor/students/s-341030');

    const table = await screen.findByRole('table', { name: 'Kundalik jadval' });
    await user.click(within(table).getByText('07.09.2026'));
    const panel = await screen.findByRole('dialog', { name: /07\.09\.2026 — kun tafsiloti/ });

    // Kirish va chiqish — yonma-yon ikki blok: vaqt, masofa, aniqlik, koordinata (bitta qator), selfi.
    const checkIn = within(panel).getByRole('group', { name: /^Kirish · / });
    const checkOut = within(panel).getByRole('group', { name: /^Chiqish · / });
    expect(within(panel).getAllByText('Lokatsiya')).toHaveLength(2);
    expect(within(checkIn).getByText('Masofa')).toBeInTheDocument();
    expect(within(checkIn).getByText('Aniqlik')).toBeInTheDocument();
    expect(within(checkIn).getByText(/^41\.\d+, 69\.\d+$/)).toBeInTheDocument();
    expect(within(checkOut).getByText(/^41\.\d+, 69\.\d+$/)).toBeInTheDocument();

    // Selfi — blok ichida kichik thumbnail; bosilsa katta preview (PhotoPreview modali).
    expect(within(checkIn).getByText('Kirish selfisi')).toBeInTheDocument();
    const selfie = within(checkIn).getByRole('button', {
      name: '07.09.2026 check-in rasmi — kattalashtirish',
    });
    await waitFor(() =>
      expect(within(selfie).getByRole('img')).toHaveAttribute('src', 'blob:mock-photo'),
    );
    // Chiqish selfisi yo'q — bir qatorli kulrang matn.
    expect(within(checkOut).getByText("Selfi yo'q")).toBeInTheDocument();

    // Kun sanoqlari — bitta qatorda.
    expect(within(panel).getByText('Urinishlar')).toBeInTheDocument();
    expect(within(panel).getByText('Ish kuni')).toBeInTheDocument();

    // Shu kunga yozgan kundaligi — matni, o'rgangani va ball bilan (chap ustunda).
    const diary = within(panel).getByRole('article', { name: 'Kundalik: 07.09.2026' });
    expect(within(diary).getByText(/O'rganganim:/)).toBeInTheDocument();
    expect(within(diary).getByText(/yuborilgan \d{2}:\d{2}/)).toBeInTheDocument();
  });

  it("kun oynasida kundalikni baholash — ball qo'yilsa holat va jadval yangilanadi", async () => {
    const user = userEvent.setup();
    renderStudentDetail('/tutor/students/s-341030');

    const table = await screen.findByRole('table', { name: 'Kundalik jadval' });
    // 14.09.2026 — davrning 7-ish kuni: kundaligi "Yuborilgan" (hali baholanmagan).
    await user.click(within(table).getByText('14.09.2026'));
    const dialog = await screen.findByRole('dialog', { name: /14\.09\.2026 — kun tafsiloti/ });
    const card = within(dialog).getByRole('article', { name: 'Kundalik: 14.09.2026' });
    expect(within(card).getByText('Yuborilgan')).toBeInTheDocument();

    const scores = within(card).getByRole('group', { name: '14.09.2026 balli' });
    await user.click(within(scores).getByRole('button', { name: '5' }));

    await waitFor(() =>
      expect(within(card).getByText('Tasdiqlangan')).toHaveAttribute('data-status', 'ok'),
    );
    expect(within(scores).getByRole('button', { name: '5' })).toHaveAttribute(
      'aria-pressed',
      'true',
    );
    // Ko'rib chiqilgach tugmalar qulflanadi (backend qayta baholashda 409 beradi).
    expect(within(scores).getByRole('button', { name: '4' })).toBeDisabled();

    // Jadvaldagi "Kundalik" ustuni ham yangilandi.
    const row = within(table).getByText('14.09.2026').closest('[role="row"]') as HTMLElement;
    await waitFor(() => expect(within(row).getByText('Tasdiqlangan')).toBeInTheDocument());
  });

  it("kun oynasida qayta yozishga qaytarish izohsiz o'tmaydi", async () => {
    const user = userEvent.setup();
    renderStudentDetail('/tutor/students/s-341030');

    const table = await screen.findByRole('table', { name: 'Kundalik jadval' });
    await user.click(within(table).getByText('14.09.2026'));
    const dialog = await screen.findByRole('dialog', { name: /14\.09\.2026 — kun tafsiloti/ });
    const card = within(dialog).getByRole('article', { name: 'Kundalik: 14.09.2026' });

    await user.click(within(card).getByRole('button', { name: 'Qayta yozishga qaytarish' }));
    expect(
      await within(card).findByText('Qayta yozish sababi (izoh) majburiy.'),
    ).toBeInTheDocument();

    await user.type(within(card).getByLabelText('Izoh'), 'Batafsilroq yozing');
    await user.click(within(card).getByRole('button', { name: 'Qayta yozishga qaytarish' }));
    await waitFor(() => expect(within(card).getByText('Qayta yozish kerak')).toBeInTheDocument());
  });

  it("kun oynasida kun ma'lumoti, keyin kundalik va uning fayllari — ixcham ro'yxat", async () => {
    const user = userEvent.setup();
    renderStudentDetail('/tutor/students/s-341030');

    // 12.10.2026 — mock "bugun", eng yangi kundalik: JPG + PDF biriktirilgan.
    const table = await screen.findByRole('table', { name: 'Kundalik jadval' });
    await user.click(within(table).getByText('12.10.2026'));
    const dialog = await screen.findByRole('dialog', { name: /12\.10\.2026 — kun tafsiloti/ });

    // Tartib: kun ma'lumotlari (kirish/chiqish, sanoqlar), keyin kundalik bo'limi.
    const details = within(dialog).getByRole('region', { name: "Kun ma'lumotlari" });
    const diarySection = within(dialog).getByRole('region', {
      name: 'Shu kunga yuborgan kundaligi',
    });
    expect(
      details.compareDocumentPosition(diarySection) & Node.DOCUMENT_POSITION_FOLLOWING,
    ).toBeTruthy();

    // Kundalik matni va baholash — kartada (matn DOM'da to'liq, CSS bilan qisqartiriladi).
    const card = within(diarySection).getByRole('article', { name: 'Kundalik: 12.10.2026' });
    expect(within(card).getByText(/ma'lumotlar bazasi sxemasini/)).toBeInTheDocument();
    expect(within(card).getByText(/O'rganganim:/)).toBeInTheDocument();
    // Fayllar kartada takrorlanmaydi — ostidagi ro'yxatda.
    expect(
      within(card).queryByRole('link', { name: 'kunlik_hisobot.pdf' }),
    ).not.toBeInTheDocument();

    const files = within(diarySection).getByRole('region', { name: 'Kundalik fayllari' });
    expect(card.compareDocumentPosition(files) & Node.DOCUMENT_POSITION_FOLLOWING).toBeTruthy();
    // Fayl nomi — havola: bosilsa yangi oynada to'liq ochiladi (token bilan yuklangan blob).
    await waitFor(() =>
      expect(within(files).getByRole('link', { name: 'kunlik_hisobot.pdf' })).toHaveAttribute(
        'href',
        'blob:mock-photo',
      ),
    );
    expect(within(files).getAllByRole('link', { name: 'Yangi oynada ochish' })).toHaveLength(2);
    const downloads = within(files).getAllByRole('link', { name: 'Yuklab olish' });
    expect(downloads).toHaveLength(2);
    expect(downloads[1]).toHaveAttribute('download', 'kunlik_hisobot.pdf');

    // "Ko'rish" — PDF/rasm shu yerda, alohida oynada ochiladi.
    await user.click(within(files).getByRole('button', { name: /ish_jarayoni_.*\.jpg — ko'rish/ }));
    const imageDialog = await screen.findByRole('dialog', { name: /ish_jarayoni_.*\.jpg/ });
    expect(within(imageDialog).getByRole('img', { name: /ish_jarayoni_.*\.jpg/ })).toHaveAttribute(
      'src',
      'blob:mock-photo',
    );
    await user.click(within(imageDialog).getByRole('button', { name: 'Yopish' }));

    await user.click(within(files).getByRole('button', { name: "kunlik_hisobot.pdf — ko'rish" }));
    const pdfDialog = await screen.findByRole('dialog', { name: 'kunlik_hisobot.pdf' });
    expect(within(pdfDialog).getByTitle('kunlik_hisobot.pdf')).toHaveAttribute(
      'src',
      'blob:mock-photo',
    );

    // Kundaliklar alohida bo'lim sifatida sahifada yo'q — hammasi kun oynasida.
    expect(screen.queryByRole('region', { name: 'Kundaliklar' })).not.toBeInTheDocument();
  });

  it("ariza tasdiqlanmagan bo'lsa korxona bloki bo'sh holatda", async () => {
    renderStudentDetail('/tutor/students/s-341032');

    await screen.findByRole('article', { name: 'Talaba: Sobirov Diyor' });
    const company = screen.getByRole('region', { name: 'Korxona' });
    expect(within(company).getByText('Ariza hali tasdiqlanmagan')).toBeInTheDocument();

    // Ariza bloki baribir to'ldirilgan bo'ladi.
    const application = screen.getByRole('region', { name: 'Ariza' });
    expect(within(application).getByText('Tuzatishda')).toHaveAttribute('data-status', 'late');
    expect(within(application).getByText(/qayta yuklang/)).toBeInTheDocument();
  });

  it("rad etilgan urinish va radius tashqarisi belgilari alohida ko'rsatiladi", async () => {
    renderStudentDetail('/tutor/students/s-341034');

    const table = await screen.findByRole('table', { name: 'Kundalik jadval' });
    // 09.09 — ichkaridan qabul qilingan, lekin undan oldin 2 urinish rad etilgan.
    const rejectedRow = within(table).getByText('09.09.2026').closest('[role="row"]')!;
    expect(
      within(rejectedRow as HTMLElement).getByText('2 urinish rad etildi'),
    ).toBeInTheDocument();
    // Shubhali holati `Badge` da (chip'da takrorlanmaydi).
    expect(within(rejectedRow as HTMLElement).getByText('Shubhali')).toHaveAttribute(
      'data-status',
      'bad',
    );
    expect(
      within(rejectedRow as HTMLElement).queryByText('Radius tashqarisida'),
    ).not.toBeInTheDocument();

    // 18.09 — qabul qilingan belgilanishning o'zi radius tashqarisida.
    const outsideRow = within(table).getByText('18.09.2026').closest('[role="row"]')!;
    expect(within(outsideRow as HTMLElement).getByText('Radius tashqarisida')).toBeInTheDocument();
  });

  it("ko'lamdan tashqari talaba → 404 bo'sh holati", async () => {
    renderStudentDetail('/tutor/students/s-999999');
    expect(await screen.findByText('Talaba topilmadi.')).toBeInTheDocument();
    expect(screen.getByRole('link', { name: 'Talabalarimga qaytish' })).toHaveAttribute(
      'href',
      '/tutor/students',
    );
  });

  it("server xatosi → 'Qayta urinish' bilan xato holati", async () => {
    const { http, HttpResponse } = await import('msw');
    const { server } = await import('@/mocks/server');
    server.use(
      http.get('/api/tutor/students/:id', () =>
        HttpResponse.json(
          { status: 500, title: 'Server xatosi', detail: 'Kutilmagan xato.' },
          { status: 500 },
        ),
      ),
    );
    renderStudentDetail('/tutor/students/s-341030');
    expect(await screen.findByText("Ma'lumotni yuklab bo'lmadi")).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Qayta urinish' })).toBeInTheDocument();
  });
});

describe('StudentDetailPage — amaliyot davri tanlagichi (v3.5)', () => {
  const PROFILE = '/api/tutor/students/s-341030';

  it("2 ta davr: tanlagich chiqadi, sukut — joriy faol davr; so'rovlar uning periodId'si bilan", async () => {
    const rec = recordRequests();
    renderStudentDetail('/tutor/students/s-341030');

    const tabs = await screen.findByRole('tablist', { name: 'Amaliyot davrlari' });
    const [spring, current] = within(tabs).getAllByRole('tab');
    // startDate kamayish tartibida: bahorgi (rejada) birinchi.
    expect(spring).toHaveTextContent('Bahorgi amaliyot 2027');
    expect(spring).toHaveTextContent('01.02 — 30.04.2027');
    expect(within(spring!).getByText('Rejada')).toHaveAttribute('data-status', 'info');
    expect(spring).toHaveAttribute('aria-selected', 'false');
    expect(current).toHaveTextContent('3-kurs ishlab chiqarish amaliyoti');
    expect(current).toHaveTextContent('07.09 — 18.12.2026');
    expect(within(current!).getByText('Faol')).toHaveAttribute('data-status', 'ok');
    expect(current).toHaveAttribute('aria-selected', 'true');

    // Birinchi yuklash periodId'siz; bog'liq so'rovlar tanlangan davr bilan.
    await screen.findByRole('table', { name: 'Kundalik jadval' });
    expect(rec.periodIds(PROFILE)).toEqual([null]);
    expect(rec.periodIds(`${PROFILE}/attendance`)).toContain('per-2026-3k');
    expect(rec.periodIds(`${PROFILE}/diaries`)).toContain('per-2026-3k');
    rec.stop();
  });

  it("boshqa davr tanlansa: periodId bilan so'rov, URL yangilanadi, rejadagi davr bo'sh holati", async () => {
    const user = userEvent.setup();
    const rec = recordRequests();
    const router = renderStudentDetail('/tutor/students/s-341030');

    await screen.findByRole('table', { name: 'Kundalik jadval' });
    await user.click(screen.getByRole('tab', { name: /Bahorgi amaliyot 2027/ }));

    expect(screen.getByRole('tab', { name: /Bahorgi amaliyot 2027/ })).toHaveAttribute(
      'aria-selected',
      'true',
    );
    expect(router.state.location.search).toBe('?period=per-2027-bahor');
    await waitFor(() => expect(rec.periodIds(PROFILE)).toContain('per-2027-bahor'));

    // Har bo'limda aniq bo'sh holat; nol foizli statistika yo'q.
    const profile = await screen.findByRole('article', { name: 'Talaba: Aliyev Akmal' });
    await waitFor(() =>
      expect(within(profile).getByText(/Bu davr 01\.02\.2027 dan boshlanadi/)).toBeInTheDocument(),
    );
    expect(within(profile).queryByRole('progressbar')).not.toBeInTheDocument();
    expect(within(profile).queryByText('Keldi')).not.toBeInTheDocument();
    for (const name of ['Korxona', 'Ariza', 'Kundalik jadval']) {
      expect(
        within(screen.getByRole('region', { name })).getByText('Bu davr 01.02.2027 dan boshlanadi'),
      ).toBeInTheDocument();
    }
    expect(screen.queryByRole('table', { name: 'Kundalik jadval' })).not.toBeInTheDocument();
    expect(
      within(screen.getByRole('region', { name: 'Amaliyot davri' })).getByText('09:00 — 17:00'),
    ).toBeInTheDocument();
    rec.stop();
  });

  it("URL'dagi ?period= sahifa ochilganda saqlanadi; begona davr → 404 va sukutga qaytish", async () => {
    const user = userEvent.setup();
    const router = renderStudentDetail('/tutor/students/s-341030?period=per-2027-bahor');
    expect(await screen.findByRole('tab', { name: /Bahorgi amaliyot 2027/ })).toHaveAttribute(
      'aria-selected',
      'true',
    );

    await act(() => router.navigate('/tutor/students/s-341030?period=begona'));
    expect(await screen.findByText('Amaliyot davri topilmadi.')).toBeInTheDocument();
    await user.click(screen.getByRole('button', { name: "Joriy davrni ko'rsatish" }));
    expect(await screen.findByRole('table', { name: 'Kundalik jadval' })).toBeInTheDocument();
    expect(router.state.location.search).toBe('');
  });

  it("1 ta davr: tanlagich yo'q, ma'lumot qatori", async () => {
    renderStudentDetail('/tutor/students/s-341031');
    const info = await screen.findByRole('group', { name: 'Amaliyot davri' });
    expect(info).toHaveTextContent('3-kurs ishlab chiqarish amaliyoti');
    expect(info).toHaveTextContent('07.09 — 18.12.2026');
    expect(within(info).getByText('Faol')).toBeInTheDocument();
    expect(screen.queryByRole('tablist')).not.toBeInTheDocument();
  });

  it("0 ta davr: bo'sh holat, statistika yo'q", async () => {
    mockProfilePeriods('tutor', detailWithPeriods([], null));
    renderStudentDetail('/tutor/students/s-341030');
    expect(
      await screen.findByText('Talabaga hali amaliyot davri biriktirilmagan'),
    ).toBeInTheDocument();
    expect(screen.queryByRole('tablist')).not.toBeInTheDocument();
    expect(screen.queryByRole('progressbar')).not.toBeInTheDocument();
  });

  it('"Tugagan" belgisi hisoblanadi; oy navigatsiyasi davr chegarasida to\'xtaydi', async () => {
    const user = userEvent.setup();
    mockProfilePeriods('tutor', detailWithPeriods(ENDED_OPTIONS, ENDED_PERIOD));
    renderStudentDetail('/tutor/students/s-341030');

    const tabs = await screen.findByRole('tablist', { name: 'Amaliyot davrlari' });
    // Backend `active` qaytargan, lekin endDate (30.04.2026) o'tgan → "Tugagan".
    expect(
      within(within(tabs).getByRole('tab', { name: /Bahorgi amaliyot 2026/ })).getByText('Tugagan'),
    ).toBeInTheDocument();
    expect(
      within(within(tabs).getByRole('tab', { name: /Kuzgi amaliyot 2025/ })).getByText('Yopilgan'),
    ).toBeInTheDocument();

    // Tugagan davr → kalendar oxirgi oyda (Aprel 2026); keyingi oyga o'tib bo'lmaydi.
    const section = screen.getByRole('region', { name: 'Kundalik jadval' });
    expect(within(section).getByRole('button', { name: 'Aprel 2026' })).toBeInTheDocument();
    expect(within(section).getByRole('button', { name: 'Keyingi oy' })).toBeDisabled();

    const prev = within(section).getByRole('button', { name: 'Oldingi oy' });
    await user.click(prev);
    await user.click(prev);
    expect(within(section).getByRole('button', { name: 'Fevral 2026' })).toBeInTheDocument();
    expect(prev).toBeDisabled();
    expect(within(section).getByRole('button', { name: 'Keyingi oy' })).toBeEnabled();
  });
});
