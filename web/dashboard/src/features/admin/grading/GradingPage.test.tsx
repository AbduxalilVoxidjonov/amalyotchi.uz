import { screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { renderTutorRoute } from '../test-utils';

describe('GradingPage (/tutor/grading)', () => {
  it("jadval ko'rinadi; tavsiya etilgan ballarni qabul qilish → PUT → baho hisoblanadi", async () => {
    const user = userEvent.setup();
    renderTutorRoute('/tutor/grading');
    const table = await screen.findByRole('table', { name: 'Baholash' });
    expect(within(table).getByText('40,0 · 100%')).toBeInTheDocument();
    expect(within(table).getByText('28,8 · 4,8')).toBeInTheDocument();
    expect(within(table).getByText('97,8')).toBeInTheDocument();
    expect(within(table).getByText('41,1')).toBeInTheDocument();
    expect(within(table).getByText('Qayta topshiradi')).toHaveAttribute('data-status', 'bad');

    await user.click(screen.getByRole('button', { name: 'Tavsiya etilgan ballarni qabul qilish' }));
    // 25,6 + 15,5 + 10 + 5 = 56,1 — lekin davomat 64% < 70% → baho baribir "Qayta topshiradi".
    expect(await within(table).findByText('56,1')).toBeInTheDocument();
    expect(within(table).getByText('Qayta topshiradi')).toHaveAttribute('data-status', 'bad');
    await waitFor(() =>
      expect(
        screen.getByRole('button', { name: 'Tavsiya etilgan ballarni qabul qilish' }),
      ).toBeDisabled(),
    );
  });

  it('qidiruv nom bo‘yicha filtrlaydi', async () => {
    const user = userEvent.setup();
    renderTutorRoute('/tutor/grading');
    const table = await screen.findByRole('table', { name: 'Baholash' });
    await user.type(screen.getByRole('textbox', { name: 'Qidirish' }), 'sardor');
    expect(within(table).getByText('Rahimov Sardor')).toBeInTheDocument();
    expect(within(table).queryByText('Aliyev Akmal')).not.toBeInTheDocument();
  });
});
