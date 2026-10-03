import { http, HttpResponse, type HttpHandler } from 'msw';
import { STUDENT_ENDPOINTS } from '@/shared/api/endpoints';
import { accountFromRequest, mockStudent, mockStudentNew, type MockAccount } from '@/mocks/data';
import { problem, requireBearer } from '@/mocks/problem';
import { MOCK_AUTUMN_PERIOD } from '@/features/period/mocks';
import { MOCK_PD_SUMMER_PERIOD } from '@/features/period-days/mocks';
import { addDays, tashkentToday, validateWorkHours } from './lib';
import type {
  StudentProfileDto,
  StudentProfilePracticeDto,
  StudentWorkHoursDto,
  UpdateWorkHoursRequest,
} from './types';

/** Kuzgi (faol, sukut) davr — bosh ekran davr tanlagichidagi "Kuzgi amaliyot 2026" bilan bir xil id/nom. */
export const mockAutumnPractice: StudentProfilePracticeDto = {
  period: {
    id: MOCK_AUTUMN_PERIOD.id,
    name: MOCK_AUTUMN_PERIOD.name,
    status: 'active',
    startDate: MOCK_AUTUMN_PERIOD.startDate,
    endDate: MOCK_AUTUMN_PERIOD.endDate,
  },
  company: {
    id: 'dddddddd-0000-4000-8000-000000000001',
    name: 'Tech Solutions MChJ',
    address: "Toshkent sh., Mirzo Ulug'bek tumani, Buyuk Ipak Yo'li 24",
  },
  elapsedWorkDays: 17,
  attendancePct: 88.2,
  suspiciousDays: 1,
  total: 62.5,
  grade: 4,
  finalized: false,
};

/** Yozgi (yopilgan, yakuniy baholangan) davr — bosh ekrandagi "Yozgi amaliyot 2026" bilan bir xil id/nom. */
export const mockSummerPractice: StudentProfilePracticeDto = {
  period: {
    id: MOCK_PD_SUMMER_PERIOD.id,
    name: MOCK_PD_SUMMER_PERIOD.name,
    status: 'closed',
    startDate: MOCK_PD_SUMMER_PERIOD.startDate,
    endDate: MOCK_PD_SUMMER_PERIOD.endDate,
  },
  company: {
    id: 'dddddddd-0000-4000-8000-000000000002',
    name: 'Digital Soft MChJ',
    address: "Toshkent sh., Yunusobod tumani, Amir Temur ko'chasi 108",
  },
  elapsedWorkDays: 36,
  attendancePct: 94.4,
  suspiciousDays: 0,
  total: 91,
  grade: 5,
  finalized: true,
};

/** Aliyev Akmal — ikki davr (kuzgi faol + yozgi yakunlangan), korxona, tyutor bor (Telegram bog'langan). */
export const mockProfile: StudentProfileDto = {
  id: mockStudent.id,
  fullName: mockStudent.fullName,
  hemisId: '341030',
  phoneNumber: '+998901112233',
  faculty: 'Axborot texnologiyalari fakulteti',
  department: 'Dasturiy injiniring kafedrasi',
  direction: 'Dasturiy injiniring',
  group: '412-22',
  course: 3,
  tutor: { fullName: 'Saidova Nodira', phoneNumber: '+998901234567' },
  telegramLinked: true,
  hasPassword: true,
  mustChangePassword: false,
  practice: mockAutumnPractice,
  practices: [mockAutumnPractice, mockSummerPractice],
  workHours: {
    start: null,
    end: null,
    effectiveFrom: null,
    todayStart: '09:00',
    todayEnd: '18:00',
    periodStart: '09:00',
    periodEnd: '18:00',
  },
};

/** Bitta davrli variant (faqat kuzgi). */
export const mockProfileSinglePractice: StudentProfileDto = {
  ...mockProfile,
  practices: [mockAutumnPractice],
};

/** Karimova Dilnoza — davr va tyutor biriktirilmagan, Telegram bog'lanmagan. */
export const mockProfileNoPractice: StudentProfileDto = {
  id: mockStudentNew.id,
  fullName: mockStudentNew.fullName,
  hemisId: '341031',
  phoneNumber: null,
  faculty: 'Axborot texnologiyalari fakulteti',
  department: 'Kompyuter tizimlari kafedrasi',
  direction: 'Kompyuter injiniringi',
  group: '411-22',
  course: 3,
  tutor: null,
  telegramLinked: false,
  hasPassword: true,
  mustChangePassword: true,
  practice: null,
  practices: [],
  workHours: {
    start: null,
    end: null,
    effectiveFrom: null,
    todayStart: '09:00',
    todayEnd: '18:00',
    periodStart: null,
    periodEnd: null,
  },
};

/** PUT work-hours bilan o'zgargan ish vaqti (talaba id → workHours); testlarda har testdan keyin tozalanadi. */
const mockWorkHours = new Map<string, StudentWorkHoursDto>();

export function resetProfileMocks() {
  mockWorkHours.clear();
}

function profileFor(account: MockAccount | undefined): StudentProfileDto | null {
  const base =
    account?.user.id === mockStudentNew.id
      ? mockProfileNoPractice
      : account?.user.id === mockStudent.id || account === undefined
        ? mockProfile
        : null;
  if (!base) return null;
  return {
    ...base,
    mustChangePassword: account?.mustChangePassword ?? false,
    ...(base.workHours && { workHours: mockWorkHours.get(base.id) ?? base.workHours }),
  };
}

/** Server qoidalari: ikkalasi null (davr vaqtiga qaytish) yoki ikkalasi ham; ketish > kelish, ≥ 1 soat. */
function workHoursErrors(body: UpdateWorkHoursRequest): Record<string, string[]> | null {
  if (body.start === null && body.end === null) return null;
  if (body.start === null || body.end === null) {
    return {
      [body.start === null ? 'start' : 'end']: ['Kelish va ketish vaqti birga berilishi kerak.'],
    };
  }
  const e = validateWorkHours(body.start, body.end);
  const out: Record<string, string[]> = {};
  if (e.start) out.start = [e.start];
  if (e.end) out.end = [e.end];
  return Object.keys(out).length > 0 ? out : null;
}

export const profileHandlers: HttpHandler[] = [
  http.get(STUDENT_ENDPOINTS.profile, ({ request }) => {
    const unauth = requireBearer(request);
    if (unauth) return unauth;
    const profile = profileFor(accountFromRequest(request));
    if (!profile) return problem(403, "Ruxsat yo'q", 'Faqat talabalar uchun.');
    return HttpResponse.json(profile);
  }),
  http.put(STUDENT_ENDPOINTS.profileWorkHours, async ({ request }) => {
    const unauth = requireBearer(request);
    if (unauth) return unauth;
    const profile = profileFor(accountFromRequest(request));
    if (!profile?.workHours) return problem(403, "Ruxsat yo'q", 'Faqat talabalar uchun.');
    const body = (await request.json()) as UpdateWorkHoursRequest;
    const errors = workHoursErrors(body);
    if (errors) {
      return problem(400, "Ma'lumot noto'g'ri", "Ish vaqti noto'g'ri.", { errors });
    }
    // Bugungi oyna o'zgarmaydi — yangi qiymat ertadan kuchga kiradi.
    const next: StudentWorkHoursDto = {
      ...profile.workHours,
      start: body.start,
      end: body.end,
      effectiveFrom: addDays(tashkentToday(), 1),
    };
    mockWorkHours.set(profile.id, next);
    return HttpResponse.json(next);
  }),
];
