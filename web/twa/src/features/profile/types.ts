import type { StudentPeriodStatus } from '@/features/period/types';

/**
 * GET /api/student/profile — talaba profili (shaxsiy + o'quv ma'lumotlari, amaliyot xulosasi, hisob).
 * `practice === null` — talabaga amaliyot davri biriktirilmagan.
 */
export interface StudentProfileDto {
  id: string;
  fullName: string;
  hemisId: string;
  phoneNumber: string | null;
  faculty: string;
  department: string;
  direction: string;
  group: string;
  course: number;
  tutor: { fullName: string; phoneNumber: string | null } | null;
  telegramLinked: boolean;
  hasPassword: boolean;
  mustChangePassword: boolean;
  practice: StudentProfilePracticeDto | null;
}

export interface StudentProfilePracticeDto {
  period: {
    id: string;
    name: string;
    status: StudentPeriodStatus;
    /** DateOnly */
    startDate: string;
    endDate: string;
  };
  company: { id: string; name: string; address: string | null } | null;
  /** Davr boshidan bugungacha o'tgan ish kunlari. */
  elapsedWorkDays: number;
  /** 0–100 */
  attendancePct: number;
  suspiciousDays: number;
  /** Joriy (yoki yakuniy) ball. */
  total: number;
  grade: number | null;
  finalized: boolean;
}

export const PERIOD_STATUS_LABEL: Record<StudentPeriodStatus, string> = {
  planned: 'Rejada',
  active: 'Faol',
  closed: 'Yakunlangan',
};
