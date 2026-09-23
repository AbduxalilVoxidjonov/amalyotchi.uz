import { http, HttpResponse, type HttpHandler } from 'msw';
import { STUDENT_ENDPOINTS } from '@/shared/api/endpoints';
import { problem, requireBearer } from '@/mocks/problem';
import { markDiarySubmitted, mockToday } from '@/features/today/mocks';
import {
  DIARY_MAX_FILES,
  DIARY_MIN_CHARS,
  DIARY_PDF_REQUIRED_MESSAGE,
  isPdfFile,
  rewriteFilesFor,
  type DiaryEntryDto,
} from './types';

/** SPEC-SCREENS §6 — faqat "AA" (o'z yozuvlari) + qo'shimcha eski yozuvlar. Kontrakt v2 shakli. */
function initialEntries(): DiaryEntryDto[] {
  return [
    {
      id: 'd-32',
      date: '2026-10-11',
      submittedAt: '2026-10-11T12:42:00Z',
      status: 'submitted',
      text: "Bugun jamoaning REST API loyihasida mijoz ro'yxatini qaytaruvchi endpoint ustida ishladim. Mentor bilan birga so'rov validatsiyasini ko'rib chiqdik, bo'sh maydonlar uchun xato matnlarini o'zbekchaga o'girdim. Tushdan keyin Postman'da 14 ta holatni sinab ko'rdim, ikkitasida 500 xato chiqdi — sababi bazadagi null qiymat ekan, mentor ko'rsatmasi bilan tuzatdim.",
      learned: 'FluentValidation xabarlarini lokalizatsiya qilish',
      files: [
        { id: 'f-1', name: 'ekran_surati_1.png', url: '/files/ekran_surati_1.png' },
        { id: 'f-2', name: 'postman_natija.pdf', url: '/files/postman_natija.pdf' },
      ],
      score: null,
      comment: null,
    },
    {
      id: 'd-31',
      date: '2026-10-10',
      submittedAt: '2026-10-10T13:05:00Z',
      status: 'approved',
      text: "Mijozlar jadvaliga indeks qo'shish bo'yicha mentor topshirig'ini bajardim. Avval so'rov rejasini (EXPLAIN) o'rganib chiqdik, keyin ikki ustunli indeks yaratdik va sekin so'rov 1,8 soniyadan 90 ms ga tushdi. Natijani jamoa chatida hisobot qilib yozdim.",
      learned: null,
      files: [],
      score: 5,
      comment: null,
    },
    {
      id: 'd-30',
      date: '2026-10-09',
      submittedAt: '2026-10-09T13:31:00Z',
      status: 'rewrite',
      text: 'Bugun bank filialida hujjatlar bilan ishladim. Ishladim va o‘rgandim. Kun davomida turli masalalar bilan shug‘ullandim va ko‘p narsani o‘rgandim, ertaga davom etamiz degan xulosaga keldik.',
      learned: null,
      files: [],
      score: null,
      comment: 'Aniq vazifalar va natijani yozing — umumiy gaplar yetarli emas.',
    },
    {
      id: 'd-29',
      date: '2026-10-08',
      submittedAt: '2026-10-08T13:10:00Z',
      status: 'seen',
      text: "Mentor bilan birga CI pipeline'ni sozladik: testlar har push'da ishga tushadi, xato bo'lsa jamoa chatiga xabar keladi. Bir nechta eski testlarni yangiladim va lint xatolarini tuzatdim, natijada pipeline yashil bo'ldi.",
      learned: null,
      files: [],
      score: null,
      comment: null,
    },
  ];
}

export let mockDiary: DiaryEntryDto[] = initialEntries();
let nextId = 33;

export function resetDiaryMocks() {
  mockDiary = initialEntries();
  nextId = 33;
}

export const diaryHandlers: HttpHandler[] = [
  http.get(STUDENT_ENDPOINTS.diary, ({ request }) => {
    return requireBearer(request) ?? HttpResponse.json(mockDiary);
  }),

  http.post(STUDENT_ENDPOINTS.diary, async ({ request }) => {
    const denied = requireBearer(request);
    if (denied) return denied;
    const form = await request.formData().catch(() => null);
    const text = String(form?.get('text') ?? '').trim();
    const learned = form?.get('learned');
    const files = (form?.getAll('files') ?? []).filter((f): f is File => f instanceof File);
    // Backend `Resubmit`: bugungi yozuv `rewrite` holatida bo'lsa qayta yoziladi, fayllari saqlanadi.
    const rewriting = mockDiary.find((e) => e.date === mockToday.date && e.status === 'rewrite');
    const errors: Record<string, string[]> = {};
    if (text.length < DIARY_MIN_CHARS) {
      errors['Text'] = ['Hisobot matni juda qisqa — minimal uzunlik sozlamada belgilangan.'];
    }
    if (files.length > DIARY_MAX_FILES) {
      errors['Files'] = [`Ko'pi bilan ${DIARY_MAX_FILES} ta fayl biriktirish mumkin.`];
    }
    if (Object.keys(errors).length > 0) {
      return problem(400, "Ma'lumotlar noto'g'ri", "Kiritilgan ma'lumotlarda xatolik bor.", {
        errors,
      });
    }
    if (mockToday.diary.submittedToday && !rewriting) {
      return problem(409, 'Ziddiyat', 'Bugungi hisobot allaqachon yuborilgan.');
    }
    const kept = rewriteFilesFor(mockDiary, mockToday.date);
    if (mockToday.diary.pdfRequired && !files.some(isPdfFile) && !kept.some(isPdfFile)) {
      return problem(400, "Ma'lumotlar noto'g'ri", DIARY_PDF_REQUIRED_MESSAGE, {
        errors: { Files: [DIARY_PDF_REQUIRED_MESSAGE] },
      });
    }
    const entry: DiaryEntryDto = {
      id: rewriting?.id ?? `d-${nextId++}`,
      date: mockToday.date,
      submittedAt: new Date().toISOString(),
      status: 'submitted',
      text,
      learned: typeof learned === 'string' && learned.trim() ? learned.trim() : null,
      files: [
        ...kept,
        ...files.map((f, i) => ({ id: `f-new-${i}`, name: f.name, url: `/files/${f.name}` })),
      ],
      score: null,
      comment: null,
    };
    mockDiary = [entry, ...mockDiary.filter((e) => e.id !== entry.id)];
    markDiarySubmitted();
    return HttpResponse.json(entry, { status: 201 });
  }),
];
