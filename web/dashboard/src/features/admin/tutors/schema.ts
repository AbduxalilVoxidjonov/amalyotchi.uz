import { z } from 'zod';
import { passwordField } from '@/features/shared/password/schema';

// Parol sxemasi va zod yordamchisi talaba parol modali bilan umumiy (`features/shared/password`).
export {
  firstIssues,
  passwordResetSchema,
  type PasswordResetFormValues,
} from '@/features/shared/password/schema';

/** Telefon: bo'sh (ixtiyoriy) yoki E.164 "+998901234567". Bo'shliq/defis/qavslar olib tashlanadi. */
const phoneField = z
  .string()
  .trim()
  .transform((s) => s.replace(/[\s\-()]/g, ''))
  .refine((s) => s === '' || /^\+998\d{9}$/.test(s), {
    message: "Telefon +998 XX XXX-XX-XX shaklida bo'lishi kerak.",
  });

const fullNameField = z
  .string()
  .trim()
  .min(1, 'FISH ni kiriting.')
  .min(2, "FISH 2–150 belgi bo'lishi kerak.")
  .max(150, "FISH 2–150 belgi bo'lishi kerak.");

/** `LoginCommandValidator` bilan bir xil: 5–20 raqam. */
const hemisIdField = z
  .string()
  .trim()
  .min(1, 'HEMIS ID ni kiriting.')
  .regex(/^\d{5,20}$/, "HEMIS ID 5–20 ta raqamdan iborat bo'lishi kerak.");

/** Ko'p tanlov: kamida bitta fakultet. */
const facultyIdsField = z.array(z.string()).min(1, 'Kamida bitta fakultet tanlang');

/** "Yangi tyutor" formasi — POST body'siga mos (parol ham shu yerda). */
export const tutorCreateSchema = z.object({
  fullName: fullNameField,
  hemisId: hemisIdField,
  phone: phoneField,
  password: passwordField,
  facultyIds: facultyIdsField,
});

/** "Tyutorni tahrirlash" — PUT body'si (HEMIS ID va parol o'zgartirilmaydi). */
export const tutorEditSchema = z.object({
  fullName: fullNameField,
  phone: phoneField,
  facultyIds: facultyIdsField,
});

export type TutorCreateFormValues = z.input<typeof tutorCreateSchema>;
export type TutorEditFormValues = z.input<typeof tutorEditSchema>;
