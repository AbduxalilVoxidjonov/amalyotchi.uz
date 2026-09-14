import { screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { renderTutorRoute } from '../test-utils';

describe('TodayPage (/tutor)', () => {
  it("stat, alert banner va davomat jadvali mock ma'lumot bilan ko'rinadi", async () => {
    renderTutorRoute('/tutor');
    expect(await screen.findByText('Aliyev Akmal')).toBeInTheDocument();
    expect(screen.getByText('radius ichida')).toBeInTheDocument();
    expect(screen.getByText('23/38')).toBeInTheDocument();
    expect(screen.getByText(/7 ta yangi ariza/)).toBeInTheDocument();
    expect(screen.getByText(/3 ta talaba radius tashqarisidan/)).toBeInTheDocument();
    expect(screen.getByText("38 talabadan 6 tasi ko'rsatilgan")).toBeInTheDocument();
    expect(screen.getByRole('heading', { level: 1 })).toHaveTextContent('Bugun');
    expect(screen.getByRole('button', { name: 'Eksport' })).toBeInTheDocument();
  });

  it('filtr pill → ?status= va jadval filtrlanadi; banner yopiladi', async () => {
    const user = userEvent.setup();
    const router = renderTutorRoute('/tutor');
    await screen.findByText('Aliyev Akmal');

    await user.click(screen.getByRole('button', { name: 'Kelmadi' }));
    await waitFor(() => expect(router.state.location.search).toBe('?status=absent'));
    expect(await screen.findByText("5 talabadan 5 tasi ko'rsatilgan")).toBeInTheDocument();
    const table = screen.getByRole('table', { name: 'Bugungi davomat' });
    expect(within(table).getByText('Sobirov Diyor')).toBeInTheDocument();
    expect(within(table).queryByText('Aliyev Akmal')).not.toBeInTheDocument();

    await user.click(screen.getByRole('button', { name: 'Shubhali' }));
    expect(await screen.findByText("1 talabadan 1 tasi ko'rsatilgan")).toBeInTheDocument();
    expect(within(table).getByText('Rahimov Sardor')).toBeInTheDocument();
    expect(within(table).getByText('Shubhali', { selector: '[data-status]' })).toHaveAttribute(
      'data-status',
      'bad',
    );
    await user.click(screen.getByRole('button', { name: 'Hammasi' }));

    await user.click(screen.getByRole('button', { name: 'Ogohlantirishlarni yopish' }));
    expect(screen.queryByText(/3 ta talaba radius tashqarisidan/)).not.toBeInTheDocument();
  });
});
