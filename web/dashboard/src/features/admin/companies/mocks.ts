import { http, HttpResponse, type HttpHandler } from 'msw';
import { problemResponse } from '../shared/mockProblem';
import { paginateMock } from '../shared/paginate';
import type { ImportResult } from '../shared/types';
import { COMPANIES_ENDPOINT, COMPANIES_TEMPLATE_FILE_NAME } from './api';
import type {
  ApplicationStatus,
  Company,
  CompanyDetail,
  CompanyInput,
  CompanyStudent,
  CompanyStudentState,
} from './types';

/** `maxStudentsPerCompany` sozlamasining demo qiymati (backend default — 10). */
export const MOCK_MAX_STUDENTS = 10;

/** Backend `CompanyRow` shaklida (SPEC-SCREENS §9.7 raqamlari; STIR xom). */
export const mockCompanies: Company[] = [
  {
    id: 'c1',
    name: 'Tech Solutions MChJ',
    tin: '304512889',
    activity: "Dasturiy ta'minot",
    address: 'Toshkent, Amir Temur 108',
    radiusM: 150,
    students: 4,
    suspiciousDays: 0,
    maxStudents: MOCK_MAX_STUDENTS,
    overLimit: false,
    isActive: true,
    flag: null,
  },
  {
    id: 'c2',
    name: 'Agrobank ATB',
    tin: '201344712',
    activity: 'Bank xizmatlari',
    address: 'Toshkent, Mustaqillik 12',
    radiusM: 120,
    students: 6,
    suspiciousDays: 0,
    maxStudents: MOCK_MAX_STUDENTS,
    overLimit: false,
    isActive: true,
    flag: null,
  },
  {
    id: 'c3',
    name: 'Qurilish Trest 12',
    tin: '305881204',
    activity: 'Qurilish',
    address: 'Toshkent v., Zangiota',
    radiusM: 450,
    students: 9,
    suspiciousDays: 1,
    maxStudents: MOCK_MAX_STUDENTS,
    overLimit: false,
    isActive: true,
    flag: 'largeRadius',
  },
  {
    // STIR nazorati namunasi: bitta STIR ostida chegaradan ko'p talaba.
    id: 'c4',
    name: 'Mega Servis MChJ',
    tin: '308190556',
    activity: "Xizmat ko'rsatish",
    address: 'Toshkent, Chilonzor 7',
    radiusM: 200,
    students: 21,
    suspiciousDays: 2,
    maxStudents: MOCK_MAX_STUDENTS,
    overLimit: true,
    isActive: true,
    flag: 'tooManyStudents',
  },
  {
    // Shubhali to'planish namunasi (suspiciousDays >= 3 → ustuvorlikda birinchi).
    id: 'c5',
    name: "Ipak Yo'li Logistika",
    tin: '302774610',
    activity: 'Logistika',
    address: 'Toshkent v., Qibray',
    radiusM: 260,
    students: 5,
    suspiciousDays: 12,
    maxStudents: MOCK_MAX_STUDENTS,
    overLimit: false,
    isActive: false,
    flag: 'suspicious',
  },
];

interface CompanyExtra {
  lat: number;
  lng: number;
  supervisorName: string;
  supervisorPhone: string;
  mentorName: string | null;
  mentorPhone: string | null;
  periods: CompanyDetail['periods'];
}

