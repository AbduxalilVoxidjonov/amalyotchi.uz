import { http, HttpResponse, type HttpHandler } from 'msw';
import { problem } from '@/mocks/data';
import type { LeaveDecisionRequest, LeaveRequest, LeaveRequestStatus } from './types';

/** SPEC-SCREENS §9.1 mock (4 qator). */
const SPEC: LeaveRequest[] = [
  {
    id: 'lr-1',
    studentId: 's-341032',
    studentName: 'Sobirov Diyor',
    group: '413-22',
    dateFrom: '2026-10-14',
    dateTo: '2026-10-14',
    reason: 'Kasallik — poliklinika spravkasi',
    document: { name: 'spravka.pdf', url: '/api/files/f-lr-1' },
    status: 'pending',
    comment: null,
    createdAt: '2026-10-12T04:10:00Z',
    decidedAt: null,
  },
  {
    id: 'lr-2',
    studentId: 's-341031',
    studentName: 'Karimov Bekzod',
    group: '412-22',
    dateFrom: '2026-10-15',
    dateTo: '2026-10-16',
    reason: 'Oilaviy sabab',
    document: { name: 'ariza.pdf', url: null },
    status: 'pending',
    comment: null,
    createdAt: '2026-10-12T03:00:00Z',
    decidedAt: null,
  },
  {
    id: 'lr-3',
    studentId: 's-341033',
    studentName: 'Yusupova Nilufar',
    group: '413-22',
    dateFrom: '2026-10-08',
    dateTo: '2026-10-08',
    reason: 'Universitet konferensiyasi',
    document: { name: 'xat.pdf', url: '/api/files/f-lr-3' },
    status: 'approved',
    comment: 'Tasdiqlandi',
    createdAt: '2026-10-06T05:00:00Z',
    decidedAt: '2026-10-06T09:00:00Z',
  },
  {
    id: 'lr-4',
    studentId: 's-341034',
    studentName: 'Rahimov Sardor',
    group: '412-22',
    dateFrom: '2026-10-06',
    dateTo: '2026-10-06',
    reason: "Sabab ko'rsatilmagan",
    document: null,
    status: 'rejected',
    comment: 'Sabab yetarli emas',
    createdAt: '2026-10-05T05:00:00Z',
    decidedAt: '2026-10-05T10:00:00Z',
  },
];

const STATUSES: LeaveRequestStatus[] = ['pending', 'approved', 'rejected'];

export let mockLeaveRequests: LeaveRequest[] = [];
export function resetLeaveRequestsMock() {
  mockLeaveRequests = SPEC.map((r) => ({ ...r, document: r.document && { ...r.document } }));
}
resetLeaveRequestsMock();

const validation = (errors: Record<string, string[]>) =>
  HttpResponse.json(problem(400, 'One or more validation errors occurred.', '', { errors }), {
    status: 400,
  });

export const leaveRequestsHandlers: HttpHandler[] = [
  http.get('/api/tutor/leave-requests', ({ request }) => {
    const status = new URL(request.url).searchParams.get('status');
    if (status !== null && !(STATUSES as string[]).includes(status))
      return validation({ Status: [`The value '${status}' is not valid for Status.`] });
    return HttpResponse.json(
      mockLeaveRequests.filter((r) => status === null || r.status === status),
    );
  }),

  http.post('/api/tutor/leave-requests/:id/decision', async ({ params, request }) => {
    const item = mockLeaveRequests.find((r) => r.id === params['id']);
    if (!item)
      return HttpResponse.json(problem(404, 'Topilmadi', "Ruxsat so'rovi topilmadi."), {
        status: 404,
      });
    const body = (await request.json().catch(() => ({}))) as Partial<LeaveDecisionRequest>;
    if (body.decision !== 'approve' && body.decision !== 'reject')
      return validation({ Decision: ['Qaror: approve yoki reject.'] });
    if (item.status !== 'pending')
      return HttpResponse.json(
        problem(409, 'Ziddiyat', `So'rov allaqachon hal qilingan (holat: ${item.status}).`),
        { status: 409 },
      );
    item.status = body.decision === 'approve' ? 'approved' : 'rejected';
    item.comment = body.comment?.trim() || null;
    item.decidedAt = new Date().toISOString();
    return HttpResponse.json(item);
  }),
];
