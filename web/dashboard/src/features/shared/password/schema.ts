import { z } from 'zod';

/**
 * Parol validatsiyasi — tyutor parolini tiklash va talaba parolini o'rnatish modallari uchun umumiy.
 * Backend (`Password` qoidasi) bilan bir xil: 8–128 belgi.
 */
export const PASSWORD_MIN_LENGTH = 8;
export const PASSWORD_MAX_LENGTH = 128;

export const passwordField = z
  .string()
  .min(1, 'Parolni kiriting.')
  .min(PASSWORD_MIN_LENGTH, "Parol kamida 8 ta belgidan iborat bo'lishi kerak.")
  .max(PASSWORD_MAX_LENGTH, 'Parol 128 ta belgidan oshmasligi kerak.');

/** Yangi parol + tasdiq. */
export const passwordResetSchema = z
  .object({
    password: passwordField,
    confirm: z.string().min(1, 'Parolni qayta kiriting.'),
  })
  .refine((v) => v.password === v.confirm, {
    message: 'Parollar mos kelmadi.',
    path: ['confirm'],
  });

export type PasswordResetFormValues = z.input<typeof passwordResetSchema>;

/** Zod xatolarini `{ maydon: birinchi xabar }` ko'rinishiga yig'adi. */
export function firstIssues<K extends string>(issues: readonly z.ZodIssue[]) {
  const next: Partial<Record<K, string>> = {};
  for (const issue of issues) {
    const key = issue.path[0] as K | undefined;
    if (key && !next[key]) next[key] = issue.message;
  }
  return next;
}