const EXTRA: Record<string, CompanyExtra> = {
  c1: {
    lat: 41.3111,
    lng: 69.2797,
    supervisorName: 'Rustamov Jasur',
    supervisorPhone: '+998901234567',
    mentorName: 'Xolmatov Aziz',
    mentorPhone: '+998935557711',
    periods: [
      {
        id: 'p1',
        name: '3-kurs kuzgi amaliyot',
        startDate: '2026-09-01',
        endDate: '2026-10-31',
        students: 4,
      },
    ],
  },
  c2: {
    lat: 41.3163,
    lng: 69.2483,
    supervisorName: 'Sattorova Dilnoza',
    supervisorPhone: '+998907771122',
    mentorName: null,
    mentorPhone: null,
    periods: [
      {
        id: 'p1',
        name: '3-kurs kuzgi amaliyot',
        startDate: '2026-09-01',
        endDate: '2026-10-31',
        students: 6,
      },
    ],
  },
  c3: {
    lat: 41.2069,
    lng: 69.1432,
    supervisorName: 'Nazarov Otabek',
    supervisorPhone: '+998971110099',
    mentorName: 'Yodgorov Sherzod',
    mentorPhone: '+998974440088',
    periods: [
      {
        id: 'p1',
        name: '3-kurs kuzgi amaliyot',
        startDate: '2026-09-01',
        endDate: '2026-10-31',
        students: 9,
      },
    ],
  },
  c4: {
    lat: 41.2755,
    lng: 69.2033,
    supervisorName: 'Ergashev Sanjar',
    supervisorPhone: '+998903334455',
    mentorName: 'Qodirova Malika',
    mentorPhone: '+998995556677',
    periods: [
      {
        id: 'p1',
        name: '3-kurs kuzgi amaliyot',
        startDate: '2026-09-01',
        endDate: '2026-10-31',
        students: 14,
      },
      {
        id: 'p2',
        name: '2-kurs bahorgi amaliyot',
        startDate: '2026-02-10',
        endDate: '2026-04-10',
        students: 7,
      },
    ],
  },
  c5: {
    lat: 41.3892,
    lng: 69.4211,
    supervisorName: 'Toshpulatov Ulugbek',
    supervisorPhone: '+998912223344',
    mentorName: null,
    mentorPhone: null,
    periods: [
      {
        id: 'p1',
        name: '3-kurs kuzgi amaliyot',
        startDate: '2026-09-01',
        endDate: '2026-10-31',
        students: 5,
      },
    ],
  },
};

const FALLBACK_EXTRA: CompanyExtra = {
  lat: 41.3111,
  lng: 69.2797,
  supervisorName: 'Rahbar belgilanmagan',
  supervisorPhone: '+998900000000',
  mentorName: null,
  mentorPhone: null,
  periods: [],
};

/** Mock holati — POST/PUT/PATCH/DELETE shu ro'yxatni o'zgartiradi. Testlarda `resetCompaniesMock()`. */
let state: Company[] = structuredClone(mockCompanies);
let extraState: Record<string, CompanyExtra> = structuredClone(EXTRA);
let nextId = mockCompanies.length + 1;

export function resetCompaniesMock() {
  state = structuredClone(mockCompanies);
  extraState = structuredClone(EXTRA);
  nextId = mockCompanies.length + 1;
}

export function mockCompanyDetail(id: string): CompanyDetail | null {
  const row = state.find((c) => c.id === id);
  if (!row) return null;
  const extra = extraState[id] ?? FALLBACK_EXTRA;
  return {
    id: row.id,
    name: row.name,
    tin: row.tin,
    activity: row.activity,
    address: row.address,
    lat: extra.lat,
    lng: extra.lng,
    radiusM: row.radiusM,
    supervisorName: extra.supervisorName,
    supervisorPhone: extra.supervisorPhone,
    mentorName: extra.mentorName,
    mentorPhone: extra.mentorPhone,
    isActive: row.isActive,
    students: row.students,
    suspiciousDays: row.suspiciousDays,
    maxStudents: row.maxStudents,
    overLimit: row.overLimit,
    flag: row.flag,
    periods: extra.periods,
  };
}

const NAMES: Record<string, readonly string[]> = {
  c1: ['Aliyev Akmal', 'Karimov Bekzod', 'Yusupova Nilufar', 'Rahimov Sardor'],
  c2: [
    'Abdullayev Doston',
    'Ismoilova Kamola',
    'Jalilov Nodir',
    'Maxmudova Sevara',
    'Olimov Farrux',
    'Qosimova Dilrabo',
  ],
  c3: [
    'Bozorov Shohruh',
    'Turgunov Aziz',
    'Xudoyberdiyev Umid',
    'Norqulov Jamshid',
    'Sattorov Ilhom',
    'Egamberdiyev Bobur',
    'Mirzayev Temur',
    'Yoqubov Asror',
    'Hamroqulov Dilshod',
  ],
  c4: [
    'Abdurahmonov Ziyodulla',
    'Axmedova Zilola',
    'Baxtiyorov Otabek',
    'Berdiyeva Nargiza',
    'Davronov Samandar',
    'Eshonqulov Rustam',
    'Fayzullayev Behruz',
    'Gulomova Shahzoda',
    'Hakimov Javohir',
    'Ibrohimova Zuhra',
    'Jumayev Anvar',
    'Kamolov Muhammadali',
    'Latipova Muslima',
    'Mamatqulov Sherali',
    'Nabiyev Islom',
    'Otajonova Gulnora',
    'Pulatov Alisher',
    'Rashidov Kamronbek',
    'Saidova Zarnigor',
    'Tolipov Ozodbek',
    'Usmonov Jasurbek',
  ],
  c5: [
    'Vahobov Sardor',
    'Xolmurodova Dildora',
    'Yunusov Shavkat',
    'Zokirova Malika',
    'Quvondiqov Elyor',
  ],
};

