import { http, HttpResponse, type HttpHandler } from 'msw';
import { STUDENT_ENDPOINTS } from '@/shared/api/endpoints';
import { requireBearer } from '@/mocks/problem';
import type { PortfolioDto } from './types';

/** SPEC-SCREENS §16 mock — kontrakt v2 shakli. */
export const mockPortfolio: PortfolioDto = {
  student: 'Aliyev Akmal',
  group: '412-22',
  practiceTitle: '3-kurs ishlab chiqarish amaliyoti',
  company: 'Tech Solutions MChJ',
  periodFrom: '2026-10-01',
  periodTo: '2026-11-15',
  stats: {
    attendancePct: 94,
    daysPresent: 34,
    daysTotal: 36,
    late: 2,
    excused: 1,
    reports: 32,
    avgScore: 4.2,
  },
  score: [
    { key: 'attendance', weightPct: 40, points: 37.6 },
    { key: 'reports', weightPct: 30, points: 25.2 },
    { key: 'tutor', weightPct: 20, points: 18 },
    { key: 'reference', weightPct: 10, points: 10 },
  ],
  total: 90.8,
  grade: 5,
  finalized: true,
  conclusion: {
    text: "Talaba amaliyot davrida barqaror qatnashdi, kundalik yozuvlari mazmunli va aniq. Backend yo'nalishida real vazifalarda qatnashgani ko'rinadi. Hisobotlarda mentor izohlari aks etgan, takroriy matn yo'q.",
    author: 'N. Saidova',
    date: '2026-11-16T10:00:00+05:00',
  },
  pdfUrl: null,
};

export const portfolioHandlers: HttpHandler[] = [
  http.get(STUDENT_ENDPOINTS.portfolio, ({ request }) => {
    return requireBearer(request) ?? HttpResponse.json(mockPortfolio);
  }),
];
