import { http, HttpResponse, type HttpHandler } from 'msw';
import { STUDENT_ENDPOINTS } from '@/shared/api/endpoints';
import { problem, requireBearer } from '@/mocks/problem';
import { MOCK_AUTUMN_PERIOD, MOCK_SPRING_PERIOD } from '@/features/period/mocks';
import type { PortfolioDto } from './types';

/** Talabaning davrlari — `startDate` kamayish tartibida (bahorgi kelgusi, kuzgi — sukut). */
export const mockPortfolioPeriods = [MOCK_SPRING_PERIOD, MOCK_AUTUMN_PERIOD];

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
  periodId: MOCK_AUTUMN_PERIOD.id,
  periods: mockPortfolioPeriods,
};

/** Hali boshlanmagan bahorgi davr portfoliosi — bo'sh statlar, baho yo'q. */
export const mockSpringPortfolio: PortfolioDto = {
  ...mockPortfolio,
  practiceTitle: MOCK_SPRING_PERIOD.name,
  company: null,
  periodFrom: MOCK_SPRING_PERIOD.startDate,
  periodTo: MOCK_SPRING_PERIOD.endDate,
  stats: {
    attendancePct: 0,
    daysPresent: 0,
    daysTotal: 0,
    late: 0,
    excused: 0,
    reports: 0,
    avgScore: 0,
  },
  score: mockPortfolio.score.map((s) => ({ ...s, points: 0 })),
  total: 0,
  grade: null,
  finalized: false,
  conclusion: null,
  periodId: MOCK_SPRING_PERIOD.id,
};

export const portfolioHandlers: HttpHandler[] = [
  http.get(STUDENT_ENDPOINTS.portfolio, ({ request }) => {
    const denied = requireBearer(request);
    if (denied) return denied;
    const periodId = new URL(request.url).searchParams.get('periodId');
    if (!periodId || periodId === MOCK_AUTUMN_PERIOD.id) return HttpResponse.json(mockPortfolio);
    if (periodId === MOCK_SPRING_PERIOD.id) return HttpResponse.json(mockSpringPortfolio);
    return problem(404, 'Topilmadi', 'Amaliyot davri topilmadi.');
  }),
];
