import { http, HttpResponse, type HttpHandler } from 'msw';
import { problem } from '@/mocks/data';
import type { DiaryEntry, DiaryFile, DiaryStatus } from '../diaries/types';
import { MOCK_TODAY_DATE } from '../today/mocks';
import type { AttendanceStatus } from '../today/types';
import { isCompanyBoundApplication } from './types';
import type {
  AttendanceAttempt,
  AttendanceDayDiary,
  AttendancePunch,
  AttendanceSummary,
  StudentAttendanceDay,
  StudentCompany,
  StudentGrade,
  StudentPeriod,
  StudentPeriodOption,
  StudentApplicationStatus,
  StudentStatus,
  TutorStudent,
  TutorStudentDetail,
} from './types';
import { hasMockStudentPassword } from '@/features/shared/student-password/passwordStore';

/**
 * SPEC-SCREENS §5 mock (6 ta). `buildDetail`/`buildAttendance`/`buildDiaries` admin mock'laridan
 * ham ishlatiladi (`features/admin/students/mocks.ts`) — ikkala profil bir xil ma'lumotni ko'rsatadi.
 */
export const mockStudents: TutorStudent[] = [
  {
    id: 's-341030',
    name: 'Aliyev Akmal',
    hemisId: '341030',
    group: '412-22',
    company: 'Tech Solutions MChJ',
    attendancePct: 94,
    attendedDays: 34,
    totalDays: 36,
    diaryCount: 32,
    diaryAvg: 4.2,
    state: 'active',
    suspiciousCount: 0,
  },
  {
    id: 's-341031',
    name: 'Karimov Bekzod',
    hemisId: '341031',
    group: '412-22',
    company: 'Agrobank ATB',
    attendancePct: 86,
    attendedDays: 31,
    totalDays: 36,
    diaryCount: 28,
    diaryAvg: 3.8,
    state: 'active',
    suspiciousCount: 0,
  },
  {
    id: 's-341032',
    name: 'Sobirov Diyor',
    hemisId: '341032',
    group: '413-22',
    // Ariza tuzatishga qaytarilgan — tasdiqlangan ariza yo'q, shuning uchun korxona ham yo'q.
    company: null,
    attendancePct: 64,
    attendedDays: 23,
    totalDays: 36,
    diaryCount: 19,
    diaryAvg: 3.1,
    state: 'redFlag',
    suspiciousCount: 0,
  },
  {
    id: 's-341033',
    name: 'Yusupova Nilufar',
    hemisId: '341033',
    group: '413-22',
    company: 'Mediapark',
    attendancePct: 100,
    attendedDays: 36,
    totalDays: 36,
    diaryCount: 36,
    diaryAvg: 4.8,
    state: 'active',
    suspiciousCount: 0,
  },
  {
    id: 's-341034',
    name: 'Rahimov Sardor',
    hemisId: '341034',
    group: '412-22',
    company: 'Qurilish Trest 12',
    attendancePct: 78,
    attendedDays: 28,
    totalDays: 36,
    diaryCount: 25,
    diaryAvg: 3.5,
    state: 'suspicious',
    suspiciousCount: 3,
  },
  {
    id: 's-341035',
    name: 'Toshpulatova Zarina',
    hemisId: '341035',
    group: '413-22',
    company: 'Ipak Yuli Bank',
    attendancePct: 89,
    attendedDays: 32,
    totalDays: 36,
    diaryCount: 30,
    diaryAvg: 4.0,
    state: 'active',
    suspiciousCount: 0,
  },
];

/* ────────────────────────────────────────────────────────────────────────────
 * Talaba profili mock'lari (KONTRAKT §2): detail · kun-bakun davomat · kundaliklar.
 * Sanalar `today/mocks.ts` dagi MOCK_TODAY_DATE ga bog'langan (2026-10-12).
 * ──────────────────────────────────────────────────────────────────────────── */

const PERIOD: StudentPeriod = {
  id: 'per-2026-3k',
  name: '3-kurs ishlab chiqarish amaliyoti',
  startDate: '2026-09-07',
  endDate: '2026-12-18',
  dailyStart: '09:00',
  dailyEnd: '18:00',
  workDays: [1, 2, 3, 4, 5, 6],
  requiredDays: 72,
};

/**
 * v3.5 (§4.6): demo talaba (s-341030 · admin s1) guruhiga ikkinchi — rejadagi bahorgi davr ham
 * biriktirilgan. Tanlagich 2 ta davrni ko'rsatadi; bahorgi davr tanlansa bloklar bo'sh keladi.
 */
export const SPRING_PERIOD: StudentPeriod = {
  id: 'per-2027-bahor',
  name: 'Bahorgi amaliyot 2027',
  startDate: '2027-02-01',
  endDate: '2027-04-30',
  dailyStart: '09:00',
  dailyEnd: '17:00',
  workDays: [1, 2, 3, 4, 5],
  requiredDays: 60,
};

