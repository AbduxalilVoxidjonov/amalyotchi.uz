import { QueryClient } from '@tanstack/react-query';
import { fireEvent, render, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { createMemoryRouter, RouterProvider } from 'react-router-dom';
import { beforeAll, describe, expect, it, vi } from 'vitest';
import { UserRole } from '@amaliyotchi/shared/auth';
import { AppShell } from '@/app/layout';
import { AppProviders } from '@/app/providers';
import { issueSession, mockUsers } from '@/mocks/data';
import { useAuthStore } from '@/shared/auth/store';
import { RequireRole } from '@/shared/auth/RequireRole';
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
    expect(within(application).getByRole('link', { name: 'shartnomani ochish' })).toHaveAttribute(
      'href',
      '/api/files/contract-s-341030',
    );

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

    const table = await screen.findByRole('table', { name: 'Kun-bakun davomat' });
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

    // Qator bosilsa — kun tafsiloti paneli (xarita + urinishlar).
    await user.click(within(table).getByText('07.09.2026'));
    const panel = await screen.findByRole('region', { name: '07.09.2026 kuni tafsiloti' });
    expect(within(panel).getByText('Urinishlar')).toBeInTheDocument();

    // Sana oralig'i: amaliyotdan oldingi oy → bo'sh holat.
    fireEvent.change(screen.getByLabelText('Dan'), { target: { value: '2026-01-01' } });
    fireEvent.change(screen.getByLabelText('Gacha'), { target: { value: '2026-01-31' } });
    expect(await screen.findByText("Tanlangan oraliqda davomat yozuvi yo'q.")).toBeInTheDocument();
  });

  it("kundaliklar matni, foto hisobot va fayl havolasi ko'rinadi", async () => {
    renderStudentDetail('/tutor/students/s-341030');

    const diaries = await screen.findByRole('region', { name: 'Kundaliklar' });
    const cards = await within(diaries).findAllByRole('article', { name: /^Kundalik: / });
    expect(cards).toHaveLength(15);

    const first = cards[0]!;
    expect(within(first).getByText(/ma'lumotlar bazasi sxemasini/)).toBeInTheDocument();
    expect(within(first).getByText(/O'rganganim:/)).toBeInTheDocument();
    expect(within(first).getByRole('link', { name: 'kunlik_hisobot.pdf' })).toHaveAttribute(
      'href',
      expect.stringContaining('/api/files/doc-s-341030-'),
    );
    // Foto hisobot — rasm sifatida (Chip havola emas).
    expect(within(first).getByRole('button', { name: /ish_jarayoni_.*\.jpg/ })).toBeInTheDocument();
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

    const table = await screen.findByRole('table', { name: 'Kun-bakun davomat' });
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
