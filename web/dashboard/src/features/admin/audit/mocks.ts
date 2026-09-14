import { http, HttpResponse, type HttpHandler } from 'msw';
import { paginateMock } from '../shared/paginate';
import { AUDIT_ENDPOINT } from './api';
import { AUDIT_ACTIONS, type AuditAction, type AuditEntry } from './types';

const TUTOR_ID = '01a0a0e0-572d-74ad-96a4-4b1d36b09e80';
const TUTOR2_ID = '01a0a0e0-572d-7728-95d8-6833405c4f65';
const ADMIN_ID = '01a0a0df-3383-77dc-bc67-85c3bbbf4c73';

/** Backend `AuditEntryDto` shaklida (SPEC-SCREENS §9.8 syujeti; vaqtlar ISO, UTC). */
export const mockAudit: AuditEntry[] = [
  {
    id: 'a1',
    at: '2026-10-12T04:31:00+00:00',
    action: 'manualCheckIn',
    entityName: 'DailyAttendance',
    entityId: '01a0a0e1-0001-7000-8000-000000000001',
    reason: 'telefon zaryadi tugagan',
    changes: null,
    userId: TUTOR_ID,
    userName: 'Nodira Saidova',
    userRole: 'tutor',
  },
  {
    id: 'a2',
    at: '2026-10-12T03:54:00+00:00',
    action: 'radiusChanged',
    entityName: 'Company',
    entityId: '01a0a0e0-5734-7001-8000-000000000002',
    reason: null,
    changes: '{"RadiusM":{"old":200,"new":120}}',
    userId: TUTOR_ID,
    userName: 'Nodira Saidova',
    userRole: 'tutor',
  },
  {
    id: 'a3',
    at: '2026-10-11T12:10:00+00:00',
    action: 'applicationRejected',
    entityName: 'PracticeApplication',
    entityId: '01a0a0e1-0002-7000-8000-000000000003',
    reason: "shartnomada muhr yo'q",
    changes: null,
    userId: TUTOR2_ID,
    userName: 'Baxtiyor Rasulov',
    userRole: 'tutor',
  },
  {
    id: 'a4',
    at: '2026-10-11T10:02:00+00:00',
    action: 'settingsChanged',
    entityName: 'AppSetting',
    entityId: null,
    reason: null,
    changes: '{"geofenceRadius":{"old":"200","new":"250"}}',
    userId: ADMIN_ID,
    userName: 'Admin Adminov',
    userRole: 'admin',
  },
  {
    id: 'a5',
    at: '2026-10-10T07:20:00+00:00',
    action: 'gradeReverted',
    entityName: 'PracticeGrade',
    entityId: '01a0a0e1-0003-7000-8000-000000000005',
    reason: 'tyutor qarori admin tomonidan qaytarildi',
    changes: null,
    userId: ADMIN_ID,
    userName: 'Admin Adminov',
    userRole: 'admin',
  },
  {
    id: 'a6',
    at: '2026-10-10T06:00:00+00:00',
    action: 'loggedIn',
    entityName: 'User',
    entityId: ADMIN_ID,
    reason: null,
    changes: null,
    userId: ADMIN_ID,
    userName: 'Admin Adminov',
    userRole: 'admin',
  },
];

function isAuditAction(v: string): v is AuditAction {
  return (AUDIT_ACTIONS as readonly string[]).includes(v);
}

export const auditHandlers: HttpHandler[] = [
  http.get(AUDIT_ENDPOINT, ({ request }) => {
    const action = new URL(request.url).searchParams.get('action');
    if (action && !isAuditAction(action)) {
      return HttpResponse.json(
        {
          title: 'One or more validation errors occurred.',
          status: 400,
          errors: { Action: [`The value '${action}' is not valid for Action.`] },
        },
        { status: 400, headers: { 'Content-Type': 'application/problem+json' } },
      );
    }
    const items = action ? mockAudit.filter((a) => a.action === action) : mockAudit;
    return HttpResponse.json(
      paginateMock(request.url, items, (a) => [a.entityName, a.entityId, a.reason, a.userName]),
    );
  }),
];
