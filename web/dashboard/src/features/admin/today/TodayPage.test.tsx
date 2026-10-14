import { screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { renderAdminRoute } from '../shared/renderAdminRoute';

describe('TodayPage (/admin/today)', () => {
  it("stat, alert banner va davomat jadvali mock ma'lumot bilan ko'rinadi", async () => {
    renderAdminRoute('/admin/today');
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
    const router = renderAdminRoute('/admin/today');
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

  it('"Joyida" ustuni: holat chip\'lari, soxta joylashuv belgisi, oxirgi ping vaqti', async () => {
    renderAdminRoute('/admin/today');
    const table = await screen.findByRole('table', { name: 'Bugungi davomat' });
    await within(table).findByText('Aliyev Akmal');
    expect(within(table).getByRole('columnheader', { name: 'Joyida' })).toBeInTheDocument();
    expect(within(table).getByText('Joyida', { selector: '[data-state]' })).toHaveAttribute(
      'data-tone',
      'ok',
    );
    expect(within(table).getByText('Tashqarida · 18 daq')).toHaveAttribute('data-tone', 'bad');
    expect(within(table).getByText(/^Signal yo'q · \d{2}:\d{2} dan$/)).toHaveAttribute(
      'data-tone',
      'lost',
    );
    expect(within(table).getByText('Ketgan')).toBeInTheDocument();
    expect(within(table).getByRole('img', { name: 'Soxta joylashuv aniqlangan' })).toHaveAttribute(
      'title',
      'Soxta joylashuv aniqlangan',
    );
    expect(within(table).getByText('3 daq oldin')).toBeInTheDocument();
    // Qator nomi — kunlik iz sahifasiga havola.
    expect(within(table).getByRole('link', { name: 'Aliyev Akmal' })).toHaveAttribute(
      'href',
      '/admin/today/students/s-341030/location?name=Aliyev+Akmal',
    );
  });

  it("joylashuv filtri: Tashqarida / Signal yo'q → ?presence=", async () => {
    const user = userEvent.setup();
    const router = renderAdminRoute('/admin/today');
    const table = await screen.findByRole('table', { name: 'Bugungi davomat' });
    await within(table).findByText('Aliyev Akmal');
    const group = screen.getByRole('group', { name: 'Joylashuv filtri' });

    await user.click(within(group).getByRole('button', { name: 'Tashqarida' }));
    await waitFor(() => expect(router.state.location.search).toBe('?presence=outside'));
    await waitFor(() => expect(within(table).queryByText('Aliyev Akmal')).not.toBeInTheDocument());
    expect(within(table).getByText('Rahimov Sardor')).toBeInTheDocument();

    await user.click(within(group).getByRole('button', { name: "Signal yo'q" }));
    await waitFor(() => expect(router.state.location.search).toBe('?presence=signalLost'));
    expect(await within(table).findByText('Karimov Bekzod')).toBeInTheDocument();
    expect(within(table).queryByText('Rahimov Sardor')).not.toBeInTheDocument();

    await user.click(within(group).getByRole('button', { name: 'Barchasi' }));
    await waitFor(() => expect(router.state.location.search).toBe(''));
    expect(await within(table).findByText('Aliyev Akmal')).toBeInTheDocument();
  });
});
