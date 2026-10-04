import { screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { renderTutorRoute } from '../test-utils';

describe('ApplicationsPage (/tutor/applications)', () => {
  it("tab'lar, kartalar va birinchi ariza detail'i ko'rinadi", async () => {
    renderTutorRoute('/tutor/applications');
    expect(await screen.findByRole('tab', { name: /Yangi/ })).toHaveAttribute(
      'aria-selected',
      'true',
    );
    const list = await screen.findByRole('list', { name: "Arizalar ro'yxati" });
    expect(within(list).getAllByRole('listitem')).toHaveLength(7);
    expect(within(list).getByRole('button', { name: /Aliyev Akmal/ })).toHaveAttribute(
      'aria-pressed',
      'true',
    );

    const detail = await screen.findByRole('article', { name: 'Ariza: Aliyev Akmal' });
    expect(within(detail).getByText('412-22 · HEMIS 341030 · 3-kurs')).toBeInTheDocument();
    expect(within(list).getByText('2 soat oldin')).toBeInTheDocument();
    expect(within(detail).getByText('304 512 889')).toBeInTheDocument();
    expect(within(detail).getByText('Islomov B. · +998 90 123 45 67')).toBeInTheDocument();
    expect(within(detail).getByText('41.3111, 69.2797')).toBeInTheDocument();
    expect(within(detail).getAllByRole('checkbox')).toHaveLength(7);
    expect(within(detail).getByText('shartnoma_aliyev.pdf')).toBeInTheDocument();
    expect(within(detail).getByText(/2 bet · 1,8 MB/)).toBeInTheDocument();
    expect(within(detail).getByText('Har bir qaror audit jurnaliga yoziladi')).toBeInTheDocument();
  });

  it('karta tanlash → ?id=, radius stepper ±50 m, checkbox lokal', async () => {
    const user = userEvent.setup();
    const router = renderTutorRoute('/tutor/applications');
    const list = await screen.findByRole('list', { name: "Arizalar ro'yxati" });

    await user.click(within(list).getByRole('button', { name: /Mirzayev Jasur/ }));
    await waitFor(() => expect(router.state.location.search).toBe('?id=app-2'));
    const detail = await screen.findByRole('article', { name: 'Ariza: Mirzayev Jasur' });
    expect(within(detail).getByText('Uzbekinvest AJ')).toBeInTheDocument();

    expect(within(detail).getByText('150 m')).toBeInTheDocument();
    await user.click(within(detail).getByRole('button', { name: 'Radiusni oshirish' }));
    expect(within(detail).getByText('200 m')).toBeInTheDocument();
    await user.click(within(detail).getByRole('button', { name: 'Radiusni kamaytirish' }));
    await user.click(within(detail).getByRole('button', { name: 'Radiusni kamaytirish' }));
    expect(within(detail).getByText('100 m')).toBeInTheDocument();

    const box = within(detail).getByRole('checkbox', {
      name: 'Shartnoma imzolangan va muhrlangan',
    });
    await user.click(box);
    expect(box).toBeChecked();
  });

  it("Tasdiqlash → mutation → ro'yxat yangilanadi (ariza Tasdiqlangan tab'ga o'tadi)", async () => {
    const user = userEvent.setup();
    const router = renderTutorRoute('/tutor/applications?id=app-2');
    const detail = await screen.findByRole('article', { name: 'Ariza: Mirzayev Jasur' });
    expect(screen.getByRole('tab', { name: /Yangi/ })).toHaveTextContent('7');

    // Izohsiz qaytarish — backend'ga bormaydi, lokal xato.
    await user.click(within(detail).getByRole('button', { name: 'Tuzatishga qaytarish' }));
    expect(within(detail).getByRole('alert')).toHaveTextContent(/izoh/i);
    expect(router.state.location.search).toBe('?id=app-2');

    await user.click(within(detail).getByRole('checkbox', { name: 'STIR 9 xonali va haqiqiy korxonaga tegishli' }));
    await user.click(within(detail).getByRole('button', { name: 'Tasdiqlash' }));

    const list = screen.getByRole('list', { name: "Arizalar ro'yxati" });
    await waitFor(() =>
      expect(
        within(list).queryByRole('button', { name: /Mirzayev Jasur/ }),
      ).not.toBeInTheDocument(),
    );
    expect(within(list).getAllByRole('listitem')).toHaveLength(6);
    expect(screen.getByRole('tab', { name: /Yangi/ })).toHaveTextContent('6');
    expect(screen.getByRole('tab', { name: /Tasdiqlangan/ })).toHaveTextContent('25');

    await user.click(screen.getByRole('tab', { name: /Tasdiqlangan/ }));
    await waitFor(() => expect(router.state.location.search).toBe('?tab=approved'));
    await user.click(await screen.findByRole('button', { name: /Mirzayev Jasur/ }));
    const approved = await screen.findByRole('article', { name: 'Ariza: Mirzayev Jasur' });
    expect(within(approved).getByText('Tasdiqlangan')).toHaveAttribute('data-status', 'ok');
    expect(within(approved).getByRole('button', { name: 'Tasdiqlash' })).toBeDisabled();
    expect(
      within(approved).getByRole('checkbox', { name: 'STIR 9 xonali va haqiqiy korxonaga tegishli' }),
    ).toBeChecked();
    expect(within(approved).getByText('150 m')).toBeInTheDocument();
  });

  it("Rad etish izoh bilan → 'Rad etilgan' tab'da izoh ko'rinadi", async () => {
    const user = userEvent.setup();
    renderTutorRoute('/tutor/applications?id=app-5');
    const detail = await screen.findByRole('article', { name: 'Ariza: Saidov Alisher' });
    await user.type(within(detail).getByLabelText('Izoh'), 'Shartnoma muddati tugagan');
    await user.click(within(detail).getByRole('button', { name: 'Rad etish' }));
    await waitFor(() => expect(screen.getByRole('tab', { name: /Rad etilgan/ })).toHaveTextContent('2'));
    await user.click(screen.getByRole('tab', { name: /Rad etilgan/ }));
    await user.click(await screen.findByRole('button', { name: /Saidov Alisher/ }));
    const rejected = await screen.findByRole('article', { name: 'Ariza: Saidov Alisher' });
    expect(within(rejected).getByText(/Oldingi izoh: Shartnoma muddati tugagan/)).toBeInTheDocument();
  });
});
