import { http, HttpResponse, type HttpHandler } from 'msw';
import { SETTINGS_ENDPOINT } from './api';
import { accountSecurityHandlers } from './security/mocks';
import type { AdminSettings, Setting, SettingsUpdate } from './types';

const UPDATED = '2026-09-01T04:00:00+00:00';

/** Backend `AdminSettingsDto` shaklida (seed bilan bir xil kalitlar/yozuvlar). */
export const mockSettings: AdminSettings = {
  settings: [
    {
      key: 'geofenceRadius',
      label: 'Standart geofence radiusi',
      value: '200',
      type: 'int',
      unit: 'm',
      note: "Yangi korxona uchun standart radius; tyutor har korxona uchun o'zgartira oladi.",
      min: 50,
      max: 1000,
      updatedAt: UPDATED,
    },
    {
      key: 'lateTolerance',
      label: 'Kechikish chegarasi',
      value: '15',
      type: 'int',
      unit: 'min',
      note: 'Ish boshlanishidan shuncha daqiqa o\'tgach check-in "kech keldi" bo\'ladi.',
      min: 0,
      max: 120,
      updatedAt: UPDATED,
    },
    {
      key: 'minGpsAccuracy',
      label: 'Minimal GPS aniqligi',
      value: '100',
      type: 'int',
      unit: 'm',
      note: "Aniqlik shundan yomon bo'lsa check-in qabul qilinmaydi.",
      min: 10,
      max: 1000,
      updatedAt: UPDATED,
    },
    {
      key: 'autoCheckout',
      label: 'Avtomatik check-out',
      value: '60',
      type: 'int',
      unit: 'min',
      note: 'Ish tugagach shuncha daqiqa ichida check-out qilinmasa kun avtomatik yopiladi.',
      min: 0,
      max: 360,
      updatedAt: UPDATED,
    },
    {
      key: 'workDays',
      label: 'Ish kunlari',
      value: '1,2,3,4,5,6',
      type: 'weekdays',
      unit: null,
      note: '1 — Dushanba … 7 — Yakshanba.',
      min: null,
      max: null,
      updatedAt: UPDATED,
    },
    {
      key: 'dailyReportRequired',
      label: 'Kundalik hisobot majburiy',
      value: 'true',
      type: 'bool',
      unit: null,
      note: 'Yoqilgan bo\'lsa hisobotsiz kun "tugallanmagan" hisoblanadi.',
      min: null,
      max: null,
      updatedAt: UPDATED,
    },
    {
      key: 'minReportLength',
      label: 'Hisobotning minimal uzunligi',
      value: '150',
      type: 'int',
      unit: 'chars',
      note: 'Shundan qisqa hisobot qabul qilinmaydi.',
      min: 0,
      max: 5000,
      updatedAt: UPDATED,
    },
    {
      key: 'checkInWindow',
      label: 'Check-in oynasi',
      value: '90',
      type: 'int',
      unit: 'min',
      note: 'Ish boshlanishidan shuncha daqiqa o\'tgach check-in yopiladi va talaba "kelmadi" bo\'ladi.',
      min: 15,
      max: 480,
      updatedAt: UPDATED,
    },
    {
      key: 'checkinPhotoRequired',
      label: 'Check-in uchun rasm majburiy',
      value: 'true',
      type: 'bool',
      unit: null,
      note: "Yoqilgan bo'lsa check-in/check-out so'roviga selfi biriktirilmasa urinish qabul qilinmaydi.",
      min: null,
      max: null,
      updatedAt: UPDATED,
    },
    {
      key: 'checkinQrRequired',
      label: 'Check-in uchun QR kod majburiy',
      value: 'true',
      type: 'bool',
      unit: null,
      note: "Yoqilgan bo'lsa check-in/check-out uchun korxona QR kodi skanerlanishi shart.",
      min: null,
      max: null,
      updatedAt: UPDATED,
    },
    {
      key: 'maxStudentsPerCompany',
      label: 'Korxonaga maksimal talaba',
      value: '10',
      type: 'int',
      unit: null,
      note: "Shu sondan ko'p talaba bitta korxonaga biriktirilsa ogohlantirish chiqadi.",
      min: 1,
      max: 200,
      updatedAt: UPDATED,
    },
    {
      key: 'diaryPdfRequired',
      label: 'Hisobotga PDF majburiy',
      value: 'false',
      type: 'bool',
      unit: null,
      note: "Yoqilgan bo'lsa talaba kundalik hisobotiga kamida bitta PDF fayl biriktirmasa hisobot qabul qilinmaydi.",
      min: null,
      max: null,
      updatedAt: UPDATED,
    },
    {
      key: 'faceVerificationEnabled',
      label: 'Yuzni tekshirish',
      value: 'false',
      type: 'bool',
      unit: null,
      note: "Yoqilgan bo'lsa talaba etalon yuz rasmini yuboradi (tyutor tasdiqlaydi) va har check-in selfisi u bilan solishtiriladi.",
      min: null,
      max: null,
      updatedAt: UPDATED,
    },
    {
      key: 'faceMatchThreshold',
      label: 'Yuz moslik chegarasi',
      value: '36',
      type: 'int',
      unit: '%',
      note: "Selfi va etalon rasm o'xshashligi shundan past bo'lsa check-in rad etiladi.",
      min: 0,
      max: 100,
      updatedAt: UPDATED,
    },
  ],
  holidays: [
    { id: 'h0', date: '2000-01-01', name: 'Yangi yil', isRecurring: true },
    { id: 'h1', date: '2000-03-08', name: 'Xalqaro xotin-qizlar kuni', isRecurring: true },
    { id: 'h2', date: '2000-03-21', name: "Navro'z bayrami", isRecurring: true },
    { id: 'h3', date: '2000-05-09', name: 'Xotira va qadrlash kuni', isRecurring: true },
    { id: 'h4', date: '2000-09-01', name: 'Mustaqillik kuni', isRecurring: true },
    { id: 'h5', date: '2000-10-01', name: "O'qituvchi va murabbiylar kuni", isRecurring: true },
    { id: 'h6', date: '2000-12-08', name: 'Konstitutsiya kuni', isRecurring: true },
    { id: 'h7', date: '2027-03-20', name: "Navro'z (ko'chirilgan dam olish)", isRecurring: false },
  ],
  templates: [
    {
      id: 'd1',
      kind: 'contract',
      name: 'Uch tomonlama shartnoma namunasi',
      fileName: 'shartnoma_shablon.docx',
      url: '/api/files/d1',
    },
    {
      id: 'd2',
      kind: 'referral',
      name: "Yo'llanma shabloni",
      fileName: 'yollanma.docx',
      url: '/api/files/d2',
    },
    {
      id: 'd3',
      kind: 'reference',
      name: 'Tavsifnoma shabloni',
      fileName: 'tavsifnoma.docx',
      url: '/api/files/d3',
    },
  ],
};

