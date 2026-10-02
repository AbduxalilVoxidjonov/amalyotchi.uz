import { render, screen } from '@testing-library/react';
import { createMemoryRouter, RouterProvider } from 'react-router-dom';
import { chunkReloadDeps, resetChunkReloadState } from '@/shared/lib/chunk-reload';
import { RouteError } from './RouteError';

function Boom({ error }: { error: Error }): never {
  throw error;
}

function renderWithError(error: Error) {
  const router = createMemoryRouter([
    { path: '/', element: <Boom error={error} />, errorElement: <RouteError scope="root" /> },
  ]);
  return render(<RouterProvider router={router} />);
}

describe('dashboard RouteError', () => {
  beforeEach(() => {
    vi.spyOn(console, 'error').mockImplementation(() => {});
    window.sessionStorage.clear();
    resetChunkReloadState();
  });
  afterEach(() => vi.restoreAllMocks());

  it('render xatosi → o‘zbekcha ekran (inglizcha "Unexpected Application Error" emas)', () => {
    renderWithError(new TypeError('x is undefined'));
    expect(screen.getByRole('alert')).toHaveTextContent("Sahifani ochib bo'lmadi");
    expect(screen.queryByText(/Unexpected Application Error/i)).not.toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Qayta yuklash' })).toBeInTheDocument();
  });

  it('eski chunk xatosi → bir marta avtomatik qayta yuklash', () => {
    const reload = vi.spyOn(chunkReloadDeps, 'reload').mockImplementation(() => {});
    renderWithError(new TypeError('Failed to fetch dynamically imported module: /assets/x.js'));
    expect(reload).toHaveBeenCalledTimes(1);
    expect(screen.getByRole('alert')).toHaveTextContent('Ilova yangilanmoqda');
  });
});
