import { screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { renderTutorRoute } from '../test-utils';

describe('LeaveRequestsPage (/tutor/leave-requests)', () => {
  it("jadval ko'rinadi; Hammasini tasdiqlash → Kutilmoqda qolmaydi", async () => {
    const user = userEvent.setup();
    renderTutorRoute('/tutor/leave-requests');
    const table = await screen.findByRole('table', { name: "Ruxsat so'rovlari" });
    expect(within(table).getByText('15.10–16.10')).toBeInTheDocument();
    expect(within(table).getByText('14.10')).toBeInTheDocument();
    expect(within(table).getByRole('link', { name: 'spravka.pdf' })).toHaveAttribute(
      'href',
      '/api/files/f-lr-1',
    );
    expect(within(table).getByText('ariza.pdf')).toBeInTheDocument();
    expect(within(table).getAllByText('Kutilmoqda')).toHaveLength(2);
    expect(screen.getByRole('heading', { level: 1 })).toHaveTextContent("Ruxsat so'rovlari");

    await user.click(screen.getByRole('button', { name: 'Hammasini tasdiqlash' }));
    await waitFor(() => expect(within(table).queryByText('Kutilmoqda')).not.toBeInTheDocument());
    expect(within(table).getAllByText('Tasdiqlangan')).toHaveLength(3);
    expect(screen.getByRole('button', { name: 'Hammasini tasdiqlash' })).toBeDisabled();
  });
});