const GROUPS = ['412-22', '413-22', '311-23', '214-24'];
const FACULTIES = ['Axborot texnologiyalari', 'Iqtisodiyot va moliya'];
const TUTORS = ['Nodira Saidova', 'Alisher Qodirov', null];

function stateOf(pct: number, suspicious: number): CompanyStudentState {
  if (pct < 70) return 'redFlag';
  return suspicious > 0 ? 'suspicious' : 'active';
}

/** Deterministik demo talabalar: davomat 58–100%, ba'zilarida shubhali kunlar. */
function buildStudents(companyId: string): CompanyStudent[] {
  const names = NAMES[companyId] ?? [];
  const detail = mockCompanyDetail(companyId);
  const periods = detail?.periods ?? [];
  const totalDays = 36;
  // HEMIS ID: har korxona uchun alohida yuzlik (c1 → 341100, c2 → 341200, ...).
  const hemisBase = 341000 + (state.findIndex((c) => c.id === companyId) + 1) * 100;
  return names.map((name, i) => {
    const pct = [94, 86, 64, 100, 78, 89, 72, 58, 91][i % 9] ?? 80;
    const suspicious = i % 5 === 4 ? 3 : 0;
    const status: ApplicationStatus = 'approved';
    const period = periods[i % Math.max(1, periods.length)] ?? null;
    return {
      studentId: `${companyId}-s${i + 1}`,
      name,
      hemisId: String(hemisBase + i),
      group: GROUPS[i % GROUPS.length] ?? '412-22',
      course: 2 + (i % 3),
      faculty: FACULTIES[i % FACULTIES.length] ?? FACULTIES[0]!,
      tutorName: TUTORS[i % TUTORS.length] ?? null,
      applicationStatus: status,
      periodName: period?.name ?? null,
      attendancePct: pct,
      attendedDays: Math.round((pct / 100) * totalDays),
      totalDays,
      diaryCount: Math.max(0, Math.round((pct / 100) * totalDays) - (i % 4)),
      state: stateOf(pct, suspicious),
      suspiciousCount: suspicious,
    } satisfies CompanyStudent;
  });
}

export function mockCompanyStudents(companyId: string): CompanyStudent[] {
  return buildStudents(companyId).sort((a, b) => a.name.localeCompare(b.name, 'uz'));
}

/** Backend `CompanyValidationRules` bilan bir xil (`schema.ts` dagi zod bilan mos), kalitlar camelCase. */
function validateInput(body: Partial<CompanyInput>): Record<string, string[]> {
  const errors: Record<string, string[]> = {};
  const text = (v: unknown) => (typeof v === 'string' ? v.trim() : '');
  const phoneOk = (v: unknown) => typeof v === 'string' && /^\+998\d{9}$/.test(v);

  if (!text(body.name)) errors['name'] = ['Korxona nomini kiriting.'];
  if (!text(body.tin)) errors['tin'] = ['STIR ni kiriting.'];
  else if (!/^\d{9}$/.test(text(body.tin)))
    errors['tin'] = ["STIR 9 ta raqamdan iborat bo'lishi kerak. Namuna: 123456789"];
  if (!text(body.activity)) errors['activity'] = ['Faoliyat turini kiriting.'];
  if (!text(body.address)) errors['address'] = ['Manzilni kiriting.'];
  if (typeof body.lat !== 'number' || body.lat < -90 || body.lat > 90)
    errors['lat'] = ["Kenglik (lat) -90 va 90 oralig'ida bo'lishi kerak."];
  if (typeof body.lng !== 'number' || body.lng < -180 || body.lng > 180)
    errors['lng'] = ["Uzunlik (lng) -180 va 180 oralig'ida bo'lishi kerak."];
  if (body.radiusM != null && (body.radiusM < 50 || body.radiusM > 1000))
    errors['radiusM'] = ["Radius 50–1000 m oralig'ida bo'lishi kerak."];
  if (!text(body.supervisorName)) errors['supervisorName'] = ['Rahbar FISH ni kiriting.'];
  if (!phoneOk(body.supervisorPhone))
    errors['supervisorPhone'] = ["Telefon raqami noto'g'ri. Namuna: +998901234567"];
  if (body.mentorPhone != null && !phoneOk(body.mentorPhone))
    errors['mentorPhone'] = ["Telefon raqami noto'g'ri. Namuna: +998901234567"];
  return errors;
}

