import { http, HttpResponse, type HttpHandler } from 'msw';
import { COMPANY_ENDPOINTS, STUDENT_ENDPOINTS } from '@/shared/api/endpoints';
import { problem, requireBearer } from '@/mocks/problem';
import {
  TIN_DIGITS,
  TIN_FORMAT_MESSAGE,
  type CompanyLookupDto,
  type PracticePlaceDto,
  type SubmitPlaceCommand,
} from './types';

/** SPEC-SCREENS §14 mock — kontrakt v2 shakli. */
export const mockPlace: PracticePlaceDto = {
  status: 'approved',
  comment: null,
  company: 'Tech Solutions MChJ',
  tin: '304512889',
  activity: "Dasturiy ta'minot ishlab chiqish",
  address: "Toshkent, Amir Temur ko'chasi 108",
  supervisorName: 'Islomov B.',
  supervisorPhone: '+998901234567',
  mentorName: 'Xolmatov S.',
  mentorPhone: '+998935551209',
  radiusM: 150,
  lat: 41.3111,
  lng: 69.2797,
  periodFrom: '2026-10-01',
  periodTo: '2026-11-15',
  contract: {
    fileId: '01a0a0e0-0000-7000-8000-000000000001',
    fileName: 'shartnoma_aliyev.pdf',
    pages: 2,
    sizeBytes: 1_887_436,
    uploadedAt: '2026-09-24T09:12:00+05:00',
    approvedAt: '2026-10-08T14:03:00+05:00',
    approvedBy: 'N. Saidova',
    templateUrl: '/files/shartnoma-shablon.docx',
  },
};

/**
 * Admin oldindan kiritgan korxonalar — talaba faqat shu STIR'lardan birini kiritadi.
 * Boshqa STIR → 404 ("korxona avval tizimga kiritilishi kerak").
 */
export const mockCompanies: CompanyLookupDto[] = [
  {
    id: '0a1b2c3d-0000-4000-8000-000000000001',
    name: mockPlace.company,
    tin: mockPlace.tin,
    activity: mockPlace.activity,
    address: mockPlace.address,
    lat: mockPlace.lat,
    lng: mockPlace.lng,
    radiusM: mockPlace.radiusM,
    supervisorName: mockPlace.supervisorName,
    supervisorPhone: mockPlace.supervisorPhone,
    mentorName: mockPlace.mentorName,
    mentorPhone: mockPlace.mentorPhone,
  },
  {
    id: '0a1b2c3d-0000-4000-8000-000000000002',
    name: 'Innova Digital MChJ',
    tin: '305881204',
    activity: 'Axborot texnologiyalari xizmatlari',
    address: "Toshkent, Shota Rustaveli ko'chasi 41",
    lat: 41.2856,
    lng: 69.2401,
    radiusM: 120,
    supervisorName: 'Qodirov J.',
    supervisorPhone: '+998907778899',
    mentorName: null,
    mentorPhone: null,
  },
];

/** Joriy amaliyot joyi (`null` — hali biriktirilmagan → GET 404). */
let currentPlace: PracticePlaceDto | null = mockPlace;

/** Testlarda boshlang'ich holatni tanlash (masalan `setMockPlace(null)` — joy yo'q). */
export function setMockPlace(place: PracticePlaceDto | null) {
  currentPlace = place;
}

export function resetPlaceMocks() {
  currentPlace = mockPlace;
}

const NOT_FOUND_MESSAGE =
  'Bu STIR bilan faol korxona topilmadi. Korxona avval tizimga kiritilishi kerak — tyutoringizga murojaat qiling.';
const NO_PLACE_MESSAGE =
  'Amaliyot joyi biriktirilmagan. Korxonani STIR orqali tanlang yoki tyutoringizga murojaat qiling.';
const PENDING_MESSAGE =
  "Arizangiz ko'rib chiqilmoqda — tyutor qaroridan keyin o'zgartirish mumkin.";
const ALREADY_APPROVED_MESSAGE =
  "Sizga allaqachon amaliyot joyi biriktirilgan. O'zgartirish uchun tyutoringizga murojaat qiling.";

const normalizeTin = (tin: string | null | undefined) => (tin ?? '').replace(/[\s-]/g, '');
const isValidTin = (tin: string) => tin.length === TIN_DIGITS && /^\d+$/.test(tin);
const findCompany = (tin: string) => mockCompanies.find((c) => c.tin === tin) ?? null;

/** Topilgan korxonadan `submitted` holatidagi amaliyot joyini yasaydi (backend kabi). */
function placeFromCompany(company: CompanyLookupDto): PracticePlaceDto {
  return {
    status: 'submitted',
    comment: null,
    company: company.name,
    tin: company.tin,
    activity: company.activity,
    address: company.address,
    supervisorName: company.supervisorName,
    supervisorPhone: company.supervisorPhone,
    mentorName: company.mentorName,
    mentorPhone: company.mentorPhone,
    radiusM: company.radiusM,
    lat: company.lat,
    lng: company.lng,
    periodFrom: mockPlace.periodFrom,
    periodTo: mockPlace.periodTo,
    contract: null,
  };
}

export const placeHandlers: HttpHandler[] = [
  http.get(STUDENT_ENDPOINTS.place, ({ request }) => {
    const denied = requireBearer(request);
    if (denied) return denied;
    if (!currentPlace) return problem(404, 'Topilmadi', NO_PLACE_MESSAGE);
    return HttpResponse.json(currentPlace);
  }),

  http.get(COMPANY_ENDPOINTS.lookup, ({ request }) => {
    const denied = requireBearer(request);
    if (denied) return denied;
    const tin = normalizeTin(new URL(request.url).searchParams.get('tin'));
    if (!isValidTin(tin)) {
      return problem(400, "Ma'lumot noto'g'ri", TIN_FORMAT_MESSAGE);
    }
    const company = findCompany(tin);
    if (!company) return problem(404, 'Topilmadi', NOT_FOUND_MESSAGE);
    return HttpResponse.json(company);
  }),

  http.post(STUDENT_ENDPOINTS.place, async ({ request }) => {
    const denied = requireBearer(request);
    if (denied) return denied;
    const body = (await request.json().catch(() => ({}))) as Partial<SubmitPlaceCommand>;
    const tin = normalizeTin(body.tin);
    if (!isValidTin(tin)) {
      return problem(400, "Ma'lumotlar noto'g'ri", "Kiritilgan ma'lumotlarda xatolik bor.", {
        errors: { Tin: [TIN_FORMAT_MESSAGE] },
      });
    }
    const company = findCompany(tin);
    if (!company) return problem(404, 'Topilmadi', NOT_FOUND_MESSAGE);
    if (currentPlace?.status === 'submitted') {
      return problem(409, 'Ziddiyat', PENDING_MESSAGE);
    }
    if (currentPlace?.status === 'approved' || currentPlace?.status === 'completed') {
      return problem(409, 'Ziddiyat', ALREADY_APPROVED_MESSAGE);
    }
    currentPlace = placeFromCompany(company);
    return HttpResponse.json(currentPlace, { status: 201 });
  }),
];
