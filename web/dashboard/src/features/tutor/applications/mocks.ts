import { http, HttpResponse, type HttpHandler } from 'msw';
import { problem } from '@/mocks/data';
import {
  DECISION_TO_STATUS,
  RADIUS_MAX_M,
  RADIUS_MIN_M,
  RADIUS_STEP_M,
  type ApplicationCounts,
  type ApplicationDecisionRequest,
  type ApplicationDetail,
  type ApplicationListResponse,
  type ApplicationStatus,
  type ApplicationSummary,
} from './types';

const HOUR = 3_600_000;
const DAY = 24 * HOUR;
/** Mock "hozir" — `waited` yorliqlari (2 soat oldin …) shu nuqtaga nisbatan. */
const now = () => Date.now();
const ago = (ms: number) => new Date(now() - ms).toISOString();

interface Seed {
  id: string;
  name: string;
  group: string;
  company: string;
  status: ApplicationStatus;
  hemisId: string;
  /** Yuborilganidan beri o'tgan vaqt (ms). */
  submittedAgo: number;
  decidedAgo?: number;
  comment?: string;
  revisionCount?: number;
}

/** SPEC-SCREENS §4 mock: 4 ta ariza + `detailCommon`; dizayndagi 7 yangi arizaga qo'shimchalar. */
const SEEDS: Seed[] = [
  { id: 'app-1', name: 'Aliyev Akmal', group: '412-22', company: 'Tech Solutions MChJ', status: 'submitted', hemisId: '341030', submittedAgo: 2 * HOUR },
  { id: 'app-2', name: 'Mirzayev Jasur', group: '412-22', company: 'Uzbekinvest AJ', status: 'submitted', hemisId: '341031', submittedAgo: DAY },
  { id: 'app-3', name: 'Qodirova Oysha', group: '413-22', company: 'Buxoro Tekstil MChJ', status: 'revisionNeeded', hemisId: '341032', submittedAgo: 4 * DAY, decidedAgo: 3 * DAY, comment: "Shartnomada imzo yo'q", revisionCount: 1 },
  { id: 'app-4', name: 'Nazarov Firdavs', group: '413-22', company: 'Qurilish Trest 12', status: 'approved', hemisId: '341033', submittedAgo: 6 * DAY, decidedAgo: 4 * DAY, comment: "Hujjatlar to'liq" },
  { id: 'app-5', name: 'Saidov Alisher', group: '412-22', company: 'Artel Electronics', status: 'submitted', hemisId: '341036', submittedAgo: DAY + HOUR },
  { id: 'app-6', name: 'Tursunova Malika', group: '413-22', company: 'Beeline Uzbekistan', status: 'submitted', hemisId: '341037', submittedAgo: 2 * DAY },
  { id: 'app-7', name: 'Umarov Farrux', group: '412-22', company: 'Uztelecom', status: 'submitted', hemisId: '341038', submittedAgo: 2 * DAY + HOUR },
  { id: 'app-8', name: 'Valiyeva Zulfiya', group: '413-22', company: 'Kapitalbank', status: 'submitted', hemisId: '341039', submittedAgo: 3 * DAY },
  { id: 'app-9', name: 'Xolmatov Sanjar', group: '412-22', company: 'UzAuto Motors', status: 'submitted', hemisId: '341040', submittedAgo: 4 * DAY },
  { id: 'app-10', name: 'Ergashev Sherzod', group: '413-22', company: 'Payme', status: 'revisionNeeded', hemisId: '341041', submittedAgo: 6 * DAY, decidedAgo: 5 * DAY, comment: 'STIR xato', revisionCount: 1 },
  { id: 'app-11', name: 'Boboyeva Madina', group: '412-22', company: 'Click', status: 'rejected', hemisId: '341042', submittedAgo: 12 * DAY, decidedAgo: 10 * DAY, comment: "Korxona yo'nalishga mos emas" },
];

type MockApplication = ApplicationDetail;

function fromSeed(s: Seed): MockApplication {
  return {
    id: s.id,
    studentId: `s-${s.hemisId}`,
    name: s.name,
    group: s.group,
    course: 3,
    hemisId: s.hemisId,
    company: s.company,
    status: s.status,
    submittedAt: ago(s.submittedAgo),
    decidedAt: s.decidedAgo === undefined ? null : ago(s.decidedAgo),
    coords: { lat: 41.3111, lng: 69.2797 },
    radiusM: 150,
    companyDetails: {
      name: s.company,
      tin: '304512889',
      activity: "Dasturiy ta'minot ishlab chiqish",
      address: "Toshkent, Amir Temur ko'chasi 108",
      supervisorName: 'Islomov B.',
      supervisorPhone: '+998901234567',
      mentorName: 'Xolmatov S.',
      mentorPhone: '+998935551209',
    },
    contract:
      s.id === 'app-11'
        ? null
        : {
            name: `shartnoma_${s.name.split(' ')[0]!.toLowerCase()}.pdf`,
            pages: 2,
            sizeBytes: 1_887_437, // 1,8 MB
            url: `/api/files/${s.id}`,
          },
    comment: s.comment ?? null,
    checklist: s.status === 'approved' ? [0, 1, 2, 3, 4, 5, 6] : [],
    revisionCount: s.revisionCount ?? 0,
  };
}

