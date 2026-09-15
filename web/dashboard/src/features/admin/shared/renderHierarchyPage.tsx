import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { render } from '@testing-library/react';
import type { ReactElement } from 'react';
import { MemoryRouter, Route, Routes } from 'react-router-dom';
import { LocationProbe } from './LocationProbe';

/**
 * `useParams()`ga tayanadigan ierarxiya sahifalari (`FacultyDepartmentsPage` va h.k.) uchun test
 * render'i: QueryClient (retry yo'q) + `path` bo'yicha moslashtirilgan `Route`. Boshqa istalgan
 * marshrutga o'tish `data-testid="location"` elementida ko'rinadi — qatorga bosib ichkariga
 * o'tishni (URL o'zgarishini) tekshirish uchun.
 */
export function renderHierarchyPage(ui: ReactElement, path: string, initialEntries: string[]) {
  const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  return render(
    <QueryClientProvider client={queryClient}>
      <MemoryRouter initialEntries={initialEntries}>
        <Routes>
          <Route path={path} element={ui} />
          <Route path="*" element={<LocationProbe />} />
        </Routes>
      </MemoryRouter>
    </QueryClientProvider>,
  );
}
