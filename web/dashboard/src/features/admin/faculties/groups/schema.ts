import { z } from 'zod';

/** hierarchy-contract.md — guruh `name` (2–20, "412-22" uslubida) + `course` (1–6). */
export const groupSchema = z.object({
  name: z
    .string()
    .trim()
    .min(1, 'Guruh nomini kiriting.')
    .regex(
      /^[A-Za-z0-9-]{2,20}$/,
      "Guruh nomi 2–20 ta lotin harf, raqam yoki '-' dan iborat bo'lishi kerak.",
    ),
  course: z.coerce
    .number()
    .int("Kurs 1–6 oralig'ida bo'lishi kerak.")
    .min(1, "Kurs 1–6 oralig'ida bo'lishi kerak.")
    .max(6, "Kurs 1–6 oralig'ida bo'lishi kerak."),
});

export type GroupFormValues = z.infer<typeof groupSchema>;

export const COURSE_OPTIONS = [1, 2, 3, 4, 5, 6].map((n) => ({
  value: String(n),
  label: `${n}-kurs`,
}));
