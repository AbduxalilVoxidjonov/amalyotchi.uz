import { z } from 'zod';

/** LoginCommandValidator bilan mos: telefon bo'sh emas, parol ≥ 8 belgi. */
export const loginSchema = z.object({
  phoneNumber: z
    .string()
    .trim()
    .min(1, 'Telefon raqamini kiriting.')
    .regex(/^\+?\d[\d\s-]{8,}$/, "Telefon raqami noto'g'ri formatda."),
  password: z
    .string()
    .min(1, 'Parolni kiriting.')
    .min(8, "Parol kamida 8 ta belgidan iborat bo'lishi kerak."),
});

export type LoginFormValues = z.infer<typeof loginSchema>;
