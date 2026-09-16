import { screen, within } from '@testing-library/react';
import { renderWithProviders } from '../shared/renderWithProviders';
import { CompaniesPage } from './CompaniesPage';

describe('CompaniesPage', () => {
  it("v3 ro'yxatni ko'rsatadi (STIR formati, radius, belgi)", async () => {
    renderWithProviders(<CompaniesPage />);
    expect(await screen.findByText('Tech Solutions MChJ')).toBeInTheDocument();
    expect(screen.getByText('305 881 204')).toBeInTheDocument();
    expect(screen.getByText('450 m')).toBeInTheDocument();
    expect(screen.getByText('Katta radius')).toBeInTheDocument();
    expect(screen.getByText("Shubhali to'planish")).toBeInTheDocument();
    expect(screen.getByRole('button', { name: "Shubhali to'planishlar" })).toBeInTheDocument();
  });

  it('korxona nomi detail sahifasiga havola qiladi', async () => {
    renderWithProviders(<CompaniesPage />);
    expect(await screen.findByRole('link', { name: 'Tech Solutions MChJ' })).toHaveAttribute(
      'href',
      '/admin/companies/c1',
    );
  });

  it("STIR chegarasidan oshgan korxona ogohlantirish bilan ko'rsatiladi", async () => {
    renderWithProviders(<CompaniesPage />);
    await screen.findByText('Mega Servis MChJ');
    const table = within(screen.getByRole('table', { name: 'Korxonalar' }));
    expect(table.getByText('21/10')).toHaveAttribute('data-status', 'bad');
    expect(table.getByText("Talaba ko'p")).toHaveAttribute('data-status', 'bad');
  });
});
