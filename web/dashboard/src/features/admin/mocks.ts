import type { HttpHandler } from 'msw';
import { auditHandlers } from './audit/mocks';
import { companiesHandlers } from './companies/mocks';
import { dashboardHandlers } from './dashboard/mocks';
import { facultiesHandlers } from './faculties/mocks';
import { groupsHandlers } from './groups/mocks';
import { settingsHandlers } from './settings/mocks';
import { studentsHandlers } from './students/mocks';
import { tutorsHandlers } from './tutors/mocks';

/**
 * Admin bo'limi MSW handler'lari (`src/mocks/handlers.ts` ro'yxatiga qo'shiladi).
 * ❓ Bearer tekshiruvi yo'q (auth mock'laridan farqli) — testlarda sessiya sozlamasdan ishlatish uchun.
 */
export const adminHandlers: HttpHandler[] = [
  ...dashboardHandlers,
  ...facultiesHandlers,
  ...groupsHandlers,
  ...tutorsHandlers,
  ...studentsHandlers,
  ...companiesHandlers,
  ...auditHandlers,
  ...settingsHandlers,
];
