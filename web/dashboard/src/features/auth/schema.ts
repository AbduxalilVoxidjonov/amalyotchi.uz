import { z } from 'zod';

/** LoginCommandValidator bilan mos: HEMIS ID — 5–20 raqam, parol ≥ 8 belgi. */
export const loginSchema = z.object({
  hemisId: z
    .string()
    .trim()
    .min(1, 'HEMIS ID ni kiriting.')
    .regex(/^\d{5,20}$/, 'HEMIS ID faqat raqamlardan iborat bo\'lishi kerak.'),
  password: z
    .string()
    .min(1, 'Parolni kiriting.')
    .min(8, "Parol kamida 8 ta belgidan iborat bo'lishi kerak."),
});

export type LoginFormValues = z.infer<typeof loginSchema>;
