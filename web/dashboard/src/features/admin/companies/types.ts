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
  /** Arizasi tasdiqlangan talabalar. */
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
  'draft' | 'submitted' | 'revisionNeeded' | 'approved' | 'rejected' | 'completed';

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

/** Amaliyot davri kesimi: shu korxonada qaysi davrda nechta talaba. */
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
  students: number;
  suspiciousDays: number;
  maxStudents: number;
  overLimit: boolean;
  flag: CompanyFlag | null;
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

/** GET /api/admin/companies/{id}/students → CompanyStudent[] (FISH bo'yicha tartib). */
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
