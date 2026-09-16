import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { render } from '@testing-library/react';
import { MemoryRouter, Route, Routes } from 'react-router-dom';
import CompaniesPage from './CompaniesPage';
import CompanyDetailPage from './CompanyDetailPage';

/**
 * Korxonalar marshrutlari `app/router.tsx` (PM hududi) ga qo'shilmagunicha — lokal router.
 * Marshrutlar qo'shilgach testlarni `renderTutorRoute('/tutor/companies')` ga o'tkazish mumkin.
 * MSW handler'lari global ro'yxatda (`features/tutor/mocks.ts`).
 */
export function renderCompaniesRoute(path = '/tutor/companies') {
  const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  return render(
    <QueryClientProvider client={queryClient}>
      <MemoryRouter initialEntries={[path]}>
        <Routes>
          <Route path="/tutor/companies" element={<CompaniesPage />} />
          <Route path="/tutor/companies/:companyId" element={<CompanyDetailPage />} />
        </Routes>
      </MemoryRouter>
    </QueryClientProvider>,
  );
}
