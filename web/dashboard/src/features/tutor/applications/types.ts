import type { StatusKind } from '@/shared/ui';
import { daysSince, fmtDate, fmtRelative } from '../format';

/**
 * Backend `ApplicationStatus` (tyutor ko'radigan qismi; JSON camelCase). `transferred` — admin talabani
 * boshqa korxonaga o'tkazgan (yakuniy holat, faqat ko'rish); u alohida tab emas.
 */
export type ApplicationStatus =
  'submitted' | 'revisionNeeded' | 'approved' | 'rejected' | 'transferred';
/** Ro'yxat tab'lari (`?tab=`) — `transferred` dan tashqari holatlar. */
export type ApplicationTab = Exclude<ApplicationStatus, 'transferred'>;

export interface ApplicationSummary {
  id: string;
  studentId: string;
  name: string;
  group: string;
  course: number;
  hemisId: string;
  /** Korxona nomi. */
  company: string;
  status: ApplicationStatus;
  /** ISO 8601 */
  submittedAt: string;
  decidedAt: string | null;
}

export interface GeoCoords {
  lat: number;
  lng: number;
}

export interface ApplicationCompany {
  name: string;
  tin: string;
  activity: string;
  address: string;
  supervisorName: string;
  supervisorPhone: string;
  mentorName: string | null;
  mentorPhone: string | null;
}

export interface ApplicationContract {
  name: string;
  pages: number | null;
  sizeBytes: number;
  /** `/api/files/{id}` */
  url: string;
}

/** GET /api/tutor/applications/:id */
export interface ApplicationDetail extends ApplicationSummary {
  coords: GeoCoords;
  /** Joriy taklif (tasdiqlangach — tyutor qiymati). */
  radiusM: number;
  companyDetails: ApplicationCompany;
  contract: ApplicationContract | null;
  /** Tyutor izohi (qaytarish/rad etish sababi yoki tasdiqlash izohi). */
  comment: string | null;
  /** Belgilangan tekshiruv punktlari (0..6). */
  checklist: number[];
  revisionCount: number;
}

export type ApplicationCounts = Record<ApplicationTab, number>;

/** GET /api/tutor/applications?status= */
export interface ApplicationListResponse {
  counts: ApplicationCounts;
  items: ApplicationSummary[];
}

export type ApplicationDecision = 'approve' | 'return' | 'reject';

/**
 * POST /api/tutor/applications/:id/decision
 * approve → `radiusM` majburiy (50–1000, 50 qadam), `checklist` ixtiyoriy;
 * return/reject → `comment` majburiy. Xato → 400 `errors`; hal qilingan → 409.
 */
export interface ApplicationDecisionRequest {
  decision: ApplicationDecision;
  radiusM?: number;
  checklist?: number[];
  comment?: string;
}

export interface ApplicationDecisionResponse {
  id: string;
  status: ApplicationStatus;
}

export const APPLICATION_TABS: { value: ApplicationTab; label: string }[] = [
  { value: 'submitted', label: 'Yangi' },
  { value: 'revisionNeeded', label: 'Tuzatishda' },
  { value: 'approved', label: 'Tasdiqlangan' },
  { value: 'rejected', label: 'Rad etilgan' },
];

export const APPLICATION_STATUS_LABEL: Record<
  ApplicationStatus,
  { label: string; kind: StatusKind }
> = {
  submitted: { label: 'Yangi', kind: 'info' },
  revisionNeeded: { label: 'Tuzatishda', kind: 'late' },
  approved: { label: 'Tasdiqlangan', kind: 'ok' },
  rejected: { label: 'Rad etilgan', kind: 'bad' },
  transferred: { label: "Ko'chirilgan", kind: 'neu' },
};

export const DECISION_TO_STATUS: Record<ApplicationDecision, ApplicationStatus> = {
  approve: 'approved',
  return: 'revisionNeeded',
  reject: 'rejected',
};

/** SPEC-SCREENS §4 — tekshirish ro'yxati (7 ta, indekslar 0–6 backend'ga yuboriladi). */
export const CHECKLIST_ITEMS: readonly string[] = [
  'Shartnomada FISH profildagi bilan mos',
  'Korxona nomi formada va shartnomada bir xil',
  'STIR 9 xonali va haqiqiy korxonaga tegishli',
  'Shartnoma imzolangan va muhrlangan',
  'Sanalar amaliyot davriga mos',
  "Xaritadagi nuqta haqiqiy manzilga to'g'ri keladi",
  "Korxona faoliyati yo'nalishga mos",
];

export const RADIUS_STEP_M = 50;
export const RADIUS_MIN_M = 50;
export const RADIUS_MAX_M = 1000;

export function isApplicationTab(v: string | null): v is ApplicationTab {
  return APPLICATION_TABS.some((t) => t.value === v);
}

/** Karta pastidagi vaqt yorlig'i: "2 soat oldin" · "3 kun kutilmoqda" · "08.10 da tasdiqlandi". */
export function applicationWaited(app: ApplicationSummary, now: number = Date.now()): string {
  switch (app.status) {
    case 'submitted':
      return fmtRelative(app.submittedAt, now);
    case 'revisionNeeded': {
      const days = daysSince(app.decidedAt ?? app.submittedAt, now);
      return days === 0 ? 'bugun qaytarildi' : `${days} kun kutilmoqda`;
    }
    case 'approved':
      return app.decidedAt ? `${fmtDate(app.decidedAt).slice(0, 5)} da tasdiqlandi` : 'tasdiqlandi';
    case 'rejected':
      return app.decidedAt ? `${fmtDate(app.decidedAt).slice(0, 5)} da rad etildi` : 'rad etildi';
    case 'transferred':
      return app.decidedAt ? `${fmtDate(app.decidedAt).slice(0, 5)} da ko'chirildi` : "ko'chirildi";
  }
}
