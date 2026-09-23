import { screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { http, HttpResponse } from 'msw';
import { server } from '@/mocks/server';
import { renderHierarchyPage } from '../shared/renderHierarchyPage';
import { PRACTICE_PERIODS_ENDPOINT } from './api';
import { resetPracticePeriodsMock } from './mocks';
import { PracticePeriodsPage } from './PracticePeriodsPage';

function renderPage(entry = '/admin/practice-periods') {
  return renderHierarchyPage(<PracticePeriodsPage />, '/admin/practice-periods', [entry]);
}

function table() {
  return within(screen.getByRole('table', { name: 'Amaliyot davrlari' }));
}

describe('PracticePeriodsPage', () => {
  afterEach(() => resetPracticePeriodsMock());

  it("ro'yxat: sanalar, kun soni, status, guruh/talaba soni (startDate ↓)", async () => {
    renderPage();
    expect(await screen.findByText('Ishlab chiqarish amaliyoti 2026')).toBeInTheDocument();

    const t = table();
    expect(t.getByText('31.08.2026 — 14.10.2026')).toBeInTheDocument();
    expect(t.getByText('45 kun')).toBeInTheDocument();
    expect(t.getByText('Faol', { selector: '[data-status]' })).toHaveAttribute('data-status', 'ok');
    expect(t.getByText('Rejada', { selector: '[data-status]' })).toHaveAttribute(
      'data-status',
      'info',
    );
    expect(t.getByText('Yopilgan', { selector: '[data-status]' })).toHaveAttribute(
      'data-status',
      'neu',
    );
    // p1: g1 (19) + g2 (19) + g4 (24) = 62 talaba, 3 guruh.
    const row = within(
      t.getByText('Ishlab chiqarish amaliyoti 2026').closest('[role="row"]') as HTMLElement,
    );
    expect(row.getByText('3')).toBeInTheDocument();
    expect(row.getByText('62')).toBeInTheDocument();

    const names = t
      .getAllByRole('link')
      .map((a) => a.textContent)
      .filter((n) => n !== 'Yangi amaliyot davri');
    expect(names).toEqual([
      'Qishki amaliyot 2027',
      'Ishlab chiqarish amaliyoti 2026',
      '2-kurs bahorgi amaliyot',
    ]);
    expect(screen.getByRole('link', { name: 'Yangi amaliyot davri' })).toHaveAttribute(
      'href',
      '/admin/practice-periods/new',
    );
  });

  it('status filtri: chip bosilsa faqat shu holatdagi davrlar', async () => {
    const user = userEvent.setup();
    renderPage();
    await screen.findByText('Ishlab chiqarish amaliyoti 2026');

    const filters = within(screen.getByRole('group', { name: "Status bo'yicha filtr" }));
    expect(filters.getByRole('button', { name: 'Hammasi' })).toHaveAttribute(
      'aria-pressed',
      'true',
    );

    await user.click(filters.getByRole('button', { name: 'Rejada' }));
    await waitFor(() =>
      expect(screen.queryByText('Ishlab chiqarish amaliyoti 2026')).not.toBeInTheDocument(),
    );
    expect(table().getByText('Qishki amaliyot 2027')).toBeInTheDocument();
    expect(filters.getByRole('button', { name: 'Rejada' })).toHaveAttribute('aria-pressed', 'true');

    await user.click(filters.getByRole('button', { name: 'Yopilgan' }));
    expect(await screen.findByText('2-kurs bahorgi amaliyot')).toBeInTheDocument();
    await waitFor(() => expect(screen.queryByText('Qishki amaliyot 2027')).not.toBeInTheDocument());
  });

  it('URL dagi ?status= filtri qo‘llanadi', async () => {
    renderPage('/admin/practice-periods?status=active');
    expect(await screen.findByText('Ishlab chiqarish amaliyoti 2026')).toBeInTheDocument();
    expect(screen.queryByText('Qishki amaliyot 2027')).not.toBeInTheDocument();
  });

  it('qator bosilsa — davr sahifasi ochiladi (rowHref)', async () => {
    const user = userEvent.setup();
    renderPage();
    await screen.findByText('Qishki amaliyot 2027');
    await user.click(table().getByText('11.01.2027 — 20.02.2027'));
    expect(await screen.findByTestId('location')).toHaveTextContent('/admin/practice-periods/p3');
  });

  it("bo'sh holat: tushuntirish va yaratish tugmasi", async () => {
    server.use(http.get(PRACTICE_PERIODS_ENDPOINT, () => HttpResponse.json([])));
    renderPage();
    expect(await screen.findByText("Hali amaliyot davri yo'q")).toBeInTheDocument();
    expect(screen.getAllByRole('link', { name: 'Yangi amaliyot davri' })).toHaveLength(2);
  });
});
