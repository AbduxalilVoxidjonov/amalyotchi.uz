import type { AuditEntry } from '../audit/types';
import type { TutorStatus } from '../tutors/types';

/**
 * Kontrakt v2 `GET /api/admin/dashboard` (backend `AdminDashboardDto`). Faqat xom raqamlar —
 * stat kartalar matni, foizlar, "N ariza" kabi yozuvlar `DashboardView` da yasaladi.
 */
export interface DashboardStats {
  /** Talabalar ro'yxati (va sidebar nav) bilan teng. */
  studentsTotal: number;
  /** Telegram hisobi bog'langan talabalar. */
  studentsLinked: number;
  studentsUnlinked: number;
  /** Fakultetlar ro'yxati (va sidebar nav) bilan teng. */
  faculties: number;
  groups: number;
  /** Katalogda faol (`isActive`) korxonalar — talabaga bog'liq emas. */
  companiesActive: number;
  /** "Aktiv korxona" qoidasi bo'yicha bugun kamida bitta amaliyotchisi bor korxonalar. */
  companiesWithInterns: number;
  /** Ochiq (yopilmagan, tugamagan) davrlardagi `submitted` arizalar. */
  applicationsPending: number;
  /** Shulardan 48 soatdan ortiq javobsiz kutayotganlar. */
  applicationsOverdue: number;
  /** Shartnomalar — ochiq davrlar bo'yicha; `transferred` hech qayerda sanalmaydi. */
  contractsApproved: number;
  contractsRevision: number;
  contractsRejected: number;
  contractsMissing: number;
  /** Bugun davom etayotgan (yopilmagan, sanalar ichida) davrlar soni; 0 — bugun amaliyot yo'q. */
  ongoingPeriods: number;
  /** Bugun davomat kutilgan talabalar (davom etayotgan davr, ish kuni). Yopilgan davr talabalari kirmaydi. */
  expectedToday: number;
  presentToday: number;
  lateToday: number;
  absentToday: number;
  excusedToday: number;
  noDiaryToday: number;
  /** keldi (present+late) / (kutilgan − sababli), int 0..100. */
  attendanceTodayPct: number;
  /** Kecha davomat kutilgan talabalar (0 — kecha ish kuni bo'lmagan yoki davr yo'q). */
  expectedYesterday: number;
  attendanceYesterdayPct: number;
}

export interface FacultyAttendance {
  id: string;
  name: string;
  code: string;
  studentCount: number;
  /** 0 — bugun fakultetda davom etayotgan amaliyot ish kuni yo'q (foiz ko'rsatilmaydi). */
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