/** Testlar orasida `resetTutorMocks()` bilan tiklanadi. */
export let mockApplications: MockApplication[] = [];

export function resetApplicationsMock() {
  mockApplications = SEEDS.map(fromSeed);
}
resetApplicationsMock();

const summaryOf = (a: MockApplication): ApplicationSummary => ({
  id: a.id,
  studentId: a.studentId,
  name: a.name,
  group: a.group,
  course: a.course,
  hemisId: a.hemisId,
  company: a.company,
  status: a.status,
  submittedAt: a.submittedAt,
  decidedAt: a.decidedAt,
});

function counts(): ApplicationCounts {
  const c: ApplicationCounts = { submitted: 0, revisionNeeded: 0, approved: 0, rejected: 0 };
  for (const a of mockApplications) c[a.status] += 1;
  // Dizayndagi "Tasdiqlangan 24" — mock ro'yxatda bittasi; qolganlari boshqa davrda deb hisoblaymiz ❓.
  c.approved += 23;
  return c;
}

const STATUSES: ApplicationStatus[] = ['submitted', 'revisionNeeded', 'approved', 'rejected'];

const validation = (errors: Record<string, string[]>) =>
  HttpResponse.json(problem(400, 'One or more validation errors occurred.', '', { errors }), {
    status: 400,
  });

export const applicationsHandlers: HttpHandler[] = [
  http.get('/api/tutor/applications', ({ request }) => {
    const status = new URL(request.url).searchParams.get('status');
    if (status !== null && !(STATUSES as string[]).includes(status))
      return validation({ Status: [`The value '${status}' is not valid for Status.`] });
    const body: ApplicationListResponse = {
      counts: counts(),
      items: mockApplications.filter((a) => status === null || a.status === status).map(summaryOf),
    };
    return HttpResponse.json(body);
  }),

  http.get('/api/tutor/applications/:id', ({ params }) => {
    const app = mockApplications.find((a) => a.id === params['id']);
    if (!app)
      return HttpResponse.json(problem(404, 'Topilmadi', 'Ariza topilmadi.'), { status: 404 });
    return HttpResponse.json(app);
  }),

  http.post('/api/tutor/applications/:id/decision', async ({ params, request }) => {
    const app = mockApplications.find((a) => a.id === params['id']);
    if (!app)
      return HttpResponse.json(problem(404, 'Topilmadi', 'Ariza topilmadi.'), { status: 404 });
    const body = (await request.json().catch(() => ({}))) as Partial<ApplicationDecisionRequest>;
    if (!body.decision || !(body.decision in DECISION_TO_STATUS))
      return validation({ Decision: ['Qaror: approve, return yoki reject.'] });
    if (body.decision === 'approve') {
      const r = body.radiusM;
      if (r === undefined || r === null) return validation({ RadiusM: ['Tasdiqlashda radius majburiy.'] });
      if (r < RADIUS_MIN_M || r > RADIUS_MAX_M || r % RADIUS_STEP_M !== 0)
        return validation({ RadiusM: [`Radius ${RADIUS_MIN_M}–${RADIUS_MAX_M} m, ${RADIUS_STEP_M} m qadam.`] });
    } else if (!body.comment?.trim()) {
      return validation({ Comment: ['Qaytarish/rad etish sababi (izoh) majburiy.'] });
    }
    if (app.status !== 'submitted')
      return HttpResponse.json(
        problem(409, 'Ziddiyat', `Ariza allaqachon hal qilingan — (holat: ${app.status}).`),
        { status: 409 },
      );
    app.status = DECISION_TO_STATUS[body.decision];
    app.decidedAt = new Date(now()).toISOString();
    app.comment = body.comment?.trim() || null;
    if (body.decision === 'approve') {
      app.radiusM = body.radiusM!;
      app.checklist = [...new Set(body.checklist ?? [])].sort((a, b) => a - b);
    }
    if (body.decision === 'return') app.revisionCount += 1;
    return HttpResponse.json({ id: app.id, status: app.status });
  }),
];