const PERIOD_STATUS: Record<string, StudentPeriodOption['status']> = {
  [PERIOD.id]: 'active',
  [SPRING_PERIOD.id]: 'planned',
};

/** Talaba → uning davrlari (startDate kamayish tartibida). */
const STUDENT_PERIODS: Record<string, StudentPeriod[]> = {
  's-341030': [SPRING_PERIOD, PERIOD],
};

function periodsOf(studentId: string): StudentPeriod[] {
  return STUDENT_PERIODS[studentId] ?? [PERIOD];
}

/** `GET …/students/{id}` → `periods` (sukut — davom etayotgan davr). */
export function mockPeriodOptions(studentId: string): StudentPeriodOption[] {
  return periodsOf(studentId).map((p) => ({
    id: p.id,
    name: p.name,
    startDate: p.startDate,
    endDate: p.endDate,
    status: PERIOD_STATUS[p.id] ?? 'active',
    isDefault: p.id === PERIOD.id,
  }));
}

/**
 * `?periodId=` → talabaning shu davri; berilmasa sukut davri. Talabaga tegishli bo'lmasa `null`
 * (backend 404 "Amaliyot davri topilmadi.").
 */
export function resolveMockPeriod(
  studentId: string,
  periodId: string | null,
): StudentPeriod | null {
  if (!periodId) return PERIOD;
  return periodsOf(studentId).find((p) => p.id === periodId) ?? null;
}

const COMPANIES: Record<string, StudentCompany> = {
  'Tech Solutions MChJ': {
    id: 'co-1',
    name: 'Tech Solutions MChJ',
    tin: '304512889',
    activity: "Dasturiy ta'minot ishlab chiqish",
    address: "Toshkent sh., Mirzo Ulug'bek t., Buyuk Ipak Yo'li 12",
    supervisorName: 'Rustamov Jasur',
    supervisorPhone: '+998901112233',
    mentorName: 'Qodirov Sherzod',
    mentorPhone: '+998901112244',
    lat: 41.32451,
    lng: 69.29712,
    radiusM: 200,
  },
  'Agrobank ATB': {
    id: 'co-2',
    name: 'Agrobank ATB',
    tin: '200832451',
    activity: 'Bank xizmatlari',
    address: 'Toshkent sh., Shayxontohur t., Navoiy ko‘chasi 44',
    supervisorName: 'Nazarov Otabek',
    supervisorPhone: '+998907771122',
    mentorName: null,
    mentorPhone: null,
    lat: 41.31698,
    lng: 69.24015,
    radiusM: 150,
  },
  Uzinfocom: {
    id: 'co-3',
    name: 'Uzinfocom',
    tin: '201445673',
    activity: 'Axborot texnologiyalari xizmatlari',
    address: 'Toshkent sh., Yashnobod t., Amir Temur 108',
    supervisorName: 'Saidova Kamola',
    supervisorPhone: '+998935550011',
    mentorName: 'Ergashev Anvar',
    mentorPhone: '+998935550022',
    lat: 41.33912,
    lng: 69.28455,
    radiusM: 250,
  },
  Mediapark: {
    id: 'co-4',
    name: 'Mediapark',
    tin: '305118762',
    activity: 'Reklama va marketing',
    address: 'Toshkent sh., Chilonzor t., Bunyodkor shoh ko‘chasi 6',
    supervisorName: 'Islomov Doston',
    supervisorPhone: '+998901234500',
    mentorName: 'Mirzayeva Sevara',
    mentorPhone: '+998901234501',
    lat: 41.28374,
    lng: 69.20431,
    radiusM: 180,
  },
  'Qurilish Trest 12': {
    id: 'co-5',
    name: 'Qurilish Trest 12',
    tin: '202990145',
    activity: 'Qurilish-montaj ishlari',
    address: 'Toshkent vil., Zangiota t., Sanoat ko‘chasi 3',
    supervisorName: 'Tursunov Bahodir',
    supervisorPhone: '+998977001234',
    mentorName: null,
    mentorPhone: null,
    lat: 41.20114,
    lng: 69.18902,
    radiusM: 300,
  },
  'Ipak Yuli Bank': {
    id: 'co-6',
    name: 'Ipak Yuli Bank',
    tin: '200114589',
    activity: 'Bank xizmatlari',
    address: 'Toshkent sh., Yunusobod t., Bog‘ishamol 29',
    supervisorName: 'Yo‘ldoshev Farrux',
    supervisorPhone: '+998909998877',
    mentorName: 'Abdullayeva Malika',
    mentorPhone: '+998909998866',
    lat: 41.35102,
    lng: 69.28901,
    radiusM: 160,
  },
};

