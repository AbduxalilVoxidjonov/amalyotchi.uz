import { http, HttpResponse, type HttpHandler } from 'msw';
import { mockAudit } from '../audit/mocks';
import { ADMIN_DASHBOARD_ENDPOINT } from './api';
import type { AdminDashboard } from './types';

/** Backend `AdminDashboardDto` shaklida (SPEC-SCREENS §7 raqamlari). */
export const mockAdminDashboard: AdminDashboard = {
  date: '2026-10-12',
  stats: {
    studentsTotal: 1284,
    studentsLinked: 1106,
    studentsUnlinked: 178,
    faculties: 11,
    groups: 47,
    companiesActive: 96,
    companiesWithInterns: 88,
    applicationsPending: 43,
    applicationsOverdue: 7,
    contractsApproved: 1180,
    contractsRevision: 12,
    contractsRejected: 9,
    contractsMissing: 40,
    ongoingPeriods: 3,
    expectedToday: 1180,
    presentToday: 1030,
    lateToday: 44,
    absentToday: 92,
    excusedToday: 14,
    noDiaryToday: 210,
    attendanceTodayPct: 92,
    expectedYesterday: 1176,
    attendanceYesterdayPct: 89,
  },
  faculties: [
    {
      id: 'f1',
      name: 'Axborot texnologiyalari',
      code: 'AT',
      studentCount: 286,
      expectedToday: 280,
      attendedToday: 263,
      attendancePct: 94,
    },
    {
      id: 'f2',
      name: 'Iqtisodiyot va moliya',
      code: 'IM',
      studentCount: 341,
      expectedToday: 330,
      attendedToday: 294,
      attendancePct: 89,
    },
    {
      id: 'f3',
      name: 'Qurilish va arxitektura',
      code: 'QA',
      studentCount: 198,
      expectedToday: 190,
      attendedToday: 156,
      attendancePct: 82,
    },
    {
      id: 'f4',
      name: 'Filologiya',
      code: 'FL',
      studentCount: 124,
      expectedToday: 120,
      attendedToday: 91,
      attendancePct: 76,
    },
  ],
  audit: mockAudit.slice(0, 4),
};

export const dashboardHandlers: HttpHandler[] = [
  http.get(ADMIN_DASHBOARD_ENDPOINT, () => HttpResponse.json(mockAdminDashboard)),
];
