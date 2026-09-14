import { screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { http, HttpResponse } from 'msw';
import { server } from '@/mocks/server';
import { renderWithProviders } from '../shared/renderWithProviders';
import { FACULTIES_ENDPOINT } from './api';
import { FacultiesPage } from './FacultiesPage';

describe('FacultiesPage', () => {
  it("mock ro'yxatni ko'rsatadi va qidiradi (debounce)", async () => {
    const user = userEvent.setup();
    renderWithProviders(<FacultiesPage />);
    expect(await screen.findByText('Axborot texnologiyalari')).toBeInTheDocument();
    expect(screen.getByText("E'tibor")).toBeInTheDocument();
    expect(screen.getByText('1–4 / 4')).toBeInTheDocument();

    await user.type(screen.getByLabelText('Qidirish'), 'filolog');
    await waitFor(() =>
      expect(screen.queryByText('Axborot texnologiyalari')).not.toBeInTheDocument(),
    );
    expect(screen.getByText('Filologiya')).toBeInTheDocument();
    expect(screen.getByText('1–1 / 1')).toBeInTheDocument();
  });

  it("bo'sh javob → EmptyState", async () => {
    server.use(
      http.get(FACULTIES_ENDPOINT, () =>
        HttpResponse.json({ items: [], page: 1, pageSize: 20, total: 0 }),
      ),
    );
    renderWithProviders(<FacultiesPage />);
    expect(await screen.findByText("Fakultetlar yo'q")).toBeInTheDocument();
    expect(screen.getByText('0–0 / 0')).toBeInTheDocument();
  });
});
