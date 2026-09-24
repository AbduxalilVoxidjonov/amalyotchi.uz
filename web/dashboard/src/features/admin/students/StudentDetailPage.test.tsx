import { screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { useLocation } from 'react-router-dom';
import {
  detailWithPeriods,
  ENDED_OPTIONS,
  ENDED_PERIOD,
  mockProfilePeriods,
  recordRequests,
} from '@/features/tutor/students/periodTestUtils';
import { renderHierarchyPage } from '../shared/renderHierarchyPage';
import { StudentDetailPage } from './StudentDetailPage';

/** Joriy `?query` — URL'dagi davr tanlovini tekshirish uchun. */
function SearchProbe() {
  return <div data-testid="search">{useLocation().search}</div>;
}

function renderPage(studentId = 's1', search = '') {
  return renderHierarchyPage(
    <>
      <StudentDetailPage />
      <SearchProbe />
    </>,
    '/admin/students/:studentId',
    [`/admin/students/${studentId}${search}`],
  );
}

const ADMIN_EXTRA = {
  groupId: 'g1',
  department: 'Dasturiy injiniring kafedrasi',
  adminStatus: 'active',
  telegramLinked: true,
  tutor: null,
};

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

  it('admin kun oynasida ham urinishlar galereyasi va jadvalda ikki thumbnail bor', async () => {
    const user = userEvent.setup();
    renderPage('s4');
    const table = await screen.findByRole('table', { name: 'Kundalik jadval' });
    const row = within(table).getByText('10.09.2026').closest('[role="row"]') as HTMLElement;
    expect(
      within(row).getByRole('button', { name: '10.09.2026 check-in rasmi — kattalashtirish' }),
    ).toBeInTheDocument();
    expect(
      within(row).getByRole('button', { name: '10.09.2026 check-out rasmi — kattalashtirish' }),
    ).toBeInTheDocument();

    await user.click(within(table).getByText('09.09.2026'));
    const dialog = await screen.findByRole('dialog', { name: /09\.09\.2026 — kun tafsiloti/ });
    const gallery = within(dialog).getByRole('region', { name: 'Urinishlar' });
    expect(within(gallery).getAllByRole('listitem')).toHaveLength(4);
    expect(within(gallery).getByText('QR kod mos emas')).toBeInTheDocument();
  });

  it("kun oynasida admin ham kundalikka ball qo'ya oladi", async () => {
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

describe('Admin StudentDetailPage — amaliyot davri tanlagichi (v3.5)', () => {
  const PROFILE = '/api/admin/students/s1';

  it("2 ta davr: tanlagich va sukut davr; boshqa davr → periodId, URL va rejadagi bo'sh holat", async () => {
    const user = userEvent.setup();
    const rec = recordRequests();
    renderPage();

    const tabs = await screen.findByRole('tablist', { name: 'Amaliyot davrlari' });
    expect(within(tabs).getAllByRole('tab')).toHaveLength(2);
    expect(within(tabs).getByRole('tab', { selected: true })).toHaveTextContent(
      '3-kurs ishlab chiqarish amaliyoti',
    );
    await screen.findByRole('table', { name: 'Kundalik jadval' });
    expect(rec.periodIds(PROFILE)).toEqual([null]);
    expect(rec.periodIds(`${PROFILE}/attendance`)).toContain('per-2026-3k');
    expect(screen.getByTestId('search')).toHaveTextContent(/^$/);

    await user.click(within(tabs).getByRole('tab', { name: /Bahorgi amaliyot 2027/ }));
    expect(screen.getByTestId('search')).toHaveTextContent('?period=per-2027-bahor');
    await waitFor(() => expect(rec.periodIds(PROFILE)).toContain('per-2027-bahor'));
    expect(
      await within(screen.getByRole('region', { name: 'Kundalik jadval' })).findByText(
        'Bu davr 01.02.2027 dan boshlanadi',
      ),
    ).toBeInTheDocument();
    expect(
      within(screen.getByRole('region', { name: 'Korxona' })).getByText(
        'Bu davr 01.02.2027 dan boshlanadi',
      ),
    ).toBeInTheDocument();
    expect(screen.queryByRole('progressbar')).not.toBeInTheDocument();
    // Admin bloki davrdan qat'i nazar ko'rinadi.
    expect(screen.getByRole('region', { name: "Tashkiliy ma'lumot" })).toBeInTheDocument();
    rec.stop();
  });

  it("?period= bilan ochilsa o'sha davr tanlanadi", async () => {
    const rec = recordRequests();
    renderPage('s1', '?period=per-2027-bahor');
    expect(await screen.findByRole('tab', { name: /Bahorgi amaliyot 2027/ })).toHaveAttribute(
      'aria-selected',
      'true',
    );
    expect(rec.periodIds(PROFILE)).toEqual(['per-2027-bahor']);
    rec.stop();
  });

  it("1 ta davr: tanlagich yo'q, ma'lumot qatori", async () => {
    renderPage('s3');
    const info = await screen.findByRole('group', { name: 'Amaliyot davri' });
    expect(info).toHaveTextContent('3-kurs ishlab chiqarish amaliyoti');
    expect(screen.queryByRole('tablist')).not.toBeInTheDocument();
  });

  it("0 ta davr: bo'sh holat", async () => {
    mockProfilePeriods('admin', detailWithPeriods([], null), ADMIN_EXTRA);
    renderPage();
    expect(
      await screen.findByText('Talabaga hali amaliyot davri biriktirilmagan'),
    ).toBeInTheDocument();
    expect(screen.queryByRole('tablist')).not.toBeInTheDocument();
  });

  it('"Tugagan" belgisi va oy navigatsiyasi davr chegarasida', async () => {
    const user = userEvent.setup();
    mockProfilePeriods('admin', detailWithPeriods(ENDED_OPTIONS, ENDED_PERIOD), ADMIN_EXTRA);
    renderPage();

    const tab = await screen.findByRole('tab', { name: /Bahorgi amaliyot 2026/ });
    expect(within(tab).getByText('Tugagan')).toHaveAttribute('data-status', 'late');

    const section = within(screen.getByRole('region', { name: 'Kundalik jadval' }));
    expect(section.getByRole('button', { name: 'Aprel 2026' })).toBeInTheDocument();
    expect(section.getByRole('button', { name: 'Keyingi oy' })).toBeDisabled();
    const prev = section.getByRole('button', { name: 'Oldingi oy' });
    await user.click(prev);
    await user.click(prev);
    await user.click(prev);
    expect(section.getByRole('button', { name: 'Fevral 2026' })).toBeInTheDocument();
    expect(prev).toBeDisabled();
  });
});