function validationProblem(errors: Record<string, string[]>) {
  return problemResponse(400, "Ma'lumotlar noto'g'ri", "Kiritilgan ma'lumotlarda xatolik bor.", {
    errors,
  });
}

/** `geofenceRadius` sozlamasining demo qiymati — `radiusM` berilmasa shu ishlatiladi. */
const DEFAULT_RADIUS_M = 200;

function applyInput(row: Company, extra: CompanyExtra, body: CompanyInput) {
  row.name = body.name.trim();
  row.tin = body.tin.trim();
  row.activity = body.activity.trim();
  row.address = body.address.trim();
  row.radiusM = body.radiusM ?? DEFAULT_RADIUS_M;
  row.flag = row.radiusM > 400 ? 'largeRadius' : row.flag;
  extra.lat = body.lat;
  extra.lng = body.lng;
  extra.supervisorName = body.supervisorName.trim();
  extra.supervisorPhone = body.supervisorPhone;
  extra.mentorName = body.mentorName?.trim() || null;
  extra.mentorPhone = body.mentorPhone || null;
}

/** Import hisoboti namunasi: 6 qatordan 2 tasi rad etilgan (uchta xato yozuvi bilan). */
export const mockCompanyImportResult: ImportResult = {
  totalRows: 6,
  created: 4,
  failed: 2,
  errors: [
    {
      row: 3,
      column: 'STIR',
      value: '30451288',
      message: "STIR 9 ta raqamdan iborat bo'lishi kerak. Namuna: 123456789",
    },
    {
      row: 3,
      column: 'Rahbar telefoni',
      value: '901234567',
      message: "Telefon raqami noto'g'ri. Namuna: +998901234567",
    },
    {
      row: 5,
      column: 'Kenglik (lat)',
      value: '',
      message: "Kenglik (lat) -90 va 90 oralig'ida bo'lishi kerak.",
    },
  ],
};