/** Mock holati (PUT o'zgartiradi). Har GET — nusxa. */
let state: AdminSettings = structuredClone(mockSettings);

export function resetSettingsMock() {
  state = structuredClone(mockSettings);
}

/** Backend `SettingDefinition.Validate` ga o'xshash: int — butun son va [min,max]; bool — true/false; weekdays — 1..7. */
function validate(s: Setting, value: string): string | null {
  const v = value.trim();
  if (s.type === 'int') {
    if (!/^-?\d+$/.test(v)) return `'${s.key}' butun son bo'lishi kerak.`;
    const n = Number(v);
    if ((s.min != null && n < s.min) || (s.max != null && n > s.max))
      return `'${s.key}' ${s.min}–${s.max} oralig'ida bo'lishi kerak.`;
    return null;
  }
  if (s.type === 'bool')
    return v === 'true' || v === 'false' ? null : `'${s.key}' true yoki false bo'lishi kerak.`;
  return /^[1-7](,[1-7])*$/.test(v) ? null : `'${s.key}' 1–7 kunlar vergul bilan bo'lishi kerak.`;
}

export const settingsHandlers: HttpHandler[] = [
  ...accountSecurityHandlers,
  http.get(SETTINGS_ENDPOINT, () => HttpResponse.json(state)),
  http.put(SETTINGS_ENDPOINT, async ({ request }) => {
    const body = (await request.json().catch(() => ({}))) as Partial<SettingsUpdate>;
    const values = body.values ?? {};
    const errors: Record<string, string[]> = {};
    for (const [key, value] of Object.entries(values)) {
      const def = state.settings.find((s) => s.key === key);
      if (!def) {
        errors[key] = [`'${key}' noma'lum sozlama.`];
        continue;
      }
      const err = validate(def, value);
      if (err) errors[key] = [err];
    }
    if (Object.keys(errors).length > 0) {
      return HttpResponse.json(
        {
          title: "Ma'lumotlar noto'g'ri",
          status: 400,
          detail: "Kiritilgan ma'lumotlarda xatolik bor.",
          errors,
        },
        { status: 400, headers: { 'Content-Type': 'application/problem+json' } },
      );
    }
    const now = new Date().toISOString();
    state = {
      ...state,
      settings: state.settings.map((s) => {
        const v = values[s.key];
        return v !== undefined && v.trim() !== s.value
          ? { ...s, value: v.trim(), updatedAt: now }
          : s;
      }),
    };
    return HttpResponse.json(state);
  }),
];
