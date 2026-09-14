import { http, HttpResponse, type HttpHandler } from 'msw';
import { STUDENT_ENDPOINTS } from '@/shared/api/endpoints';
import { problem, requireBearer } from '@/mocks/problem';
import { LEAVE_REASON_MIN, type LeaveRequestCreate, type LeaveRequestDto } from './types';

/** SPEC-SCREENS §15 mock — kontrakt v2 shakli. */
function initialRequests(): LeaveRequestDto[] {
  return [
    {
      id: 'lr-3',
      dateFrom: '2026-10-14',
      dateTo: '2026-10-14',
      reason: 'Kasallik — poliklinika spravkasi',
      status: 'pending',
      comment: null,
      document: { name: 'spravka.pdf', url: null },
      createdAt: '2026-10-12T08:15:00+05:00',
    },
    {
      id: 'lr-2',
      dateFrom: '2026-10-08',
      dateTo: '2026-10-08',
      reason: 'Universitet konferensiyasi',
      status: 'approved',
      comment: 'Tasdiqlandi',
      document: { name: 'xat.pdf', url: '/files/xat.pdf' },
      createdAt: '2026-10-06T10:00:00+05:00',
    },
    {
      id: 'lr-1',
      dateFrom: '2026-10-02',
      dateTo: '2026-10-02',
      reason: "Sabab ko'rsatilmagan — shaxsiy",
      status: 'rejected',
      comment: 'Sabab yetarli emas',
      document: null,
      createdAt: '2026-10-01T09:30:00+05:00',
    },
  ];
}

export let mockLeaveRequests: LeaveRequestDto[] = initialRequests();
let nextId = 4;

export function resetLeaveMocks() {
  mockLeaveRequests = initialRequests();
  nextId = 4;
}

const DATE_RE = /^\d{4}-\d{2}-\d{2}$/;

export const leaveHandlers: HttpHandler[] = [
  http.get(STUDENT_ENDPOINTS.leaveRequests, ({ request }) => {
    return requireBearer(request) ?? HttpResponse.json(mockLeaveRequests);
  }),

  http.post(STUDENT_ENDPOINTS.leaveRequests, async ({ request }) => {
    const denied = requireBearer(request);
    if (denied) return denied;
    const body = (await request.json().catch(() => ({}))) as Partial<LeaveRequestCreate>;
    const errors: Record<string, string[]> = {};
    if (!body.dateFrom || !DATE_RE.test(body.dateFrom)) {
      errors['DateFrom'] = ['Boshlanish sanasini kiriting.'];
    }
    if (!body.dateTo || !DATE_RE.test(body.dateTo))
      errors['DateTo'] = ['Tugash sanasini kiriting.'];
    if (body.dateFrom && body.dateTo && body.dateTo < body.dateFrom) {
      errors['DateTo'] = ["Tugash sanasi boshlanish sanasidan oldin bo'lishi mumkin emas."];
    }
    if (!body.reason || body.reason.trim().length < LEAVE_REASON_MIN) {
      errors['Reason'] = [`Sabab kamida ${LEAVE_REASON_MIN} belgidan iborat bo'lishi kerak.`];
    }
    if (Object.keys(errors).length > 0) {
      return problem(400, "Ma'lumotlar noto'g'ri", "Kiritilgan ma'lumotlarda xatolik bor.", {
        errors,
      });
    }
    const overlaps = mockLeaveRequests.some(
      (l) => l.status !== 'rejected' && l.dateFrom <= body.dateTo! && l.dateTo >= body.dateFrom!,
    );
    if (overlaps) {
      return problem(409, 'Ziddiyat', "Bu sanalar uchun ruxsat so'rovi allaqachon bor.");
    }
    const created: LeaveRequestDto = {
      id: `lr-${nextId++}`,
      dateFrom: body.dateFrom!,
      dateTo: body.dateTo!,
      reason: body.reason!.trim(),
      status: 'pending',
      comment: null,
      document: body.attachmentName ? { name: body.attachmentName, url: null } : null,
      createdAt: new Date().toISOString(),
    };
    mockLeaveRequests = [created, ...mockLeaveRequests];
    return HttpResponse.json(created, { status: 201 });
  }),
];