interface ProfileSeed {
  course: number;
  faculty: string;
  direction: string;
  phone: string | null;
  status: StudentStatus;
  /** Ariza holati; `approved` bo'lmasa korxona ham ko'rsatilmaydi. */
  applicationStatus: StudentApplicationStatus;
  withContract: boolean;
  grade: StudentGrade | null;
  /** Davomat naqshi (ish kunlari bo'yicha aylanadi). */
  pattern: string;
}

/**
 * Kod harflari: k — keldi · l — kech keldi · a — kelmadi · s — sababli ·
 * x — keldi (ichkaridan qabul qilindi), lekin undan oldin radius tashqarisidan rad etilgan
 *     urinishlar bo'lgan → shubhali kun · o — qabul qilingan, ammo radius keyin toraytirilgani
 *     uchun `outOfRadius` · m — tyutor qo'lda belgilagan.
 */
const PROFILES: Record<string, ProfileSeed> = {
  's-341030': {
    course: 3,
    faculty: 'Raqamli iqtisodiyot',
    direction: 'Axborot tizimlari va texnologiyalari',
    phone: '+998901010101',
    status: 'active',
    applicationStatus: 'approved',
    withContract: true,
    grade: { total: 87.5, grade: 5 },
    pattern: 'kkkkklkkkkkkkkskkkkkkakkkkkkkkk',
  },
  's-341031': {
    course: 3,
    faculty: 'Moliya',
    direction: 'Bank ishi',
    phone: '+998901010102',
    status: 'active',
    applicationStatus: 'approved',
    withContract: true,
    grade: { total: 74.2, grade: 4 },
    pattern: 'kklkkkkllkkkskkkkakkkklkkkkkkkk',
  },
  's-341032': {
    course: 3,
    faculty: 'Raqamli iqtisodiyot',
    direction: 'Axborot tizimlari va texnologiyalari',
    phone: null,
    status: 'active',
    // Ariza tuzatishga qaytarilgan → korxona hali biriktirilmagan (company: null), ariza bloki bor.
    applicationStatus: 'revisionNeeded',
    withContract: false,
    grade: null,
    pattern: 'kaalkaakaaskkaaakkaaakaakkaalak',
  },
  's-341033': {
    course: 3,
    faculty: 'Marketing',
    direction: 'Marketing va reklama',
    phone: '+998901010104',
    status: 'active',
    applicationStatus: 'approved',
    withContract: true,
    grade: { total: 95.4, grade: 5 },
    pattern: 'kkkkkkkkkkkkkkkkkkkkkkkkkkkkkkk',
  },
  's-341034': {
    course: 3,
    faculty: 'Qurilish iqtisodiyoti',
    direction: 'Qurilishni tashkil etish',
    phone: '+998901010105',
    status: 'active',
    applicationStatus: 'approved',
    withContract: true,
    grade: { total: 62.8, grade: 3 },
    pattern: 'kkxklkkmkkoxkskkkkxkkakkkkkkkkk',
  },
  's-341035': {
    course: 3,
    faculty: 'Moliya',
    direction: 'Bank ishi',
    phone: '+998901010106',
    status: 'suspended',
    applicationStatus: 'approved',
    withContract: true,
    grade: { total: 81.3, grade: 4 },
    pattern: 'kkkkkkkkskkkkkkkklkkkkkkkkkkkkk',
  },
};

const DAY_MS = 86_400_000;

/** [from..to] oralig'idagi barcha DateOnly kunlar. */
function eachDate(from: string, to: string): string[] {
  const out: string[] = [];
  const end = Date.parse(`${to}T00:00:00Z`);
  for (let t = Date.parse(`${from}T00:00:00Z`); t <= end; t += DAY_MS) {
    out.push(new Date(t).toISOString().slice(0, 10));
  }
  return out;
}

/** 1 = dushanba … 7 = yakshanba. */
function isoDow(date: string): number {
  const d = new Date(`${date}T00:00:00Z`).getUTCDay();
  return d === 0 ? 7 : d;
}

const hhmm = (minutes: number) =>
  `${String(Math.floor(minutes / 60)).padStart(2, '0')}:${String(minutes % 60).padStart(2, '0')}`;

const punch = (
  date: string,
  minutes: number,
  rest: Omit<AttendancePunch, 'at' | 'atIso'>,
): AttendancePunch => ({
  at: hhmm(minutes),
  atIso: `${date}T${hhmm(minutes)}:00+05:00`,
  ...rest,
});

const attempt = (
  date: string,
  id: string,
  kind: AttendanceAttempt['kind'],
  minutes: number,
  rest: Omit<AttendanceAttempt, 'id' | 'kind' | 'at' | 'atIso'>,
): AttendanceAttempt => ({
  id,
  kind,
  at: hhmm(minutes),
  atIso: `${date}T${hhmm(minutes)}:00+05:00`,
  ...rest,
});