export const companiesHandlers: HttpHandler[] = [
  // `/import/template` va `/import` — `:id` dan OLDIN (aks holda ular id deb o'qiladi).
  http.get(
    `${COMPANIES_ENDPOINT}/import/template`,
    () =>
      new HttpResponse('mock-xlsx', {
        headers: {
          'Content-Type': 'application/vnd.openxmlformats-officedocument.spreadsheetml.sheet',
          'Content-Disposition': `attachment; filename="${COMPANIES_TEMPLATE_FILE_NAME}"`,
        },
      }),
  ),

  http.post(`${COMPANIES_ENDPOINT}/import`, async ({ request }) => {
    const form = await request.formData().catch(() => null);
    // Fayl nomi jsdom/undici serializatsiyasida yo'qolishi mumkin — faqat mavjudligini tekshiramiz.
    if (!form?.has('file')) {
      return problemResponse(400, "Ma'lumotlar noto'g'ri", 'Excel fayl tanlanmagan.');
    }
    return HttpResponse.json(mockCompanyImportResult);
  }),

  http.get(`${COMPANIES_ENDPOINT}/:id/students`, ({ params }) => {
    const id = String(params['id']);
    if (!state.some((c) => c.id === id)) {
      return problemResponse(404, 'Topilmadi', 'Korxona topilmadi.');
    }
    return HttpResponse.json(mockCompanyStudents(id));
  }),

  http.post(COMPANIES_ENDPOINT, async ({ request }) => {
    const body = (await request.json().catch(() => ({}))) as Partial<CompanyInput>;
    const errors = validateInput(body);
    if (Object.keys(errors).length > 0) return validationProblem(errors);

    const input = body as CompanyInput;
    const tin = input.tin.trim();
    if (state.some((c) => c.tin === tin)) {
      return problemResponse(409, 'Ziddiyat', `STIR ${tin} bilan korxona allaqachon mavjud.`);
    }
    const id = `c${nextId++}`;
    const row: Company = {
      id,
      name: '',
      tin,
      activity: '',
      address: '',
      radiusM: DEFAULT_RADIUS_M,
      students: 0,
      suspiciousDays: 0,
      maxStudents: MOCK_MAX_STUDENTS,
      overLimit: false,
      isActive: true,
      flag: null,
    };
    const extra: CompanyExtra = { ...structuredClone(FALLBACK_EXTRA) };
    applyInput(row, extra, input);
    state = [...state, row];
    extraState[id] = extra;
    return HttpResponse.json(mockCompanyDetail(id), { status: 201 });
  }),

  http.put(`${COMPANIES_ENDPOINT}/:id`, async ({ request, params }) => {
    const id = String(params['id']);
    const existing = state.find((c) => c.id === id);
    if (!existing) return problemResponse(404, 'Topilmadi', 'Korxona topilmadi.');

    const body = (await request.json().catch(() => ({}))) as Partial<CompanyInput>;
    const errors = validateInput(body);
    if (Object.keys(errors).length > 0) return validationProblem(errors);

    const input = body as CompanyInput;
    const tin = input.tin.trim();
    if (state.some((c) => c.id !== id && c.tin === tin)) {
      return problemResponse(409, 'Ziddiyat', `STIR ${tin} bilan korxona allaqachon mavjud.`);
    }
    const row = { ...existing };
    const extra = { ...(extraState[id] ?? structuredClone(FALLBACK_EXTRA)) };
    applyInput(row, extra, input);
    state = state.map((c) => (c.id === id ? row : c));
    extraState[id] = extra;
    return HttpResponse.json(mockCompanyDetail(id));
  }),

  http.patch(`${COMPANIES_ENDPOINT}/:id/status`, async ({ request, params }) => {
    const id = String(params['id']);
    const existing = state.find((c) => c.id === id);
    if (!existing) return problemResponse(404, 'Topilmadi', 'Korxona topilmadi.');

    const body = (await request.json().catch(() => ({}))) as { isActive?: boolean };
    state = state.map((c) => (c.id === id ? { ...c, isActive: Boolean(body.isActive) } : c));
    return HttpResponse.json(mockCompanyDetail(id));
  }),

  http.delete(`${COMPANIES_ENDPOINT}/:id`, ({ params }) => {
    const id = String(params['id']);
    const existing = state.find((c) => c.id === id);
    if (!existing) return problemResponse(404, 'Topilmadi', 'Korxona topilmadi.');
    if (existing.isActive) {
      return problemResponse(
        409,
        'Ziddiyat',
        "Avval korxonani faolsizlantiring — keyin o'chirish mumkin.",
      );
    }
    if (existing.students > 0) {
      return problemResponse(
        409,
        'Ziddiyat',
        `Korxonaga ${existing.students} ta talaba biriktirilgan — uni o'chirib bo'lmaydi. ` +
          "Talabalarni boshqa korxonaga ko'chiring yoki korxonani faqat faolsizlantiring.",
      );
    }
    state = state.filter((c) => c.id !== id);
    delete extraState[id];
    return new HttpResponse(null, { status: 204 });
  }),

  http.get(`${COMPANIES_ENDPOINT}/:id`, ({ params }) => {
    const detail = mockCompanyDetail(String(params['id']));
    if (!detail) return problemResponse(404, 'Topilmadi', 'Korxona topilmadi.');
    return HttpResponse.json(detail);
  }),

  http.get(COMPANIES_ENDPOINT, ({ request }) =>
    HttpResponse.json(paginateMock(request.url, state, (c) => [c.name, c.tin, c.address])),
  ),
];
