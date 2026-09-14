import { screen } from '@testing-library/react';
import { renderWithProviders } from '../shared/renderWithProviders';
import { TutorsPage } from './TutorsPage';

describe('TutorsPage', () => {
  it("v2 ro'yxatni ko'rsatadi (telefon, doira, holat)", async () => {
    renderWithProviders(<TutorsPage />);
    expect(await screen.findByText('Baxtiyor Rasulov')).toBeInTheDocument();
    expect(screen.getByText('Kechikmoqda')).toBeInTheDocument();
    expect(screen.getByText('+998 90 111-22-33')).toBeInTheDocument();
    expect(screen.getByText('AT · 412-22, 413-22')).toBeInTheDocument();
    expect(screen.getByText('IM · 5 guruh')).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Kirish havolasini yuborish' })).toBeInTheDocument();
  });
});
