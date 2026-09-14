import { screen } from '@testing-library/react';
import { renderWithProviders } from '../shared/renderWithProviders';
import { CompaniesPage } from './CompaniesPage';

describe('CompaniesPage', () => {
  it("v2 ro'yxatni ko'rsatadi (STIR formati, radius, belgi)", async () => {
    renderWithProviders(<CompaniesPage />);
    expect(await screen.findByText('Tech Solutions MChJ')).toBeInTheDocument();
    expect(screen.getByText('305 881 204')).toBeInTheDocument();
    expect(screen.getByText('450 m')).toBeInTheDocument();
    expect(screen.getByText('Katta radius')).toBeInTheDocument();
    expect(screen.getByText("Shubhali to'planish")).toBeInTheDocument();
    expect(screen.getByRole('button', { name: "Shubhali to'planishlar" })).toBeInTheDocument();
  });
});
