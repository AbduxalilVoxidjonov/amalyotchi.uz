import { z } from 'zod';

/**
 * Backend `CompanyValidationRules` bilan bir xil xabarlar — forma maydonlari ostida shu matn chiqadi,
 * server 400 qaytarsa ham (`errors.name`, `errors.tin`, …) foydalanuvchi bir xil matnni ko'radi.
 * Formadagi barcha qiymatlar — matn; zod ularni `CompanyInput` shakliga (son/null) aylantiradi.
 */

const PHONE_MESSAGE = "Telefon raqami noto'g'ri. Namuna: +998901234567";
const PHONE_RE = /^\+998\d{9}$/;

/** Majburiy matn maydoni: bo'shligi va uzunligi tekshiriladi. */
function textField(required: string, max: number, tooLong: string) {
  return z.string().trim().min(1, required).max(max, tooLong);
}

/** Ixtiyoriy matn: bo'sh bo'lsa `null` (backend `string?`). */
function optionalTextField(max: number, tooLong: string) {
  return z
    .string()
    .trim()
    .max(max, tooLong)
    .transform((s) => (s === '' ? null : s));
}

/** STIR — 9 ta raqam; kiritishda bo'shliqlarga yo'l qo'yiladi ("305 881 204"). */
const tinField = z
  .string()
  .trim()
  .transform((s) => s.replace(/\s/g, ''))
  .refine((s) => s !== '', 'STIR ni kiriting.')
  .refine(
    (s) => s === '' || /^\d{9}$/.test(s),
    "STIR 9 ta raqamdan iborat bo'lishi kerak. Namuna: 123456789",
  );

/** Koordinata: "41,3111" ham qabul qilinadi (vergul → nuqta). */
function coordField(required: string, message: string, min: number, max: number) {
  return z
    .string()
    .trim()
    .transform((s) => s.replace(',', '.'))
    .refine((s) => s !== '', required)
    .refine((s) => {
      if (s === '') return true;
      if (!/^-?\d+(\.\d+)?$/.test(s)) return false;
      const n = Number(s);
      return n >= min && n <= max;
    }, message)
    .transform(Number);
}

/** Radius ixtiyoriy: bo'sh bo'lsa `null` (server sozlamadan oladi), aks holda 50–1000. */
const radiusField = z
  .string()
  .trim()
  .refine(
    (s) => s === '' || (/^\d+$/.test(s) && Number(s) >= 50 && Number(s) <= 1000),
    "Radius 50–1000 m oralig'ida bo'lishi kerak.",
  )
  .transform((s) => (s === '' ? null : Number(s)));

/** Telefon: bo'shliq/defis/qavslar olib tashlanadi, so'ng E.164 "+998901234567". */
function phoneField(required: string) {
  return z
    .string()
    .trim()
    .transform((s) => s.replace(/[\s\-()]/g, ''))
    .refine((s) => s !== '', required)
    .refine((s) => s === '' || PHONE_RE.test(s), PHONE_MESSAGE);
}

const optionalPhoneField = z
  .string()
  .trim()
  .transform((s) => s.replace(/[\s\-()]/g, ''))
  .refine((s) => s === '' || PHONE_RE.test(s), PHONE_MESSAGE)
  .transform((s) => (s === '' ? null : s));

/** "Yangi korxona" / "Korxonani tahrirlash" — bitta shakl (POST va PUT body'lari bir xil). */
export const companySchema = z.object({
  name: textField('Korxona nomini kiriting.', 200, 'Korxona nomi 200 belgidan oshmasligi kerak.'),
  tin: tinField,
  activity: textField(
    'Faoliyat turini kiriting.',
    200,
    'Faoliyat turi 200 belgidan oshmasligi kerak.',
  ),
  address: textField('Manzilni kiriting.', 500, 'Manzil 500 belgidan oshmasligi kerak.'),
  lat: coordField(
    'Kenglikni (lat) kiriting.',
    "Kenglik (lat) -90 va 90 oralig'ida bo'lishi kerak.",
    -90,
    90,
  ),
  lng: coordField(
    'Uzunlikni (lng) kiriting.',
    "Uzunlik (lng) -180 va 180 oralig'ida bo'lishi kerak.",
    -180,
    180,
  ),
  radiusM: radiusField,
  supervisorName: textField(
    'Rahbar FISH ni kiriting.',
    200,
    'Rahbar FISH 200 belgidan oshmasligi kerak.',
  ),
  supervisorPhone: phoneField('Rahbar telefonini kiriting.'),
  mentorName: optionalTextField(200, 'Mentor FISH 200 belgidan oshmasligi kerak.'),
  mentorPhone: optionalPhoneField,
});

/** Forma holati — barcha maydonlar matn (`<input value>`). */
export type CompanyFormValues = z.input<typeof companySchema>;

/** Zod xatolarini `{ maydon: birinchi xabar }` ko'rinishiga yig'adi (tyutor formasidagi bilan bir xil). */
export function firstIssues<K extends string>(issues: readonly z.ZodIssue[]) {
  const next: Partial<Record<K, string>> = {};
  for (const issue of issues) {
    const key = issue.path[0] as K | undefined;
    if (key && !next[key]) next[key] = issue.message;
  }
  return next;
}
