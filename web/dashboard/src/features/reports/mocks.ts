import { http, HttpResponse, type HttpHandler } from 'msw';
import { REPORTS_ENDPOINT } from './api';
import type { ReportsCatalog } from './types';

/** Backend `GetReportsCatalogQuery` kartalari (4 ta) — tyutor ko'lami. */
const NOTE = "Fayl generatsiyasi keyingi bosqichda (M14) qo'shiladi.";

export const mockReports: ReportsCatalog = {
  filter: {
    dateFrom: '2026-10-01',
    dateTo: '2026-11-15',
    scope: '412-22, 413-22',
    groups: ['412-22', '413-22'],
    studentCount: 38,
  },
  reports: [
    {
      id: 'attendance',
      name: 'Davomat hisoboti',
      formats: ['xlsx'],
      desc: "Davr bo'yicha har talabaning kunlik davomati: keldi / kech keldi / kelmadi / sababli, foiz.",
      available: false,
      note: NOTE,
    },
    {
      id: 'portfolio',
      name: 'Talaba portfoliosi',
      formats: ['pdf'],
      desc: 'Kundalik hisobotlar, davomat statistikasi, baholar va tyutor xulosasi — bitta PDF.',
      available: false,
      note: NOTE,
    },
    {
      id: 'company-reference',
      name: 'Korxona tavsifnomasi',
      formats: ['pdf'],
      desc: "Korxona rahbari imzolaydigan tavsifnoma shabloni (talaba ma'lumotlari to'ldirilgan).",
      available: false,
      note: NOTE,
    },
    {
      id: 'diaries',
      name: "Kundaliklar yig'masi",
      formats: ['pdf', 'xlsx'],
      desc: "Davr bo'yicha barcha kundalik yozuvlari va tyutor baholari.",
      available: false,
      note: NOTE,
    },
  ],
};

export const reportsHandlers: HttpHandler[] = [
  http.get(REPORTS_ENDPOINT, () => HttpResponse.json(mockReports)),
];
