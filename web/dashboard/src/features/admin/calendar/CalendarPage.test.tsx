import { screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { renderTutorRoute } from '../test-utils';

describe('CalendarPage (/tutor/calendar)', () => {
  it("oy grid, katak kodlari (backend holati → kod) va legend ko'rinadi", async () => {
    renderTutorRoute('/tutor/calendar?month=2026-10');
    expect(
      await screen.findByRole('heading', { level: 2, name: 'Oktabr 2026 · 01–31 kunlar' }),
    ).toBeInTheDocument();
    const rows = screen.getAllByRole('row');
    expect(rows).toHaveLength(7); // head + 6 talaba
    const akmal = rows[1]!;
    expect(within(akmal).getByRole('rowheader')).toHaveTextContent('Aliyev Akmal');
    const cells = within(akmal).getAllByRole('cell');
    expect(cells).toHaveLength(31);
    expect(cells.slice(0, 12).map((c) => c.getAttribute('data-code')).join('')).toBe('kkkldkkkakkn');
    expect(cells[11]).toHaveAttribute('data-status', 'pending');
    expect(cells[30]).toHaveAttribute('data-status', 'future');
    expect(cells[3]).toHaveAttribute('data-status', 'late');
    expect(cells[3]).toHaveTextContent('k');
    expect(cells[8]).toHaveTextContent('×');
    expect(cells[8]).toHaveAccessibleName('Kelmadi');
    expect(screen.getByText('Kelmagan kun')).toBeInTheDocument();
  });

  it('oy almashtirish → ?month= va sarlavha yangilanadi', async () => {
    const user = userEvent.setup();
    const router = renderTutorRoute('/tutor/calendar?month=2026-10');
    await screen.findByRole('heading', { level: 2, name: 'Oktabr 2026 · 01–31 kunlar' });

    await user.click(screen.getByRole('button', { name: 'Noyabr ›' }));
    await waitFor(() => expect(router.state.location.search).toBe('?month=2026-11'));
    expect(
      await screen.findByRole('heading', { level: 2, name: 'Noyabr 2026 · 01–30 kunlar' }),
    ).toBeInTheDocument();
    expect(screen.getByRole('button', { name: '‹ Oktabr' })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Dekabr ›' })).toBeInTheDocument();

    await user.click(screen.getByRole('button', { name: '‹ Oktabr' }));
    await user.click(screen.getByRole('button', { name: '‹ Sentabr' }));
    await waitFor(() => expect(router.state.location.search).toBe('?month=2026-09'));
    expect(
      await screen.findByRole('heading', { level: 2, name: 'Sentabr 2026 · 01–30 kunlar' }),
    ).toBeInTheDocument();
  });
});
