import { http, HttpResponse, type HttpHandler } from 'msw';
import { paginateMock } from '../shared/paginate';
import { COMPANIES_ENDPOINT } from './api';
import type { Company } from './types';

/** Backend `CompanyRow` shaklida (SPEC-SCREENS §9.7 raqamlari; STIR xom). */
export const mockCompanies: Company[] = [
  {
    id: 'c1',
    name: 'Tech Solutions MChJ',
    tin: '304512889',
    activity: "Dasturiy ta'minot",
    address: 'Toshkent, Amir Temur 108',
    radiusM: 150,
    students: 4,
    suspiciousDays: 0,
    isActive: true,
    flag: null,
  },
  {
    id: 'c2',
    name: 'Agrobank ATB',
    tin: '201344712',
    activity: 'Bank xizmatlari',
    address: 'Toshkent, Mustaqillik 12',
    radiusM: 120,
    students: 6,
    suspiciousDays: 0,
    isActive: true,
    flag: null,
  },
  {
    id: 'c3',
    name: 'Qurilish Trest 12',
    tin: '305881204',
    activity: 'Qurilish',
    address: 'Toshkent v., Zangiota',
    radiusM: 450,
    students: 9,
    suspiciousDays: 1,
    isActive: true,
    flag: 'largeRadius',
  },
  {
    id: 'c4',
    name: 'Mega Servis MChJ',
    tin: '308190556',
    activity: "Xizmat ko'rsatish",
    address: 'Toshkent, Chilonzor 7',
    radiusM: 200,
    students: 21,
    suspiciousDays: 12,
    isActive: true,
    flag: 'suspicious',
  },
];

export const companiesHandlers: HttpHandler[] = [
  http.get(COMPANIES_ENDPOINT, ({ request }) =>
    HttpResponse.json(paginateMock(request.url, mockCompanies, (c) => [c.name, c.tin, c.address])),
  ),
];
