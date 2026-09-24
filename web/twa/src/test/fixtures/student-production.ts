/**
 * Production API javoblari shakli (demo talaba, 2026-09-24) — shaxsiy ma'lumotlar demo qiymatlarga
 * almashtirilgan (ism, HEMIS ID, telefonlar, korxona, id'lar). Mock'lardan farqlari (regressiya uchun muhim):
 *  - davr `status: "closed"` (endDate kelajakda bo'lsa ham), bugun check-in `absent` + `note` matni;
 *  - `distanceM`/`gpsAccuracyM`/`checkInAt` — null; `place.contract` — null; `place.comment` — matn;
 *  - `portfolio.grade`/`conclusion`/`pdfUrl` — null, `finalized: false`; `profile.practice.grade` — null;
 *  - kundalik: `rewrite`/`seen`/`approved`/`submitted`, fayli bor va yo'q, `score` null va raqam;
 *  - kalendar: `present`/`late`/`absent`/`dayOff`/`future` aralash, butun oy.
 * `tabs-production-data.test.tsx` shu javoblar bilan barcha tablarni ochadi.
 */

export const prodToday = {
  date: '2026-09-24',
  window: {
    start: '09:00',
    end: '09:15',
    closesAt: '10:30',
    checkoutAt: '17:00',
    isOpen: false,
  },
  checkin: {
    status: 'absent',
    checkInAt: null,
    checkOutAt: null,
    distanceM: null,
    radiusM: 150,
    gpsAccuracyM: null,
    suspicious: false,
    autoClosed: false,
    note: 'Amaliyot davri tugagan: Ishlab chiqarish amaliyoti 2026.',
    photoRequired: true,
    qrRequired: true,
  },
  place: {
    company: 'Demo Korxona MChJ',
    address: "Toshkent, Demo ko'chasi 1",
    radiusM: 150,
    attendancePct: 54.5,
    daysPresent: 12,
    daysTotal: 22,
    reports: 10,
    avgScore: 4,
  },
  diary: {
    submittedToday: false,
    minChars: 0,
    maxFiles: 5,
    pdfRequired: false,
  },
  period: {
    id: '0190aaaa-0000-7000-8000-000000000001',
    name: 'Ishlab chiqarish amaliyoti 2026',
    startDate: '2026-08-31',
    endDate: '2026-10-14',
    status: 'closed',
    isDefault: true,
  },
};