/** Qabul qilingan belgilanish → urinish yozuvi (radius tashqarisida qabul qilingan bo'lishi ham mumkin). */
const fromPunch = (
  id: string,
  kind: AttendanceAttempt['kind'],
  p: AttendancePunch,
  radiusM: number,
): AttendanceAttempt => ({
  id,
  kind,
  at: p.at,
  atIso: p.atIso,
  accepted: true,
  rejectReason: null,
  rejectMessage: null,
  distanceM: p.distanceM,
  accuracyM: p.accuracyM,
  radiusM,
  lat: p.lat,
  lng: p.lng,
  photoUrl: p.photoUrl,
});

const CODE_STATUS: Record<string, AttendanceStatus> = {
  k: 'present',
  l: 'late',
  a: 'absent',
  s: 'excused',
  x: 'present',
  o: 'present',
  m: 'present',
};

/**
 * Bitta talabaning davr boshidan bugungacha bo'lgan kunlari (qat'iy, tasodifsiz).
 * Rejadagi (hali boshlanmagan) davrda — bo'sh.
 */
export function buildAttendance(
  studentId: string,
  periodId: string | null = null,
): StudentAttendanceDay[] {
  const seed = PROFILES[studentId];
  const student = mockStudents.find((s) => s.id === studentId);
  if (!seed || !student) return [];
  if (resolveMockPeriod(studentId, periodId)?.id !== PERIOD.id) return [];
  const company = student.company ? COMPANIES[student.company] : undefined;
  const radiusM = company?.radiusM ?? 200;
  const baseLat = company?.lat ?? 41.3111;
  const baseLng = company?.lng ?? 69.2797;

  const days: StudentAttendanceDay[] = [];
  let w = 0;
  for (const date of eachDate(PERIOD.startDate, MOCK_TODAY_DATE)) {
    const dow = isoDow(date);
    const isWorkDay = PERIOD.workDays.includes(dow);
    if (!isWorkDay) {
      days.push({
        date,
        status: 'dayOff',
        isWorkDay: false,
        checkIn: null,
        checkOut: null,
        autoClosed: false,
        suspicious: false,
        suspiciousReason: null,
        manual: false,
        manualReason: null,
        leaveRequestId: null,
        diary: null,
        attempts: 0,
        rejectedAttempts: 0,
        events: [],
      });
      continue;
    }

    const code = seed.pattern[w % seed.pattern.length] ?? 'k';
    w += 1;
    const status = CODE_STATUS[code] ?? 'present';
    const late = code === 'l';
    // `x` — qabul qilingan belgilanish RADIUS ICHIDA, lekin undan oldin rad etilgan urinishlar bo'lgan.
    const rejectedBefore = code === 'x';
    // `o` — qabul qilingan belgilanish radius tashqarisida (radius keyinroq toraytirilgan).
    const outside = code === 'o';
    const manual = code === 'm';
    const present = status === 'present' || late;

    // Kelmagan / sababli / qo'lda belgilangan kunlarda belgilanish yo'q.
    const hasPunch = present && !manual;
    const inMinutes = 9 * 60 + (late ? 38 + ((w * 5) % 17) : 1 + ((w * 7) % 13));
    const distanceM = outside ? radiusM + 380 + ((w * 37) % 900) : 18 + ((w * 13) % 110);
    // Har 3-kunda (va `x` kunlarda) check-in selfie'si bor; bitta kun — fayli yo'q (AuthImage fallback'ini ko'rsatadi).
    // Rad etilgan urinishli (`x`) kunlarda qabul qilingan kirish ham selfi bilan — galereyada
    // rad/qabul rasmlari yonma-yon.
    const photoUrl =
      !hasPunch || (w % 3 !== 1 && !rejectedBefore)
        ? null
        : w === 7
          ? '/api/files/missing-photo'
          : `/api/files/ph-${studentId}-${date}`;
    // Har 8-kunda check-out yo'q — tizim avtomatik yopgan.
    const autoClosed = hasPunch && w % 8 === 0;

    const checkIn = hasPunch
      ? punch(date, inMinutes, {
          distanceM,
          accuracyM: 6 + ((w * 3) % 19),
          lat: Number((baseLat + ((w % 7) - 3) * 0.00042).toFixed(5)),
          lng: Number((baseLng + ((w % 5) - 2) * 0.00051).toFixed(5)),
          photoUrl,
          outOfRadius: distanceM > radiusM,
        })
      : null;
    const checkOut =
      hasPunch && !autoClosed
        ? punch(date, 17 * 60 + ((w * 11) % 45), {
            distanceM: 15 + ((w * 17) % 90),
            accuracyM: 6 + ((w * 5) % 21),
            lat: Number((baseLat + ((w % 6) - 3) * 0.00039).toFixed(5)),
            lng: Number((baseLng + ((w % 4) - 2) * 0.00047).toFixed(5)),
            // Har 6-kunda (4, 10, 16…) chiqish selfisi ham bor — jadvalda ikki thumbnail yonma-yon.
            photoUrl: w % 3 === 1 && w % 2 === 0 ? `/api/files/ph-out-${studentId}-${date}` : null,
            outOfRadius: false,
          })
        : null;

    const events: AttendanceAttempt[] = [];
    if (hasPunch && checkIn) {
      if (rejectedBefore) {
        // Qabul qilingan kirishdan oldin 2 ta rad etilgan urinish: radius tashqarisi (selfi bilan)
        // va QR mos kelmagan (selfisiz — "Selfi yo'q" placeholder).
        const farM = radiusM + 340 + ((w * 29) % 400);
        events.push(
          attempt(date, `${studentId}-${date}-r1`, 'checkIn', inMinutes - 14, {
            accepted: false,
            rejectReason: 'outOfRadius',
            rejectMessage: `Korxona hududidan tashqaridasiz: ${farM} m (ruxsat etilgan ${radiusM} m)`,
            distanceM: farM,
            accuracyM: 12,
            radiusM,
            lat: Number((baseLat + 0.0041).toFixed(5)),
            lng: Number((baseLng - 0.0033).toFixed(5)),
            photoUrl: `/api/files/att-${studentId}-${date}-r1`,
          }),
          attempt(date, `${studentId}-${date}-r2`, 'checkIn', inMinutes - 6, {
            accepted: false,
            rejectReason: 'qrInvalid',
            rejectMessage: null,
            distanceM: 64,
            accuracyM: 9,
            radiusM,
            lat: checkIn.lat,
            lng: checkIn.lng,
            photoUrl: null,
          }),
        );
      }
      events.push(fromPunch(`${studentId}-${date}-in`, 'checkIn', checkIn, radiusM));
      if (checkOut) {
        events.push(fromPunch(`${studentId}-${date}-out`, 'checkOut', checkOut, radiusM));
      }
    }

    const hasDiary = present && w % 5 !== 3;
    const diaryStatus: DiaryStatus =
      w % 7 === 0 ? 'submitted' : w % 11 === 0 ? 'rewrite' : 'approved';
    days.push({
      date,
      status,
      isWorkDay: true,
      checkIn,
      checkOut,
      autoClosed,
      suspicious: rejectedBefore || outside,
      suspiciousReason: rejectedBefore
        ? '2 ta urinish rad etildi (radius tashqarisi, QR mos emas), keyin korxona hududidan belgilandi'
        : outside
          ? `Qabul qilingan belgilanish radius tashqarisida (${Math.round(distanceM)} m > ${radiusM} m)`
          : null,
      manual,
      manualReason: manual ? 'Korxona rahbari tasdiqlagan — tyutor qo‘lda belgiladi' : null,
      leaveRequestId: status === 'excused' ? `lr-${studentId}-${date}` : null,
      diary: hasDiary ? dayDiary(`sd-${studentId}-${date}`, diaryStatus, w) : null,
      attempts: rejectedBefore ? 3 : hasPunch ? 1 : 0,
      rejectedAttempts: rejectedBefore ? 2 : 0,
      events,
    });
  }
  return days;
}

