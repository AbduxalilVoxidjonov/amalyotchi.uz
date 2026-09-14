import { http, HttpResponse, type HttpHandler } from 'msw';
import type { MapResponse } from './types';

/** SPEC-SCREENS §11 mock (5 nuqta). */
export const mockMap: MapResponse = {
  date: '2026-10-12',
  points: [
    {
      studentId: 's-341030',
      name: 'Aliyev Akmal',
      company: 'Tech Solutions MChJ',
      distanceM: 45,
      radiusM: 150,
      rejected: false,
      time: '09:02',
      kind: 'ok',
      lat: 41.3111,
      lng: 69.2797,
    },
    {
      studentId: 's-341033',
      name: 'Yusupova Nilufar',
      company: 'Mediapark',
      distanceM: 30,
      radiusM: 150,
      rejected: false,
      time: '08:57',
      kind: 'ok',
      lat: 41.3265,
      lng: 69.2285,
    },
    {
      studentId: 's-341031',
      name: 'Karimov Bekzod',
      company: 'Agrobank ATB',
      distanceM: 85,
      radiusM: 120,
      rejected: false,
      time: '09:41',
      kind: 'late',
      lat: 41.3123,
      lng: 69.2787,
    },
    {
      studentId: 's-341034',
      name: 'Rahimov Sardor',
      company: 'Qurilish Trest 12',
      distanceM: 410,
      radiusM: 450,
      rejected: false,
      time: '09:08',
      kind: 'bad',
      lat: 41.2995,
      lng: 69.2401,
    },
    {
      studentId: 's-341036',
      name: 'Mirzayev Jasur',
      company: 'Uzbekinvest AJ',
      distanceM: 3400,
      radiusM: 150,
      rejected: true,
      time: '09:12',
      kind: 'bad',
      lat: 41.2856,
      lng: 69.2034,
    },
  ],
};

export const mapHandlers: HttpHandler[] = [
  http.get('/api/tutor/map', () => HttpResponse.json(mockMap)),
];
