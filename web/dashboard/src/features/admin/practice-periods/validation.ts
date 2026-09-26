import { isApiError } from '@/shared/api';
import { parseWeekdays } from '../shared/weekdays';
import { calendarDays, isIsoDate } from './dates';

export interface PeriodFormValues {
  name: string;
  startDate: string;
  endDate: string;
  /** "HH:mm" */
  dailyStart: string;
  /** "HH:mm" */
  dailyEnd: string;
  /** Tartiblangan CSV ("1,2,3,4,5,6"). */
  workDays: string;
}

export type PeriodFormField = keyof PeriodFormValues;
export type PeriodFormErrors = Partial<Record<PeriodFormField, string>>;

/** Xatolar ko'rsatiladigan tartib (forma bo'ylab yuqoridan pastga). */
export const PERIOD_FORM_FIELDS: readonly PeriodFormField[] = [
  'name',
  'startDate',
  'endDate',
  'workDays',
  'dailyStart',
  'dailyEnd',
];

/** Sozlamalar yuklanmasa/bo'sh bo'lsa ishlatiladigan standart ish kunlari (Du–Sh). */
export const DEFAULT_WORK_DAYS = '1,2,3,4,5,6';
export const DEFAULT_DAILY_START = '09:00';
export const DEFAULT_DAILY_END = '17:00';

export const EMPTY_PERIOD_FORM: PeriodFormValues = {
  name: '',
  startDate: '',
  endDate: '',
  dailyStart: DEFAULT_DAILY_START,
  dailyEnd: DEFAULT_DAILY_END,
  workDays: DEFAULT_WORK_DAYS,
};

/** Faol davr: ish kunlari o'zgarishi RETROAKTIV (backend butun davrni qayta hisoblaydi). */
export const ACTIVE_WORK_DAYS_WARNING =
  "Faol davrda ish kunlarini o'zgartirish butun davr statistikasini (talab qilinadigan kunlar, o'tgan kunlar holati) qayta hisoblaydi. Belgilangan davomat yozuvlari o'zgarmaydi.";
export const ACTIVE_DAILY_TIME_HINT = 'Yangi vaqt darhol (bugundan) kuchga kiradi.';

const HH_MM = /^([01]\d|2[0-3]):[0-5]\d$/;

export function isHhMm(v: string): boolean {
  return HH_MM.test(v);
}

/**
 * Kontrakt qoidalari: nom 1–200, ikkala sana, `endDate >= startDate`; kamida bitta ish kuni;
 * vaqtlar "HH:mm" va `dailyEnd > dailyStart` ("HH:mm" satrlarini solishtirish to'g'ri).
 */
export function validatePeriodForm(v: PeriodFormValues): PeriodFormErrors {
  const errors: PeriodFormErrors = {};
  const name = v.name.trim();
  if (!name) errors.name = 'Davr nomini kiriting.';
  else if (name.length > 200) errors.name = 'Nom 200 belgidan oshmasligi kerak.';
  if (!isIsoDate(v.startDate)) errors.startDate = 'Boshlanish sanasini kiriting.';
  if (!isIsoDate(v.endDate)) errors.endDate = 'Tugash sanasini kiriting.';
  else if (isIsoDate(v.startDate) && calendarDays(v.startDate, v.endDate) === null)
    errors.endDate = "Tugash sanasi boshlanish sanasidan oldin bo'lishi mumkin emas.";

  if (parseWeekdays(v.workDays).size === 0) errors.workDays = 'Kamida bitta ish kunini tanlang.';
  if (!isHhMm(v.dailyStart)) errors.dailyStart = 'Boshlanish vaqtini kiriting (masalan, 09:00).';
  if (!isHhMm(v.dailyEnd)) errors.dailyEnd = 'Tugash vaqtini kiriting (masalan, 17:00).';
  else if (isHhMm(v.dailyStart) && v.dailyEnd <= v.dailyStart)
    errors.dailyEnd = "Ish tugash vaqti boshlanish vaqtidan keyin bo'lishi kerak.";
  return errors;
}

/** Server xatosi: umumiy matn (`detail`) + maydon xatolari ro'yxati (`errors`). */
export function serverErrorLines(error: unknown): { message: string; details: string[] } | null {
  if (!error) return null;
  if (!isApiError(error)) return { message: 'Kutilmagan xatolik yuz berdi.', details: [] };
  const details = Object.values(error.fieldErrors).flat();
  return { message: error.message, details: details.filter((d) => d !== error.message) };
}