function summarize(days: readonly StudentAttendanceDay[]): AttendanceSummary {
  const workDays = days.filter((d) => d.isWorkDay);
  const count = (fn: (d: StudentAttendanceDay) => boolean) => workDays.filter(fn).length;
  const attendedDays = count((d) => d.status === 'present');
  const lateDays = count((d) => d.status === 'late');
  const excusedDays = count((d) => d.status === 'excused');
  // Backend: hisobga olinadigan ish kunlari — sababli kunlar chiqarib tashlanadi.
  const totalDays = workDays.length - excusedDays;
  const pct = totalDays === 0 ? 0 : ((attendedDays + lateDays) / totalDays) * 100;
  return {
    totalDays,
    attendedDays,
    lateDays,
    excusedDays,
    absentDays: count((d) => d.status === 'absent'),
    suspiciousDays: count((d) => d.suspicious),
    attendancePct: Math.round(pct * 10) / 10,
  };
}

const DIARY_TEXTS: string[] = [
  "Bugun mentor bilan birga loyihaning ma'lumotlar bazasi sxemasini ko'rib chiqdik. Uchta jadvalga indeks qo'shdim va sekin ishlayotgan so'rovni qayta yozdim — natijada sahifa 2 soniya o'rniga 0,4 soniyada ochildi.",
  "Ertalab bo'lim yig'ilishida qatnashdim, kunlik vazifalar taqsimlandi. Menga mijozlar ro'yxati sahifasidagi filtrlash xatosini tuzatish topshirildi. Xatoning sababi — bo'sh qidiruv satri bilan so'rov yuborilishi ekan, shartni qo'shib hal qildim.",
  "Hujjatlar aylanmasi bo'yicha arxivdagi 40 ta shartnomani raqamlashtirdim, skanerdan o'tkazib nomlash qoidasiga muvofiq papkalarga joyladim. Mentor tekshirib, ikkita faylni qayta skanerlashni so'radi.",
  "Mijozlar bilan ishlash bo'limida telefon orqali murojaatlarni qabul qilishni kuzatdim. 12 ta murojaatni jurnalga kiritdim va ularning uchtasini tegishli mutaxassisga yo'naltirdim.",
  "Ishlab chiqarish sexida xavfsizlik yo'riqnomasidan o'tdim. Keyin usta bilan birga kunlik hisobot shaklini to'ldirishni o'rgandim — materiallar sarfi va bajarilgan ishlar hajmi yoziladi.",
  "Marketing rejasining oktabr qismini tayyorladim: raqobatchilarning ijtimoiy tarmoqdagi 20 ta postini tahlil qilib, jadval ko'rinishida taqdim etdim. Rahbar ikkita g'oyani tanladi.",
  'Buxgalteriya dasturida dastlabki hujjatlarni kiritishni mashq qildim. 25 ta kirim orderini kiritdim, ikkitasida STIR xato terilgani aniqlandi va tuzatildi.',
  "Loyihaning frontend qismida forma validatsiyasini yozdim. Foydalanuvchi telefon raqamini noto'g'ri kiritganda o'zbekcha xato matni chiqadigan qildim va testdan o'tkazdim.",
];