export const prodDiary = [
  {
    id: '0190aaaa-0000-7000-8000-000000000100',
    date: '2026-09-14',
    submittedAt: '2026-09-14T13:30:00+00:00',
    status: 'rewrite',
    text: '14-kun. Demo korxonada kundalik ish: topshiriqlar mentor bilan kelishildi, bajarildi va natijalar rahbarga taqdim etildi. 14-kun. Demo korxonada kundalik ish: topshiriqlar mentor bilan kelishildi, bajarildi va natijalar rahbarga taqdim etildi.',
    learned: 'Unit testlar yozish amaliyoti',
    files: [],
    score: null,
    comment: 'Batafsil yozing',
    periodId: '0190aaaa-0000-7000-8000-000000000001',
    periodName: 'Ishlab chiqarish amaliyoti 2026',
  },
  {
    id: '0190aaaa-0000-7000-8000-000000000101',
    date: '2026-09-11',
    submittedAt: '2026-09-11T13:30:00+00:00',
    status: 'seen',
    text: '11-kun. Demo korxonada kundalik ish: topshiriqlar mentor bilan kelishildi, bajarildi va natijalar rahbarga taqdim etildi. 11-kun. Demo korxonada kundalik ish: topshiriqlar mentor bilan kelishildi, bajarildi va natijalar rahbarga taqdim etildi.',
    learned: "SQL so'rovlarini optimallashtirish",
    files: [
      {
        id: '0190aaaa-0000-7000-8000-000000000210',
        name: 'kundalik_2026-09-11.pdf',
        url: '/api/files/0190aaaa-0000-7000-8000-000000000210',
      },
    ],
    score: null,
    comment: null,
    periodId: '0190aaaa-0000-7000-8000-000000000001',
    periodName: 'Ishlab chiqarish amaliyoti 2026',
  },
  {
    id: '0190aaaa-0000-7000-8000-000000000102',
    date: '2026-09-08',
    submittedAt: '2026-09-08T13:30:00+00:00',
    status: 'approved',
    text: '08-kun. Demo korxonada kundalik ish: topshiriqlar mentor bilan kelishildi, bajarildi va natijalar rahbarga taqdim etildi. 08-kun. Demo korxonada kundalik ish: topshiriqlar mentor bilan kelishildi, bajarildi va natijalar rahbarga taqdim etildi.',
    learned: 'Unit testlar yozish amaliyoti',
    files: [
      {
        id: '0190aaaa-0000-7000-8000-000000000220',
        name: 'kundalik_2026-09-08.pdf',
        url: '/api/files/0190aaaa-0000-7000-8000-000000000220',
      },
    ],
    score: 3,
    comment: null,
    periodId: '0190aaaa-0000-7000-8000-000000000001',
    periodName: 'Ishlab chiqarish amaliyoti 2026',
  },
  {
    id: '0190aaaa-0000-7000-8000-000000000103',
    date: '2026-09-04',
    submittedAt: '2026-09-04T13:30:00+00:00',
    status: 'submitted',
    text: '04-kun. Demo korxonada kundalik ish: topshiriqlar mentor bilan kelishildi, bajarildi va natijalar rahbarga taqdim etildi. 04-kun. Demo korxonada kundalik ish: topshiriqlar mentor bilan kelishildi, bajarildi va natijalar rahbarga taqdim etildi.',
    learned: "Git branch'lar bilan ishlash va pull request tartibi",
    files: [],
    score: null,
    comment: null,
    periodId: '0190aaaa-0000-7000-8000-000000000001',
    periodName: 'Ishlab chiqarish amaliyoti 2026',
  },
  {
    id: '0190aaaa-0000-7000-8000-000000000104',
    date: '2026-09-02',
    submittedAt: '2026-09-02T13:30:00+00:00',
    status: 'seen',
    text: '02-kun. Demo korxonada kundalik ish: topshiriqlar mentor bilan kelishildi, bajarildi va natijalar rahbarga taqdim etildi. 02-kun. Demo korxonada kundalik ish: topshiriqlar mentor bilan kelishildi, bajarildi va natijalar rahbarga taqdim etildi.',
    learned: 'Unit testlar yozish amaliyoti',
    files: [],
    score: null,
    comment: null,
    periodId: '0190aaaa-0000-7000-8000-000000000001',
    periodName: 'Ishlab chiqarish amaliyoti 2026',
  },
];

export const prodCalendar = {
  month: '2026-09',
  studentName: 'Demo Talaba',
  groupName: '412-22',
  days: [
    {
      date: '2026-09-01',
      status: 'present',
    },
    {
      date: '2026-09-02',
      status: 'present',
    },
    {
      date: '2026-09-03',
      status: 'present',
    },
    {
      date: '2026-09-04',
      status: 'present',
    },
    {
      date: '2026-09-05',
      status: 'present',
    },
    {
      date: '2026-09-06',
      status: 'dayOff',
    },
    {
      date: '2026-09-07',
      status: 'late',
    },
    {
      date: '2026-09-08',
      status: 'present',
    },
    {
      date: '2026-09-09',
      status: 'late',
    },
    {
      date: '2026-09-10',
      status: 'absent',
    },
    {
      date: '2026-09-11',
      status: 'present',
    },
    {
      date: '2026-09-12',
      status: 'present',
    },
    {
      date: '2026-09-13',
      status: 'dayOff',
    },
    {
      date: '2026-09-14',
      status: 'present',
    },
    {
      date: '2026-09-15',
      status: 'absent',
    },
    {
      date: '2026-09-16',
      status: 'absent',
    },
    {
      date: '2026-09-17',
      status: 'absent',
    },
    {
      date: '2026-09-18',
      status: 'absent',
    },
    {
      date: '2026-09-19',
      status: 'absent',
    },
    {
      date: '2026-09-20',
      status: 'dayOff',
    },
    {
      date: '2026-09-21',
      status: 'absent',
    },
    {
      date: '2026-09-22',
      status: 'absent',
    },
    {
      date: '2026-09-23',
      status: 'absent',
    },
    {
      date: '2026-09-24',
      status: 'absent',
    },
    {
      date: '2026-09-25',
      status: 'future',
    },
    {
      date: '2026-09-26',
      status: 'future',
    },
    {
      date: '2026-09-27',
      status: 'dayOff',
    },
    {
      date: '2026-09-28',
      status: 'future',
    },
    {
      date: '2026-09-29',
      status: 'future',
    },
    {
      date: '2026-09-30',
      status: 'future',
    },
  ],
};

