import { screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { renderHierarchyPage } from '../shared/renderHierarchyPage';
import { StudentDetailPage } from './StudentDetailPage';

function renderPage(studentId = 's1') {
  return renderHierarchyPage(<StudentDetailPage />, '/admin/students/:studentId', [
    `/admin/students/${studentId}`,
  ]);
}

describe('Admin StudentDetailPage', () => {
  it("breadcrumb, profil kartasi va tashkiliy ma'lumot ko'rsatiladi", async () => {
    renderPage();
    expect(await screen.findByRole('heading', { name: 'Aliyev Akmal' })).toBeInTheDocument();

    const breadcrumb = screen.getByRole('navigation', { name: "Yo'l" });
    expect(within(breadcrumb).getByRole('link', { name: 'Talabalar' })).toHaveAttribute(
      'href',
      '/admin/students',
    );

    const profile = within(screen.getByRole('article', { name: 'Talaba: Aliyev Akmal' }));
    expect(profile.getByText('341030')).toBeInTheDocument();
    expect(profile.getByText('412-22')).toBeInTheDocument();

    const meta = within(screen.getByRole('region', { name: "Tashkiliy ma'lumot" }));
    expect(meta.getByRole('link', { name: 'Nodira Saidova' })).toHaveAttribute(
      'href',
      '/admin/tutors/t1',
    );
    expect(meta.getByText('+998 90 765-43-21')).toBeInTheDocument();
    expect(meta.getByText('Dasturiy injiniring kafedrasi')).toBeInTheDocument();
    expect(meta.getByText("Bog'langan")).toBeInTheDocument();
    expect(meta.getByText('Faol')).toBeInTheDocument();
  });

  it("korxona, ariza va davr bloklari tyutor profilidagidek to'ladi", async () => {
    renderPage();
    const company = within(await screen.findByRole('region', { name: 'Korxona' }));
    expect(company.getAllByText('Tech Solutions MChJ').length).toBeGreaterThan(0);
    expect(company.getByText('304 512 889')).toBeInTheDocument();

    expect(
      within(screen.getByRole('region', { name: 'Ariza' })).getByText('Tasdiqlangan'),
    ).toBeInTheDocument();
    expect(
      within(screen.getByRole('region', { name: 'Amaliyot davri' })).getAllByText(
        '3-kurs ishlab chiqarish amaliyoti',
      ).length,
    ).toBeGreaterThan(0);
  });

  it("davomat va kundaliklar admin endpoint'idan yuklanadi", async () => {
    const user = userEvent.setup();
    renderPage();
    const attendance = within(await screen.findByRole('region', { name: 'Kundalik jadval' }));
    const table = await attendance.findByRole('table', { name: 'Kundalik jadval' });

    // Kundaliklar alohida bo'lim emas — kun oynasida o'sha kunnikini ko'rsatadi.
    expect(screen.queryByRole('region', { name: 'Kundaliklar' })).not.toBeInTheDocument();
    await user.click(within(table).getByText('12.10.2026'));
    const dialog = await screen.findByRole('dialog', { name: /12\.10\.2026 — kun tafsiloti/ });
    expect(
      within(dialog).getByRole('article', { name: 'Kundalik: 12.10.2026' }),
    ).toBeInTheDocument();
  });

  it('kun oynasida admin ham kundalikka ball qo\'ya oladi', async () => {
    const user = userEvent.setup();
    renderPage();

    const table = await screen.findByRole('table', { name: 'Kundalik jadval' });
    await user.click(within(table).getByText('14.09.2026'));
    const dialog = await screen.findByRole('dialog', { name: /14\.09\.2026 — kun tafsiloti/ });
    const card = within(dialog).getByRole('article', { name: 'Kundalik: 14.09.2026' });
    expect(within(card).getByText('Yuborilgan')).toBeInTheDocument();

    const scores = within(card).getByRole('group', { name: '14.09.2026 balli' });
    await user.click(within(scores).getByRole('button', { name: '4' }));

    await waitFor(() =>
      expect(within(card).getByText('Tasdiqlangan')).toHaveAttribute('data-status', 'ok'),
    );
    expect(within(scores).getByRole('button', { name: '4' })).toHaveAttribute(
      'aria-pressed',
      'true',
    );
  });

  it("Telegram ulanmagan talabada tegishli holat ko'rinadi", async () => {
    renderPage('s5');
    expect(await screen.findByRole('heading', { name: 'Oripov Javohir' })).toBeInTheDocument();
    const meta = within(screen.getByRole('region', { name: "Tashkiliy ma'lumot" }));
    expect(meta.getByText("Bog'lanmagan")).toBeInTheDocument();
    expect(meta.getByText('Ulanmagan')).toBeInTheDocument();
  });

  it("noma'lum talaba → 404 holati va orqaga qaytish", async () => {
    renderPage('yoq');
    expect(await screen.findByText('Talaba topilmadi.')).toBeInTheDocument();
    expect(screen.getByRole('link', { name: 'Talabalarga qaytish' })).toHaveAttribute(
      'href',
      '/admin/students',
    );
  });
});
