import type { StatusKind } from '@amaliyotchi/shared/ui';

/** Domain `ApplicationStatus` (Practice) — JSON'da camelCase string. */
export type ApplicationStatus =
  | 'draft'
  | 'submitted'
  | 'revisionNeeded'
  | 'approved'
  | 'rejected'
  | 'completed'
  /** Admin talabani boshqa korxonaga o'tkazgan — eski ariza yopiladi. */
  | 'transferred';

/** Shartnoma fayli (`PracticeContractDto`). */
export interface PracticeContractDto {
  fileId: string;
  fileName: string;
  pages: number | null;
  sizeBytes: number;
  /** ISO datetime */
  uploadedAt: string;
  /** null — hali tasdiqlanmagan */
  approvedAt: string | null;
  approvedBy: string | null;
  /** Shablon havolasi (WebApp.openLink) | null */
  templateUrl: string | null;
}

/** GET /api/student/place (`PracticePlaceDto`). Ariza yo'q → 404. */
export interface PracticePlaceDto {
  status: ApplicationStatus;
  /** Tyutor izohi (qaytarilgan/rad etilgan arizada). */
  comment: string | null;
  company: string;
  /** STIR "304512889" — UI 3-3-3 qilib ko'rsatadi. */
  tin: string;
  activity: string;
  address: string;
  supervisorName: string;
  supervisorPhone: string;
  mentorName: string | null;
  mentorPhone: string | null;
  radiusM: number;
  lat: number;
  lng: number;
  /** DateOnly — amaliyot davri. */
  periodFrom: string;
  periodTo: string;
  contract: PracticeContractDto | null;
  /** Ariza tegishli amaliyot davri (eski server yubormasligi mumkin). */
  periodId?: string;
  /** Davr nomi, masalan "Kuzgi amaliyot 2026". */
  periodName?: string;
  /**
   * Davr yopilgan yoki tugash sanasi o'tgan — talaba hozir bu korxonaga biriktirilmagan.
   * Kelmasa `false` deb hisoblanadi (`isPastPlace`).
   */
  isPast?: boolean;
}

/** O'tgan (yopilgan/tugagan) davrga tegishli joymi — `isPast` yo'q bo'lsa `false`. */
export const isPastPlace = (p: Pick<PracticePlaceDto, 'isPast'>) => p.isPast === true;

/** Ariza holati → o'zbekcha yorliq + Badge rangi. */
export const APPLICATION_STATUS: Record<ApplicationStatus, { label: string; kind: StatusKind }> = {
  draft: { label: 'Qoralama', kind: 'neu' },
  submitted: { label: 'Tekshiruvda', kind: 'info' },
  revisionNeeded: { label: 'Qayta topshirish', kind: 'late' },
  approved: { label: 'Tasdiqlangan', kind: 'ok' },
  rejected: { label: 'Rad etilgan', kind: 'bad' },
  completed: { label: 'Yakunlangan', kind: 'neu' },
  transferred: { label: "Ko'chirilgan", kind: 'neu' },
};

/**
 * GET /api/companies/lookup?tin=... (`CompanyLookupDto`) — talaba korxona ma'lumotini QO'LDA
 * kiritmaydi: faqat STIR yozadi, qolgani admin oldindan kiritgan yozuvdan keladi.
 * Faolsizlantirilgan/arxivlangan korxona qidiruvda umuman ko'rinmaydi → 404.
 */
export interface CompanyLookupDto {
  id: string;
  name: string;
  tin: string;
  activity: string;
  address: string;
  lat: number;
  lng: number;
  radiusM: number;
  supervisorName: string;
  supervisorPhone: string;
  mentorName: string | null;
  mentorPhone: string | null;
}

/** POST /api/student/place — JSON (`SubmitPracticePlaceCommand`). */
export interface SubmitPlaceCommand {
  tin: string;
}

/** Domain `Tin.DigitCount` — STIR aynan 9 ta raqam. */
export const TIN_DIGITS = 9;

/** Backend `SubmitPracticePlaceCommandValidator.TinFormatMessage` bilan bir xil matn. */
export const TIN_FORMAT_MESSAGE = "STIR 9 ta raqamdan iborat bo'lishi kerak. Namuna: 123456789";

/** Faqat raqamlarni qoldiradi va 9 ta bilan cheklaydi (input uchun). */
export const onlyTinDigits = (value: string) => value.replace(/\D/g, '').slice(0, TIN_DIGITS);

/** "304512889" → "304 512 889". */
export const formatTin = (tin: string) => tin.replace(/(\d{3})(?=\d)/g, '$1 ').trim();