const DIARY_LEARNED: (string | null)[] = [
  "Indekslar va so'rov rejasini o'qishni o'rgandim",
  null,
  'Hujjatlarni arxivlash tartibi',
  'Mijoz murojaatlarini toifalash',
  null,
  'Raqobat tahlili usullari',
  'Dastlabki hujjatlar bilan ishlash',
  'Forma validatsiyasi va xato matnlari',
];

/** Mock'da qo'yilgan baholar (`POST /api/{tutor|admin}/diaries/:id/review`) — `buildDiaries` ustiga qo'yiladi. */
export type DiaryReviewPatch = Pick<DiaryEntry, 'status' | 'score' | 'comment' | 'reviewedAt'>;

const diaryReviews = new Map<string, DiaryReviewPatch>();

/**
 * Davomat qatoridagi kundalik xulosasi — mock'da qo'yilgan baho bo'lsa o'shani ko'rsatadi
 * (kun oynasida baholangach jadvaldagi "Kundalik" ustuni ham yangilanadi).
 */
function dayDiary(id: string, seedStatus: DiaryStatus, w: number): AttendanceDayDiary {
  const review = diaryReviews.get(id);
  if (review) return { id, status: review.status, score: review.score };
  return { id, status: seedStatus, score: seedStatus === 'approved' ? 3 + (w % 3) : null };
}

/** Mock baholarni tiklash (testlar orasida) — `resetTutorMocks()` chaqiradi. */
export function resetStudentDiaryReviewsMock() {
  diaryReviews.clear();
}

/** Profil kundaligini id bo'yicha topish (kundaliklar sahifasidagi ro'yxatda yo'q yozuvlar uchun). */
export function findProfileDiaryMock(diaryId: string): DiaryEntry | undefined {
  for (const student of mockStudents) {
    const found = buildDiaries(student.id).find((d) => d.id === diaryId);
    if (found) return found;
  }
  return undefined;
}

/** Baholashni mock holatiga yozish — keyingi `buildDiaries`/`buildAttendance` shuni qaytaradi. */
export function applyProfileDiaryReviewMock(diaryId: string, patch: DiaryReviewPatch) {
  diaryReviews.set(diaryId, patch);
}

/**
 * Kundaliklar: davomatdagi `diary` bo'lgan barcha kunlar (sana bo'yicha kamayish) —
 * backend `GET .../diaries` ham hammasini qaytaradi, shuning uchun kesilmaydi
 * (kundalik jadvalidagi kun paneli istalgan kunning matnini topa olsin).
 */
