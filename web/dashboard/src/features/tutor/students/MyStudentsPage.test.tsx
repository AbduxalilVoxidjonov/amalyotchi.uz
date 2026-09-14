import { screen, within } from '@testing-library/react';
import { renderTutorRoute } from '../test-utils';

describe('MyStudentsPage (/tutor/students)', () => {
  it("jadval mock ma'lumot bilan ko'rinadi", async () => {
    renderTutorRoute('/tutor/students');
    const table = await screen.findByRole('table', { name: 'Talabalarim' });
    expect(within(table).getByText('Aliyev Akmal')).toBeInTheDocument();
    expect(within(table).getByText('HEMIS 341030')).toBeInTheDocument();
    expect(within(table).getByText('34/36 kun')).toBeInTheDocument();
    expect(within(table).getByText('32 ta · 4,2')).toBeInTheDocument();
    expect(within(table).getByText('Qizil bayroq')).toHaveAttribute('data-status', 'bad');
    expect(within(table).getByText('3 shubhali')).toHaveAttribute('data-status', 'late');
    expect(
      within(table).getByRole('progressbar', { name: 'Sobirov Diyor davomati' }),
    ).toHaveAttribute('data-kind', 'bad');
    expect(screen.getByRole('heading', { level: 1 })).toHaveTextContent('Talabalarim');
  });
});
