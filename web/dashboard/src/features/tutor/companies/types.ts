import type { StatusKind } from '@/shared/ui';

/**
 * Korxona bayrog'i (backend `CompanyFlag`). Ustuvorlik: suspicious → tooManyStudents →
 * largeRadius → null.
 */
export type CompanyFlag = 'largeRadius' | 'suspicious' | 'tooManyStudents';

export const COMPANY_FLAG_LABEL: Record<CompanyFlag, { label: string; kind: StatusKind }> = {
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

/** GET /api/tutor/companies → TutorCompany[] (faqat ko'lamdagi talabalar biriktirilgan korxonalar). */
export interface TutorCompany {
  id: string;
  name: string;
  /** STIR xom 9 raqam. */
  tin: string;
  address: string;
  lat: number;
  lng: number;
  radiusM: number;
  /** FAQAT tyutor ko'lamidagi talabalar. */
  students: number;
  /** Butun tizim bo'yicha shu korxonada (STIR nazorati uchun). */
  totalStudents: number;
  maxStudents: number;
  /** `totalStudents > maxStudents`. */
  overLimit: boolean;
  /** Ko'lamdagi talabalar o'rtachasi, 0–100 (1 kasr). */
  attendancePct: number;
  suspiciousDays: number;
  flag: CompanyFlag | null;
}

/** Amaliyot davri kesimi: shu korxonada qaysi davrda nechta talaba. */
export interface CompanyPeriod {
  id: string;
  name: string;
  /** DateOnly "2026-09-01". */
  startDate: string;
  endDate: string;
  students: number;
}

/** GET /api/tutor/companies/{id} → CompanyDetail (`students` — ko'lamda) · 404 */
export interface TutorCompanyDetail {
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
  /** Tyutor ko'lamidagi talabalar. */
  students: number;
  /** Butun tizim bo'yicha (STIR nazorati). */
  totalStudents: number;
  suspiciousDays: number;
  maxStudents: number;
  overLimit: boolean;
  flag: CompanyFlag | null;
  periods: CompanyPeriod[];
}

/** GET /api/tutor/companies/{id}/students → CompanyStudent[] (faqat ko'lamdagilar, FISH tartibida). */
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
  attendancePct: number;
  attendedDays: number;
  totalDays: number;
  diaryCount: number;
  state: CompanyStudentState;
  suspiciousCount: number;
}

/** "21/10" — talaba / STIR chegarasi. */
export function studentsOfLimit(students: number, maxStudents: number): string {
  return `${students}/${maxStudents}`;
}