export function buildDiaries(studentId: string, periodId: string | null = null): DiaryEntry[] {
  const student = mockStudents.find((s) => s.id === studentId);
  if (!student) return [];
  const withDiary = buildAttendance(studentId, periodId).filter((d) => d.diary !== null);
  return withDiary.reverse().map((day, i) => {
    const diary = day.diary!;
    const submittedAt = `${day.date}T18:${String(10 + ((i * 7) % 45)).padStart(2, '0')}:00+05:00`;
    const files: DiaryFile[] =
      i % 3 === 0
        ? [
            {
              name: `ish_jarayoni_${day.date}.jpg`,
              url: `/api/files/dph-${studentId}-${day.date}`,
            },
            { name: 'kunlik_hisobot.pdf', url: `/api/files/doc-${studentId}-${day.date}` },
          ]
        : i % 3 === 1
          ? [{ name: `natija_${day.date}.png`, url: `/api/files/dph2-${studentId}-${day.date}` }]
          : [];
    const review = diaryReviews.get(diary.id);
    return {
      id: diary.id,
      studentId,
      studentName: student.name,
      group: student.group,
      date: day.date,
      submittedAt,
      status: review?.status ?? diary.status,
      text: DIARY_TEXTS[i % DIARY_TEXTS.length]!,
      learned: DIARY_LEARNED[i % DIARY_LEARNED.length] ?? null,
      files,
      score: review ? review.score : diary.score,
      comment: review
        ? review.comment
        : diary.status === 'rewrite'
          ? "Batafsil yozing: qanday hujjatlar bilan ishladingiz, natija nima bo'ldi."
          : diary.status === 'approved' && i % 4 === 0
            ? 'Yaxshi hisobot, fotolar ham biriktirilgan.'
            : null,
      reviewedAt: review
        ? review.reviewedAt
        : diary.status === 'submitted'
          ? null
          : `${day.date}T20:05:00+05:00`,
    } satisfies DiaryEntry;
  });
}

/** `null` — talaba yo'q; `'periodNotFound'` — `periodId` talabaga tegishli emas (404). */
export function buildDetail(
  studentId: string,
  periodId: string | null = null,
): TutorStudentDetail | 'periodNotFound' | null {
  const student = mockStudents.find((s) => s.id === studentId);
  const seed = PROFILES[studentId];
  if (!student || !seed) return null;
  const period = resolveMockPeriod(studentId, periodId);
  if (!period) return 'periodNotFound';
  const periods = mockPeriodOptions(studentId);

  if (period.id !== PERIOD.id) {
    // Rejadagi davr: davrga bog'liq bloklar bo'sh (application/company/grade = null, nollar).
    return {
      id: student.id,
      name: student.name,
      hemisId: student.hemisId,
      group: student.group,
      course: seed.course,
      faculty: seed.faculty,
      direction: seed.direction,
      status: seed.status,
      phone: seed.phone,
      state: 'active',
      suspiciousCount: 0,
      company: null,
      application: null,
      period,
      attendance: summarize([]),
      diary: { count: 0, scoredCount: 0, avg: 0 },
      grade: null,
      periods,
      selectedPeriodId: period.id,
      hasPassword: hasMockStudentPassword(student.id),
    };
  }

  const days = buildAttendance(studentId);
  const diaries = buildDiaries(studentId);
  const scored = diaries.filter((d) => d.score !== null);
  const company = student.company ? (COMPANIES[student.company] ?? null) : null;
  const companyBound = isCompanyBoundApplication(seed.applicationStatus);

  return {
    id: student.id,
    name: student.name,
    hemisId: student.hemisId,
    group: student.group,
    course: seed.course,
    faculty: seed.faculty,
    direction: seed.direction,
    status: seed.status,
    phone: seed.phone,
    state: student.state,
    suspiciousCount: student.suspiciousCount,
    company: companyBound ? company : null,
    application: {
      id: `app-${studentId}`,
      status: seed.applicationStatus,
      submittedAt: '2026-09-01T10:24:00+05:00',
      decidedAt: companyBound ? '2026-09-03T14:10:00+05:00' : null,
      comment: companyBound
        ? "Shartnoma to'liq, geofence radiusi 200 m qilib belgilandi."
        : 'Shartnoma nusxasi o‘qilmaydi, aniq nusxasini qayta yuklang.',
      contract: seed.withContract
        ? {
            name: `shartnoma_${student.hemisId}.pdf`,
            pages: 4,
            sizeBytes: 1_843_200,
            url: `/api/files/contract-${studentId}`,
          }
        : null,
    },
    period: PERIOD,
    attendance: summarize(days),
    diary: {
      count: diaries.length,
      scoredCount: scored.length,
      avg:
        scored.length === 0
          ? 0
          : Math.round((scored.reduce((sum, d) => sum + (d.score ?? 0), 0) / scored.length) * 10) /
            10,
    },
    grade: seed.grade,
    periods,
    selectedPeriodId: period.id,
    hasPassword: hasMockStudentPassword(student.id),
  };
}

const notFound = () =>
  HttpResponse.json(problem(404, 'Topilmadi', 'Talaba topilmadi.'), { status: 404 });

/** Begona `periodId` → 404 (backend: "Amaliyot davri topilmadi."). */
export const periodNotFound = () =>
  HttpResponse.json(problem(404, 'Topilmadi', 'Amaliyot davri topilmadi.'), { status: 404 });

