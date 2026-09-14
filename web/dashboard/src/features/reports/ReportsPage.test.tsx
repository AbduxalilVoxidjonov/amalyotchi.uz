import { screen } from '@testing-library/react';
import { renderWithProviders } from '@/features/admin/shared/renderWithProviders';
import { ReportsPage } from './ReportsPage';

describe('ReportsPage', () => {
  it("filtr va 4 ta hisobot kartasini ko'rsatadi (yuklab olish hozircha o'chiq)", async () => {
    renderWithProviders(<ReportsPage />, ['/admin/reports']);
    expect(await screen.findByText('Talaba portfoliosi')).toBeInTheDocument();
    expect(screen.getByText('01.10.2026 — 15.11.2026')).toBeInTheDocument();
    expect(screen.getByText('412-22, 413-22 · 38 talaba')).toBeInTheDocument();
    const buttons = screen.getAllByRole('button', { name: 'Yuklab olish' });
    expect(buttons).toHaveLength(4);
    expect(buttons[0]).toBeDisabled();
    expect(screen.getByText('PDF / Excel')).toBeInTheDocument();
    expect(screen.getByText('Excel')).toBeInTheDocument();
  });
});
