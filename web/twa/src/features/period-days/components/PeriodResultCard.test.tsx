import { render, screen, within } from '@testing-library/react';
import type { PeriodDay, PeriodDayStatus, PeriodSummary } from '../types';
import { PeriodResultCard } from './PeriodResultCard';

const PERIOD: PeriodSummary = {
  id: 'p1',
  name: 'Yozgi amaliyot 2026',
  status: 'closed',
  startDate: '2026-06-01',
  endDate: '2026-07-11',
  requiredDays: 30,
  elapsedWorkDays: 28,
};

const mk = (status: PeriodDayStatus, n: number): PeriodDay[] =>
  Array.from({ length: n }, (_, i) => ({
    date: `2026-06-${String(i + 1).padStart(2, '0')}`,
    weekday: 1,
    isWorkDay: true,
    holiday: null,
    status,
    checkInAt: null,
    checkOutAt: null,
    autoClosed: false,
    suspicious: false,
    manual: false,
    diary: null,
  }));

const stat = (label: string) =>
  screen.getByText(label, { selector: 'dt' }).nextElementSibling?.textContent;

describe('PeriodResultCard', () => {
  it('keldi / kech qoldi / kelmadi, sababli, talab qilingan kunlar va davomat foizi', () => {
    render(
      <PeriodResultCard
        period={PERIOD}
        days={[
          ...mk('present', 24),
          ...mk('late', 3),
          ...mk('absent', 2),
          ...mk('excused', 1),
          ...mk('dayOff', 4),
        ]}
      />,
    );
    const card = within(screen.getByRole('region', { name: 'Davr yakuni' }));
    expect(stat('Keldi')).toBe('27');
    expect(stat('Kech qoldi')).toBe('3');
    expect(stat('Kelmadi')).toBe('2');
    expect(card.getByText(/ish kunidan/)).toHaveTextContent('30 ish kunidan 27 kun keldi');
    expect(card.getByText(/Sababli:/)).toHaveTextContent('Sababli: 1 kun');
    // 27 / (27 + 2) = 93.1%
    const bar = card.getByRole('progressbar', { name: 'Davomat 93%' });
    expect(bar).toHaveAttribute('aria-valuenow', '93');
  });

  it('sababli 0 → qator yo‘q; hisoblanadigan kun yo‘q → foiz "—"', () => {
    render(<PeriodResultCard period={PERIOD} days={mk('future', 3)} />);
    expect(screen.queryByText(/Sababli/)).not.toBeInTheDocument();
    expect(stat('Keldi')).toBe('0');
    expect(screen.queryByRole('progressbar')).not.toBeInTheDocument();
    expect(screen.getByText('—')).toBeInTheDocument();
  });
});