/** `from`/`to` davr chegarasiga qisiladi (backend `…/attendance` bilan bir xil). */
export function clampAttendanceRange(
  period: StudentPeriod,
  from: string | null,
  to: string | null,
): { from: string; to: string } {
  const start = from && from > period.startDate ? from : period.startDate;
  const endCap = period.endDate < MOCK_TODAY_DATE ? period.endDate : MOCK_TODAY_DATE;
  const end = to ? (to < period.endDate ? to : period.endDate) : endCap;
  return { from: start, to: end };
}

const validation = (errors: Record<string, string[]>) =>
  HttpResponse.json(problem(400, 'One or more validation errors occurred.', '', { errors }), {
    status: 400,
  });

/** `…/attendance?periodId=&from=&to=` javobi (tyutor va admin mock'lari uchun umumiy). */
export function mockAttendanceResponse(
  studentId: string,
  periodId: string | null,
  from: string | null,
  to: string | null,
) {
  const period = resolveMockPeriod(studentId, periodId);
  if (!period) return periodNotFound();
  const range = clampAttendanceRange(period, from, to);
  if (range.from > range.to) return HttpResponse.json([]);
  const days = buildAttendance(studentId, period.id).filter(
    (d) => d.date >= range.from && d.date <= range.to,
  );
  return HttpResponse.json(days);
}

/** `…/diaries?periodId=` javobi — faqat tanlangan davr yozuvlari. */
export function mockDiariesResponse(studentId: string, periodId: string | null) {
  const period = resolveMockPeriod(studentId, periodId);
  if (!period) return periodNotFound();
  return HttpResponse.json(buildDiaries(studentId, period.id));
}

/** 1×1 shaffof PNG — `AuthImage` uchun haqiqiy blob (mock fayl xizmati). */
const PNG_1PX =
  'iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mP8z8BQDwAEhQGAhKmMIQAAAABJRU5ErkJggg==';

/** Eng kichik yaroqli PDF (mock fayl xizmati uchun). */
const PDF_STUB = '%PDF-1.4\n1 0 obj<</Type/Catalog>>endobj\ntrailer<</Root 1 0 R>>\n%%EOF\n';

function pdfBytes(): ArrayBuffer {
  const bytes = new Uint8Array(PDF_STUB.length);
  for (let i = 0; i < PDF_STUB.length; i++) bytes[i] = PDF_STUB.charCodeAt(i);
  return bytes.buffer;
}

function pngBytes(): ArrayBuffer {
  const binary = atob(PNG_1PX);
  const bytes = new Uint8Array(binary.length);
  for (let i = 0; i < binary.length; i++) bytes[i] = binary.charCodeAt(i);
  return bytes.buffer;
}

export const studentsHandlers: HttpHandler[] = [
  http.get('/api/tutor/students', () => HttpResponse.json(mockStudents)),

  http.get('/api/tutor/students/:id', ({ params, request }) => {
    const periodId = new URL(request.url).searchParams.get('periodId');
    const detail = buildDetail(String(params['id']), periodId);
    if (detail === 'periodNotFound') return periodNotFound();
    return detail ? HttpResponse.json(detail) : notFound();
  }),

  http.get('/api/tutor/students/:id/attendance', ({ params, request }) => {
    const studentId = String(params['id']);
    if (!PROFILES[studentId]) return notFound();
    const url = new URL(request.url);
    const from = url.searchParams.get('from');
    const to = url.searchParams.get('to');
    if (from && to && from > to)
      return validation({ From: ["`from` `to` dan katta bo'lmasligi kerak."] });
    return mockAttendanceResponse(studentId, url.searchParams.get('periodId'), from, to);
  }),

  http.get('/api/tutor/students/:id/diaries', ({ params, request }) => {
    const studentId = String(params['id']);
    if (!PROFILES[studentId]) return notFound();
    return mockDiariesResponse(studentId, new URL(request.url).searchParams.get('periodId'));
  }),

  /**
   * Fayl xizmati mock'i (`/api/files/{id}`) — Bearer token bilan so'raladi
   * (`AuthImage`, `AuthFileButton`). `doc-`/`contract-` id'lari PDF qaytaradi: talaba kundalikni
   * rasmga olib PDF qilib yuborgan holat. `missing*` → 404 (fallback ko'rinishini sinash uchun).
   */
  http.get('/api/files/:id', ({ params }) => {
    const id = String(params['id']);
    if (id.startsWith('missing'))
      return HttpResponse.json(problem(404, 'Topilmadi', 'Fayl topilmadi.'), { status: 404 });
    if (id.startsWith('doc-') || id.startsWith('contract-'))
      return HttpResponse.arrayBuffer(pdfBytes(), {
        headers: { 'Content-Type': 'application/pdf' },
      });
    return HttpResponse.arrayBuffer(pngBytes(), { headers: { 'Content-Type': 'image/png' } });
  }),
];
