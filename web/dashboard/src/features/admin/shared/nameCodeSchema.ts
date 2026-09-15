import { z } from 'zod';

/**
 * Kafedra va yo'nalish — bir xil forma shakli (`name` + `code`), bir xil validatsiya matnlari
 * (hierarchy-contract.md): backend `FluentValidation` bilan bir xil. `EntityFormModal` shu bilan ishlaydi.
 */
export const nameCodeSchema = z.object({
  name: z
    .string()
    .trim()
    .min(1, 'Nomni kiriting.')
    .min(2, "Nom 2–150 belgi bo'lishi kerak.")
    .max(150, "Nom 2–150 belgi bo'lishi kerak."),
  code: z
    .string()
    .trim()
    .min(1, 'Kodni kiriting.')
    .regex(
      /^[A-Za-z0-9-]{2,20}$/,
      "Kod 2–20 ta lotin harf, raqam yoki '-' dan iborat bo'lishi kerak.",
    ),
});

export type NameCodeFormValues = z.infer<typeof nameCodeSchema>;
