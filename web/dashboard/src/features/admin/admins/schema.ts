import { z } from 'zod';
import { PASSWORD_MAX_LENGTH, PASSWORD_MIN_LENGTH } from '@/features/shared/password/schema';
import { normalizeUzPhone } from '../students/schema';

export { firstIssues } from '@/features/shared/password/schema';

export const ADMIN_FULL_NAME_MAX = 150;
export const ADMIN_LOGIN_MAX = 50;

const fullNameField = z
  .string()
  .trim()
  .min(1, 'FISH ni kiriting.')
  .min(2, `FISH 2–${ADMIN_FULL_NAME_MAX} belgi bo'lishi kerak.`)
  .max(ADMIN_FULL_NAME_MAX, `FISH 2–${ADMIN_FULL_NAME_MAX} belgi bo'lishi kerak.`);

/** Telefon ixtiyoriy: bo'sh → null; aks holda E.164 ga keltiriladi (talaba formasi qoidasi). */
const phoneField = z
  .string()
  .trim()
  .transform((s, ctx) => {
    if (s === '') return null;
    const normalized = normalizeUzPhone(s);
    if (!normalized) {
      ctx.addIssue({ code: 'custom', message: "Telefon raqami noto'g'ri. Namuna: +998901234567" });
      return z.NEVER;
    }
    return normalized;
  });

/** Login — bo'shliqsiz; to'liq qoidani (format, bandlik) server tekshiradi. */
const loginField = z
  .string()
  .trim()
  .min(1, 'Loginni kiriting.')
  .max(ADMIN_LOGIN_MAX, `Login ${ADMIN_LOGIN_MAX} belgidan oshmasligi kerak.`)
  .refine((s) => !/\s/.test(s), "Login bo'shliqsiz bo'lishi kerak.");

/** Parol ixtiyoriy: bo'sh — server vaqtinchalik parol yaratadi. */
const optionalPasswordField = z
  .string()
  .refine(
    (s) => s === '' || (s.length >= PASSWORD_MIN_LENGTH && s.length <= PASSWORD_MAX_LENGTH),
    `Parol ${PASSWORD_MIN_LENGTH}–${PASSWORD_MAX_LENGTH} ta belgidan iborat bo'lishi kerak.`,
  );

export const adminCreateSchema = z.object({
  fullName: fullNameField,
  login: loginField,
  phoneNumber: phoneField,
  password: optionalPasswordField,
});

export const adminEditSchema = z.object({
  fullName: fullNameField,
  phoneNumber: phoneField,
});

export type AdminCreateFormValues = z.input<typeof adminCreateSchema>;
