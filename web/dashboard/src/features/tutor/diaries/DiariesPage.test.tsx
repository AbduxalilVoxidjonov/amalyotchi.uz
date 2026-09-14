import { screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { renderTutorRoute } from '../test-utils';

describe('DiariesPage (/tutor/diaries)', () => {
  it("kundalik kartalari ko'rinadi; ball → Tasdiqlangan (score tasdiqlaydi); tugmalar qulflanadi", async () => {
    const user = userEvent.setup();
    renderTutorRoute('/tutor/diaries');
    const card = await screen.findByRole('article', { name: 'Kundalik: Aliyev Akmal' });
    expect(screen.getByRole('heading', { level: 1 })).toHaveTextContent('Kundalik hisobotlar');
    expect(within(card).getByText('412-22 · 11.10.2026 · 17:42')).toBeInTheDocument();
    expect(within(card).getByText('Yuborilgan')).toHaveAttribute('data-status', 'info');
    expect(within(card).getByRole('link', { name: 'ekran_surati_1.png' })).toHaveAttribute(
      'href',
      '/api/files/f-d1-1',
    );
    expect(within(card).getByText(/So‘rov validatsiyasi/)).toBeInTheDocument();
    expect(screen.getByText('Qayta yozish kerak')).toHaveAttribute('data-status', 'late');

    // Qayta yozilgan karta — backend 409 beradi, tugmalar o'chirilgan.
    const rewritten = screen.getByRole('article', { name: 'Kundalik: Karimov Bekzod' });
    expect(within(rewritten).getByText(/Batafsil yozing/)).toBeInTheDocument();
    expect(within(rewritten).getByRole('button', { name: 'Tasdiqlash' })).toBeDisabled();

    await user.click(within(card).getByRole('button', { name: '4' }));
    await waitFor(() =>
      expect(within(card).getByRole('button', { name: '4' })).toHaveAttribute(
        'aria-pressed',
        'true',
      ),
    );
    expect(within(card).getByText('Tasdiqlangan')).toHaveAttribute('data-status', 'ok');
    expect(within(card).getByRole('button', { name: 'Tasdiqlash' })).toBeDisabled();
  });

  it('Qayta yozishga qaytarish — izohsiz lokal xato, izoh bilan → status rewrite', async () => {
    const user = userEvent.setup();
    renderTutorRoute('/tutor/diaries');
    const card = await screen.findByRole('article', { name: 'Kundalik: Yusupova Nilufar' });

    await user.click(within(card).getByRole('button', { name: 'Qayta yozishga qaytarish' }));
    const field = within(card).getByLabelText('Izoh');
    expect(field).toBeInvalid();

    await user.type(field, 'Statistika jadvalini biriktiring');
    await user.click(within(card).getByRole('button', { name: 'Qayta yozishga qaytarish' }));
    await waitFor(() =>
      expect(within(card).getByText('Qayta yozish kerak')).toHaveAttribute('data-status', 'late'),
    );
    expect(within(card).getByText('Tyutor izohi:')).toBeInTheDocument();
    expect(within(card).getByText('Statistika jadvalini biriktiring')).toBeInTheDocument();
  });
});
