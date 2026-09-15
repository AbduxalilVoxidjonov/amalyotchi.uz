import { z } from 'zod';

/** Backend `FacultyCommandValidator` bilan bir xil: nom 2–150, kod 2–10 lotin harf/raqam. */
export const facultySchema = z.object({
  name: z
    .string()
    .trim()
    .min(1, 'Fakultet nomini kiriting.')
    .min(2, "Fakultet nomi 2–150 belgi bo'lishi kerak.")
    .max(150, "Fakultet nomi 2–150 belgi bo'lishi kerak."),
  code: z
    .string()
    .trim()
    .min(1, 'Fakultet kodini kiriting.')
    .regex(
      /^[A-Za-z0-9]{2,10}$/,
      "Fakultet kodi 2–10 ta lotin harf yoki raqamdan iborat bo'lishi kerak.",
    ),
});

export type FacultyFormValues = z.infer<typeof facultySchema>;
