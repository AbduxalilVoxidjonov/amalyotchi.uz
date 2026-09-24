import { screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { renderHierarchyPage } from '../shared/renderHierarchyPage';
import { mockPeriodGroupStudents, resetPracticePeriodsMock } from './mocks';
import { PeriodGroupPage } from './PeriodGroupPage';

function renderPage(periodId = 'p1', groupId = 'g1') {
  return renderHierarchyPage(
    <PeriodGroupPage />,
    '/admin/practice-periods/:periodId/groups/:groupId',
    [`/admin/practice-periods/${periodId}/groups/${groupId}`],
  );
}

const table = () => within(screen.getByRole('table', { name: 'Guruh talabalari' }));

/** Jadvaldagi talaba nomlari (ko'rinish tartibida). */
function names(): string[] {
  return table()
    .getAllByRole('row')
    .slice(1)
    .map((row) => within(row).queryAllByRole('link')[0]?.textContent ?? '')
    .filter(Boolean);
}

describe('PeriodGroupPage', () => {
  afterEach(() => resetPracticePeriodsMock());

  it("breadcrumb, guruh KPI'lari va talabalar jadvali; talaba havolasida davr parami", async () => {
    const data = mockPeriodGroupStudents('p1', 'g1')!;
    renderPage();

    expect(await screen.findByRole('heading', { name: '412-22 guruhi' })).toBeInTheDocument();
    expect(screen.getByRole('link', { name: 'Amaliyot davrlari' })).toHaveAttribute(
      'href',
      '/admin/practice-periods',
    );
    expect(screen.getByRole('link', { name: 'Ishlab chiqarish amaliyoti 2026' })).toHaveAttribute(
      'href',
      '/admin/practice-periods/p1',
    );

    const kpi = within(screen.getByRole('region', { name: "Guruh ko'rsatkichlari" }));
    expect(kpi.getByText(`${data.metrics.attendancePct}%`)).toBeInTheDocument();

    expect(
      screen.getByRole('heading', { name: `Talabalar (${data.students.length})` }),
    ).toBeInTheDocument();
    // Seed talaba (s1) — admin talaba profiliga, shu davr bilan.
    expect(table().getByRole('link', { name: 'Aliyev Akmal' })).toHaveAttribute(
      'href',
      '/admin/students/s1?period=p1',
    );
    expect(table().getByText('341030')).toBeInTheDocument();
    expect(table().getByRole('columnheader', { name: /Dav\. \/40/ })).toBeInTheDocument();
  });

  it('qidiruv: FISH va HEMIS ID bo‘yicha', async () => {
    const user = userEvent.setup();
    const data = mockPeriodGroupStudents('p1', 'g1')!;
    renderPage();
    await screen.findByRole('heading', { name: '412-22 guruhi' });

    const search = screen.getByRole('searchbox', { name: 'Talabani qidirish' });
    await user.type(search, 'aliyev ak');
    expect(names()).toEqual(['Aliyev Akmal']);

    await user.clear(search);
    await user.type(search, '341030');
    expect(names()).toEqual(['Aliyev Akmal']);

    await user.clear(search);
    await user.type(search, 'zzzz');
    expect(table().getByText('Talaba topilmadi')).toBeInTheDocument();
    expect(
      screen.getByRole('heading', { name: `Talabalar (0 / ${data.students.length})` }),
    ).toBeInTheDocument();
  });

  it('saralash: jami ball va davomat (kamayish → o‘sish)', async () => {
    const user = userEvent.setup();
    const data = mockPeriodGroupStudents('p1', 'g1')!;
    const byName = (a: string, b: string) => a.localeCompare(b, 'uz', { sensitivity: 'base' });
    renderPage();
    await screen.findByRole('heading', { name: '412-22 guruhi' });

    const expected = (key: 'total' | 'attendancePct', dir: 1 | -1) =>
      [...data.students]
        .sort((a, b) => (a[key] - b[key]) * dir || byName(a.fullName, b.fullName))
        .map((s) => s.fullName);

    await user.click(screen.getByRole('button', { name: /^Jami ball bo'yicha saralash/ }));
    expect(names()).toEqual(expected('total', -1));
    expect(
      screen.getByRole('button', { name: "Jami ball bo'yicha saralash (kamayish tartibida)" }),
    ).toBeInTheDocument();

    await user.click(screen.getByRole('button', { name: /^Jami ball bo'yicha saralash/ }));
    expect(names()).toEqual(expected('total', 1));

    await user.click(screen.getByRole('button', { name: /^Davomat bo'yicha saralash/ }));
    expect(names()).toEqual(expected('attendancePct', -1));
  });

  it("rejalashtirilgan davr — bo'sh holat, ball ustunlari yo'q", async () => {
    renderPage('p3', 'g3');
    expect(await screen.findByText('Davr hali boshlanmagan')).toBeInTheDocument();
    expect(screen.queryByRole('region', { name: "Guruh ko'rsatkichlari" })).not.toBeInTheDocument();
  });

  it('404: guruh davrda emas yoki davr yo‘q', async () => {
    const { unmount } = renderPage('p1', 'g3');
    expect(await screen.findByText('Guruh bu amaliyot davrida topilmadi.')).toBeInTheDocument();
    expect(screen.getByRole('link', { name: 'Amaliyot davriga qaytish' })).toHaveAttribute(
      'href',
      '/admin/practice-periods/p1',
    );
    unmount();

    renderPage('nope', 'g1');
    expect(await screen.findByText('Guruh bu amaliyot davrida topilmadi.')).toBeInTheDocument();
  });
});
