import { http, HttpResponse, type HttpHandler } from 'msw';
import { problem } from '@/mocks/data';
import { isDiaryReviewable, type DiaryEntry, type DiaryReviewRequest, type DiaryStatus } from './types';

const SPEC_DIARIES: DiaryEntry[] = [
  {
    id: 'd-1',
    studentId: 's-341030',
    studentName: 'Aliyev Akmal',
    group: '412-22',
    date: '2026-10-11',
    submittedAt: '2026-10-11T12:42:00Z', // 17:42 Toshkent
    status: 'submitted',
    text: "Bugun jamoaning REST API loyihasida mijoz ro'yxatini qaytaruvchi endpoint ustida ishladim. Mentor bilan birga so'rov validatsiyasini ko'rib chiqdik, bo'sh maydonlar uchun xato matnlarini o'zbekchaga o'girdim. Tushdan keyin Postman'da 14 ta holatni sinab ko'rdim, ikkitasida 500 xato chiqdi — sababi bazadagi null qiymat ekan, mentor ko'rsatmasi bilan tuzatdim.",
    learned: 'So‘rov validatsiyasi va null qiymatlar bilan ishlash',
    files: [
      { name: 'ekran_surati_1.png', url: '/api/files/f-d1-1' },
      { name: 'postman_natija.pdf', url: '/api/files/f-d1-2' },
    ],
    score: null,
    comment: null,
    reviewedAt: null,
  },
  {
    id: 'd-2',
    studentId: 's-341033',
    studentName: 'Yusupova Nilufar',
    group: '413-22',
    date: '2026-10-11',
    submittedAt: '2026-10-11T11:58:00Z', // 16:58
    status: 'seen',
    text: "Marketing bo'limida oktabr oyi uchun kontent kalendarini tuzishda qatnashdim. Uch xil auditoriya uchun post matnlari yozdim, muharrir ikkitasini tanladi. Statistika panelidan o'tgan hafta ko'rsatkichlarini yig'ib, qisqa jadval tayyorladim.",
    learned: null,
    files: [{ name: 'kontent_kalendar.xlsx', url: '/api/files/f-d2-1' }],
    score: null,
    comment: null,
    reviewedAt: null,
  },
  {
    id: 'd-3',
    studentId: 's-341031',
    studentName: 'Karimov Bekzod',
    group: '412-22',
    date: '2026-10-11',
    submittedAt: '2026-10-11T13:31:00Z', // 18:31
    status: 'rewrite',
    text: "Bugun bank filialida hujjatlar bilan ishladim. Ishladim va o'rgandim.",
    learned: null,
    files: [],
    score: null,
    comment: 'Batafsil yozing: qanday hujjatlar, nima qildingiz.',
    reviewedAt: '2026-10-11T14:00:00Z',
  },
];

const STATUSES: DiaryStatus[] = ['submitted', 'seen', 'rewrite', 'approved'];

export let mockDiaries: DiaryEntry[] = [];
export function resetDiariesMock() {
  mockDiaries = SPEC_DIARIES.map((d) => ({ ...d, files: d.files.map((f) => ({ ...f })) }));
}
resetDiariesMock();

const validation = (errors: Record<string, string[]>) =>
  HttpResponse.json(problem(400, 'One or more validation errors occurred.', '', { errors }), {
    status: 400,
  });

export const diariesHandlers: HttpHandler[] = [
  http.get('/api/tutor/diaries', ({ request }) => {
    const status = new URL(request.url).searchParams.get('status');
    if (status !== null && !(STATUSES as string[]).includes(status))
      return validation({ Status: [`The value '${status}' is not valid for Status.`] });
    return HttpResponse.json(mockDiaries.filter((d) => status === null || d.status === status));
  }),

  http.post('/api/tutor/diaries/:id/review', async ({ params, request }) => {
    const entry = mockDiaries.find((d) => d.id === params['id']);
    if (!entry)
      return HttpResponse.json(problem(404, 'Topilmadi', 'Kundalik yozuvi topilmadi.'), {
        status: 404,
      });
    const body = (await request.json().catch(() => ({}))) as Partial<DiaryReviewRequest>;
    if (body.action !== 'approve' && body.action !== 'score' && body.action !== 'rewrite')
      return validation({ Action: ['Amal: approve, score yoki rewrite.'] });
    if (body.score !== undefined && (body.score < 1 || body.score > 5))
      return validation({ Score: ["Ball 1–5 oralig'ida bo'lishi kerak."] });
    if (body.action === 'score' && body.score === undefined)
      return validation({ Score: ['Baholashda ball majburiy.'] });
    if (body.action === 'rewrite' && !body.comment?.trim())
      return validation({ Comment: ['Qayta yozish sababi (izoh) majburiy.'] });
    if (!isDiaryReviewable(entry))
      return HttpResponse.json(
        problem(409, 'Ziddiyat', `Hisobot allaqachon ko'rib chiqilgan (holat: ${entry.status}).`),
        { status: 409 },
      );
    const comment = body.comment?.trim() || null;
    if (body.action === 'rewrite') {
      entry.status = 'rewrite';
      entry.score = null;
    } else {
      entry.status = 'approved';
      entry.score = body.score ?? entry.score;
    }
    entry.comment = comment;
    entry.reviewedAt = new Date().toISOString();
    return HttpResponse.json(entry);
  }),
];
