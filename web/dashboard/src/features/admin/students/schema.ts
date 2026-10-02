import { z } from 'zod';

export { firstIssues } from '@/features/shared/password/schema';

/**
 * "Talaba qo'shish" formasi. Qoidalar backend bilan bir xil (`ImportStudentsCommand` /
 * `HemisId` / `PhoneNumber` value object'lari): FISH ≤ 200, HEMIS ID 5–20 raqam,
 * telefon — O'zbekiston raqami (9 raqam yoki 998 bilan 12 raqam).
 */
export const STUDENT_FULL_NAME_MAX = 200;
export const HEMIS_ID_MIN = 5;
export const HEMIS_ID_MAX = 20;

export const STUDENT_FORM_MESSAGES = {
  fullNameRequired: "FISH bo'sh.",
  fullNameLength: `FISH ${STUDENT_FULL_NAME_MAX} ta belgidan oshmasligi kerak.`,
  hemisRequired: "HEMIS ID bo'sh.",
  hemisFormat: `HEMIS ID ${HEMIS_ID_MIN}–${HEMIS_ID_MAX} ta raqamdan iborat bo'lishi kerak.`,
  groupRequired: "Guruh bo'sh.",
  phoneFormat: "Telefon raqami noto'g'ri. Namuna: +998901234567",
} as const;

/**
 * `PhoneNumber.TryNormalize` ning nusxasi: raqamlardan boshqasi tashlanadi, 9 raqamga "998"
 * qo'shiladi; natija 998 bilan boshlanuvchi 12 raqam bo'lsa — E.164 "+998901234567", aks holda null.
 */
export function normalizeUzPhone(raw: string): string | null {
  let digits = raw.replace(/\D/g, '');
  if (digits.length === 9) digits = `998${digits}`;
  return digits.length === 12 && digits.startsWith('998') ? `+${digits}` : null;
}

const hemisPattern = new RegExp(`^\\d{${HEMIS_ID_MIN},${HEMIS_ID_MAX}}$`);

export const studentCreateSchema = z.object({
  fullName: z
    .string()
    .trim()
    .min(1, STUDENT_FORM_MESSAGES.fullNameRequired)
    .max(STUDENT_FULL_NAME_MAX, STUDENT_FORM_MESSAGES.fullNameLength),
  hemisId: z
    .string()
    .trim()
    .min(1, STUDENT_FORM_MESSAGES.hemisRequired)
    .regex(hemisPattern, STUDENT_FORM_MESSAGES.hemisFormat),
  groupId: z.string().min(1, STUDENT_FORM_MESSAGES.groupRequired),
  /** Bo'sh — null (ixtiyoriy); aks holda E.164 ga normallashtiriladi. */
  phoneNumber: z
    .string()
    .trim()
    .transform((s, ctx) => {
      if (s === '') return null;
      const normalized = normalizeUzPhone(s);
      if (!normalized) {
        ctx.addIssue({ code: 'custom', message: STUDENT_FORM_MESSAGES.phoneFormat });
        return z.NEVER;
      }
      return normalized;
    }),
});

export type StudentCreateFormValues = z.input<typeof studentCreateSchema>;