export const prodPlace = {
  status: 'approved',
  comment: "Hujjatlar to'liq",
  company: 'Demo Korxona MChJ',
  tin: '300000001',
  activity: "Dasturiy ta'minot ishlab chiqish",
  address: "Toshkent, Demo ko'chasi 1",
  supervisorName: 'Rahbar D.',
  supervisorPhone: '+998900000011',
  mentorName: 'Mentor D.',
  mentorPhone: '+998900000012',
  radiusM: 150,
  lat: 41.3111,
  lng: 69.2797,
  periodFrom: '2026-08-31',
  periodTo: '2026-10-14',
  contract: null,
};

export const prodProfile = {
  id: '0190aaaa-0000-7000-8000-000000000010',
  fullName: 'Demo Talaba',
  hemisId: '900001',
  phoneNumber: '+998900000001',
  faculty: 'Axborot texnologiyalari',
  department: 'Umumiy kafedra',
  direction: 'Dasturiy injiniring',
  group: '412-22',
  course: 3,
  tutor: {
    fullName: 'Demo Tyutor',
    phoneNumber: '+998900000002',
  },
  telegramLinked: true,
  hasPassword: true,
  mustChangePassword: false,
  practice: {
    period: {
      id: '0190aaaa-0000-7000-8000-000000000001',
      name: 'Ishlab chiqarish amaliyoti 2026',
      status: 'closed',
      startDate: '2026-08-31',
      endDate: '2026-10-14',
    },
    company: {
      id: '0190aaaa-0000-7000-8000-000000000020',
      name: 'Demo Korxona MChJ',
      address: "Toshkent, Demo ko'chasi 1",
    },
    elapsedWorkDays: 22,
    attendancePct: 54.5,
    suspiciousDays: 0,
    total: 73.8,
    grade: null,
    finalized: false,
  },
};

export const prodPortfolio = {
  student: 'Demo Talaba',
  group: '412-22',
  practiceTitle: 'Ishlab chiqarish amaliyoti 2026',
  company: 'Demo Korxona MChJ',
  periodFrom: '2026-08-31',
  periodTo: '2026-10-14',
  stats: {
    attendancePct: 54.5,
    daysPresent: 12,
    daysTotal: 22,
    late: 2,
    excused: 0,
    reports: 10,
    avgScore: 4,
  },
  score: [
    {
      key: 'attendance',
      weightPct: 40,
      points: 21.8,
    },
    {
      key: 'reports',
      weightPct: 30,
      points: 24,
    },
    {
      key: 'tutor',
      weightPct: 20,
      points: 18,
    },
    {
      key: 'reference',
      weightPct: 10,
      points: 10,
    },
  ],
  total: 73.8,
  grade: null,
  finalized: false,
  conclusion: null,
  pdfUrl: null,
  periodId: '0190aaaa-0000-7000-8000-000000000001',
  periods: [
    {
      id: '0190aaaa-0000-7000-8000-000000000001',
      name: 'Ishlab chiqarish amaliyoti 2026',
      startDate: '2026-08-31',
      endDate: '2026-10-14',
      status: 'closed',
      isDefault: true,
    },
  ],
};
