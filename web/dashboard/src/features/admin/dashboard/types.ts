import type { AuditEntry } from '../audit/types';
import type { TutorStatus } from '../tutors/types';

/**
 * Kontrakt v2 `GET /api/admin/dashboard` (backend `AdminDashboardDto`). Faqat xom raqamlar —
 * stat kartalar matni, foizlar, "N ariza" kabi yozuvlar `DashboardView` da yasaladi.
 */
export interface DashboardStats {
  studentsTotal: number;
  studentsLinked: number;
  studentsUnlinked: number;
  faculties: number;
  groups: number;
  companiesActive: number;
  applicationsPending: number;
  /** 3 kundan ortiq kutayotgan arizalar. */
  applicationsOverdue: number;
  contractsApproved: number;
  contractsRevision: number;
  contractsRejected: number;
  contractsMissing: number;
  expectedToday: number;
  presentToday: number;
  lateToday: number;
  absentToday: number;
  excusedToday: number;
  noDiaryToday: number;
  attendanceTodayPct: number;
  attendanceYesterdayPct: number;
}

export interface FacultyAttendance {
  id: string;
  name: string;
  code: string;
  studentCount: number;
  expectedToday: number;
  attendedToday: number;
  attendancePct: number;
}

export interface TutorActivity {
  id: string;
  name: string;
  facultyCode: string | null;
  /** Guruh kodlari: ["412-22", "413-22"]. */
  groups: string[];
  studentCount: number;
  pendingCount: number;
  /** ISO yoki null. */
  oldestPendingAt: string | null;
  /** Qaror tezligi (soat), qaror bo'lmasa null. */
  avgDecisionHours: number | null;
  lastActiveAt: string | null;
  status: TutorStatus;
}

export interface AdminDashboard {
  /** Toshkent kuni: "2026-09-14". */
  date: string;
  stats: DashboardStats;
  faculties: FacultyAttendance[];
  tutors: TutorActivity[];
  /** Oxirgi audit yozuvlari (yangisi birinchi). */
  audit: AuditEntry[];
}
