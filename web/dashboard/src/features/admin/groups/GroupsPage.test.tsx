import { screen } from '@testing-library/react';
import { renderWithProviders } from '../shared/renderWithProviders';
import { GroupsPage } from './GroupsPage';

describe('GroupsPage', () => {
  it("v2 ro'yxatni ko'rsatadi (tyutorsiz guruh — chiziqcha)", async () => {
    renderWithProviders(<GroupsPage />);
    expect(await screen.findByText('412-22')).toBeInTheDocument();
    expect(screen.getByText('Dasturiy injiniring')).toBeInTheDocument();
    expect(screen.getAllByText('Nodira Saidova')).toHaveLength(2);
    expect(screen.getByText('—')).toBeInTheDocument();
    expect(screen.getByRole('button', { name: "Kursga ko'chirish" })).toBeInTheDocument();
    expect(screen.getByText('1–4 / 4')).toBeInTheDocument();
  });
});
