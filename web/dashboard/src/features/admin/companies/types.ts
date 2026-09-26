import type { StatusKind } from '@/shared/ui';

/**
 * Kontrakt v3 `Company` (backend `CompanyRow`).
 * Belgi: null (—) · largeRadius (late) · tooManyStudents (bad) · suspicious (bad).
 * Ustuvorlik (backend): suspicious → tooManyStudents → largeRadius → null.
 */
export type CompanyFlag = 'largeRadius' | 'suspicious' | 'tooManyStudents';

export interface Company {
  id: string;
  name: string;
  /** STIR xom 9 raqam "305881204" (UI: "305 881 204"). */
  tin: string;
  activity: string;
  address: string;
  radiusM: number;
  /**
   * AKTIV amaliyotchilar: ochiq, hozir davom etayotgan davrda arizasi tasdiqlangan talabalar
   * (kutilayotgan / rad etilgan / ko'chirilgan / yopilgan davrdagilar hisoblanmaydi).
   */
  students: number;
  /** Shu korxonadagi talabalarning shubhali davomat kunlari. */
  suspiciousDays: number;
  /** STIR nazorati: `maxStudentsPerCompany` sozlamasi qiymati. */
  maxStudents: number;
  /** `students > maxStudents` — bitta STIR ostida haddan ko'p talaba. */
  overLimit: boolean;
  isActive: boolean;
  flag: CompanyFlag | null;
}

export const COMPANY_FLAG_LABEL: Record<CompanyFlag, { label: string; kind: 'late' | 'bad' }> = {
  largeRadius: { label: 'Katta radius', kind: 'late' },
  suspicious: { label: "Shubhali to'planish", kind: 'bad' },
  tooManyStudents: { label: "Talaba ko'p", kind: 'bad' },
};

/** Backend `ApplicationStatus` (JSON camelCase). */
export type ApplicationStatus =
  | 'draft'
  | 'submitted'
  | 'revisionNeeded'
  | 'approved'
  | 'rejected'
  | 'completed'
  /** Admin talabani boshqa korxonaga o'tkazgan — eski ariza yopiladi (tarix saqlanadi). */
  | 'transferred';

export const APPLICATION_STATUS_LABEL: Record<
  ApplicationStatus,
  { label: string; kind: StatusKind }
> = {
  draft: { label: 'Qoralama', kind: 'neu' },
  submitted: { label: 'Yangi', kind: 'info' },
  revisionNeeded: { label: 'Tuzatishda', kind: 'late' },
  approved: { label: 'Tasdiqlangan', kind: 'ok' },
  rejected: { label: 'Rad etilgan', kind: 'bad' },
  completed: { label: 'Yakunlangan', kind: 'neu' },
  transferred: { label: "Ko'chirilgan", kind: 'neu' },
};

/** Backend `StudentState`: davomat < 70% → redFlag; shubhali kun bor → suspicious; aks holda active. */
export type CompanyStudentState = 'active' | 'redFlag' | 'suspicious';

export const COMPANY_STUDENT_STATE_LABEL: Record<
  CompanyStudentState,
  { label: string; kind: StatusKind }
> = {
  active: { label: 'Faol', kind: 'ok' },
  redFlag: { label: 'Qizil bayroq', kind: 'bad' },
  suspicious: { label: 'Shubhali', kind: 'late' },
};

/** Aktiv amaliyot davri: hozir davom etayotgan ochiq davr va undagi aktiv talabalar soni. */
export interface CompanyPeriod {
  id: string;
  name: string;
  /** DateOnly "2026-09-01". */
  startDate: string;
  endDate: string;
  students: number;
}

/** GET /api/admin/companies/{id} → CompanyDetail · 404 */
export interface CompanyDetail {
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
  isActive: boolean;
  /** Aktiv amaliyotchilar soni (`Company.students` bilan bir xil ma'no). */
  students: number;
  suspiciousDays: number;
  maxStudents: number;
  overLimit: boolean;
  flag: CompanyFlag | null;
  /** Faqat hozir davom etayotgan ochiq davr(lar); aktiv davr bo'lmasa — bo'sh massiv. */
  periods: CompanyPeriod[];
}

/**
 * POST / PUT body (backend `CreateCompanyCommand` / `UpdateCompanyCommand`).
 * `radiusM: null` — server `geofenceRadius` sozlamasidan oladi; mentor maydonlari ixtiyoriy.
 */
export interface CompanyInput {
  name: string;
  tin: string;
  activity: string;
  address: string;
  lat: number;
  lng: number;
  radiusM: number | null;
  supervisorName: string;
  supervisorPhone: string;
  mentorName: string | null;
  mentorPhone: string | null;
}

/**
 * GET /api/admin/companies/{id}/students → CompanyStudent[] (FISH bo'yicha tartib).
 * Faqat hozir shu korxonada aktiv amaliyot o'tayotgan talabalar — `applicationStatus` doim `approved`.
 */
export interface CompanyStudent {
  studentId: string;
  name: string;
  hemisId: string;
  group: string;
  course: number;
  faculty: string;
  tutorName: string | null;
  applicationStatus: ApplicationStatus;
  periodName: string | null;
  /** 0–100 (kasrli bo'lishi mumkin). */
  attendancePct: number;
  attendedDays: number;
  totalDays: number;
  diaryCount: number;
  state: CompanyStudentState;
  suspiciousCount: number;
}

/** Jadval/kartochkada "21/10" ko'rinishidagi talaba-chegara nisbati. */
export function studentsOfLimit(students: number, maxStudents: number): string {
  return `${students}/${maxStudents}`;
}
