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

/** Xaritadan nuqta tanlanmagan holat — `lat`/`lng` `null` bo'ladi. */
export const MAP_POINT_REQUIRED = 'Xaritadan korxona joylashuvini belgilang.';

/**
 * Koordinata endi qo'lda emas, `MapPicker` dan keladi — shuning uchun qiymat son (yoki tanlanmagan
 * bo'lsa `null`). Serverga baribir `lat`/`lng` sonlari ketadi (backend kontrakti o'zgarmagan).
 */
function coordField(rangeMessage: string, min: number, max: number) {
  return z.number({ error: MAP_POINT_REQUIRED }).min(min, rangeMessage).max(max, rangeMessage);
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
  lat: coordField("Kenglik (lat) -90 va 90 oralig'ida bo'lishi kerak.", -90, 90),
  lng: coordField("Uzunlik (lng) -180 va 180 oralig'ida bo'lishi kerak.", -180, 180),
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

/**
 * Forma holati — matn maydonlari `<input value>`, koordinata esa xaritadan kelgan son
 * (hali tanlanmagan bo'lsa `null` → zod `MAP_POINT_REQUIRED` xatosini beradi).
 */
export type CompanyFormValues = Omit<z.input<typeof companySchema>, 'lat' | 'lng'> & {
  lat: number | null;
  lng: number | null;
};

/** Zod xatolarini `{ maydon: birinchi xabar }` ko'rinishiga yig'adi (tyutor formasidagi bilan bir xil). */
export function firstIssues<K extends string>(issues: readonly z.ZodIssue[]) {
  const next: Partial<Record<K, string>> = {};
  for (const issue of issues) {
    const key = issue.path[0] as K | undefined;
    if (key && !next[key]) next[key] = issue.message;
  }
  return next;
}
