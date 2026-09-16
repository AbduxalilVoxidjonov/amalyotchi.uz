import { screen } from '@testing-library/react';
import { renderWithProviders } from '../shared/renderWithProviders';
import { StudentsPage } from './StudentsPage';

describe('StudentsPage', () => {
  it("mock ro'yxatni ko'rsatadi (holatlar, ulanmagan korxona)", async () => {
    renderWithProviders(<StudentsPage />);
    expect(await screen.findByText('Aliyev Akmal')).toBeInTheDocument();
    expect(screen.getByText('Qizil bayroq')).toBeInTheDocument();
    expect(screen.getByText('Ulanmagan')).toBeInTheDocument();
    expect(screen.getByText('—')).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'HEMIS dan tortish' })).toBeInTheDocument();
    expect(screen.getByText('1–5 / 5')).toBeInTheDocument();
  });

  it('talaba ismi profil sahifasiga havola', async () => {
    renderWithProviders(<StudentsPage />);
    expect(await screen.findByRole('link', { name: 'Aliyev Akmal' })).toHaveAttribute(
      'href',
      '/admin/students/s1',
    );
  });
});
