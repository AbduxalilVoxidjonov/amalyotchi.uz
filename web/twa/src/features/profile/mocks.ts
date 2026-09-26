import { http, HttpResponse, type HttpHandler } from 'msw';
import { STUDENT_ENDPOINTS } from '@/shared/api/endpoints';
import { accountFromRequest, mockStudent, mockStudentNew, type MockAccount } from '@/mocks/data';
import { problem, requireBearer } from '@/mocks/problem';
import { MOCK_AUTUMN_PERIOD } from '@/features/period/mocks';
import { MOCK_PD_SUMMER_PERIOD } from '@/features/period-days/mocks';
import type { StudentProfileDto, StudentProfilePracticeDto } from './types';

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
};

function profileFor(account: MockAccount | undefined): StudentProfileDto | null {
  const base =
    account?.user.id === mockStudentNew.id
      ? mockProfileNoPractice
      : account?.user.id === mockStudent.id || account === undefined
        ? mockProfile
        : null;
  return base && { ...base, mustChangePassword: account?.mustChangePassword ?? false };
}

export const profileHandlers: HttpHandler[] = [
  http.get(STUDENT_ENDPOINTS.profile, ({ request }) => {
    const unauth = requireBearer(request);
    if (unauth) return unauth;
    const profile = profileFor(accountFromRequest(request));
    if (!profile) return problem(403, "Ruxsat yo'q", 'Faqat talabalar uchun.');
    return HttpResponse.json(profile);
  }),
];
