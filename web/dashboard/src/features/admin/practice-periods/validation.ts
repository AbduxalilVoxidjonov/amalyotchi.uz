import { isApiError } from '@/shared/api';
import { calendarDays, isIsoDate } from './dates';

export interface PeriodFormValues {
  name: string;
  startDate: string;
  endDate: string;
}

export type PeriodFormErrors = Partial<Record<keyof PeriodFormValues, string>>;

export const EMPTY_PERIOD_FORM: PeriodFormValues = { name: '', startDate: '', endDate: '' };

/** Kontrakt qoidalari: nom 1–200, ikkala sana, `endDate >= startDate`. */
export function validatePeriodForm(v: PeriodFormValues): PeriodFormErrors {
  const errors: PeriodFormErrors = {};
  const name = v.name.trim();
  if (!name) errors.name = 'Davr nomini kiriting.';
  else if (name.length > 200) errors.name = 'Nom 200 belgidan oshmasligi kerak.';
  if (!isIsoDate(v.startDate)) errors.startDate = 'Boshlanish sanasini kiriting.';
  if (!isIsoDate(v.endDate)) errors.endDate = 'Tugash sanasini kiriting.';
  else if (isIsoDate(v.startDate) && calendarDays(v.startDate, v.endDate) === null)
    errors.endDate = "Tugash sanasi boshlanish sanasidan oldin bo'lishi mumkin emas.";
  return errors;
}

/** Server xatosi: umumiy matn (`detail`) + maydon xatolari ro'yxati (`errors`). */
export function serverErrorLines(error: unknown): { message: string; details: string[] } | null {
  if (!error) return null;
  if (!isApiError(error)) return { message: 'Kutilmagan xatolik yuz berdi.', details: [] };
  const details = Object.values(error.fieldErrors).flat();
  return { message: error.message, details: details.filter((d) => d !== error.message) };
}
