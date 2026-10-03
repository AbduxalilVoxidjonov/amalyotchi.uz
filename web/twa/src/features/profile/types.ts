import type { StudentPeriodStatus } from '@/features/period/types';

/**
 * GET /api/student/profile — talaba profili (shaxsiy + o'quv ma'lumotlari, amaliyot xulosasi, hisob).
 * `practices` — talabaning BARCHA amaliyot davrlari (davom etayotgan ochiq davr birinchi, keyin `startDate`
 * kamayish tartibida); davr yo'q → `[]`. `practice` — sukut davr (eski maydon, orqaga moslik uchun qoladi;
 * `null` — davr biriktirilmagan).
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
  /** Yangi maydon; eski server yubormasligi mumkin → `profilePractices()` `practice` ga qaytadi. */
  practices?: StudentProfilePracticeDto[];
  /** Talabaning ish vaqti (kelish/ketish). Eski server yubormasligi mumkin → bo'lim yashiriladi. */
  workHours?: StudentWorkHoursDto;
}

/**
 * Talabaning o'zi belgilagan ish vaqti ("HH:mm"). `start`/`end` — oxirgi saqlangan qiymat (`null` — davr
 * vaqti ishlatiladi), `effectiveFrom` dan boshlab amal qiladi (ertangi sana bo'lsa — kutilayotgan o'zgarish).
 * `todayStart`/`todayEnd` — bugun amalda bo'lgan vaqt (o'zi belgilagan yoki davrniki).
 */
export interface StudentWorkHoursDto {
  start: string | null;
  end: string | null;
  /** DateOnly | null (hech qachon belgilanmagan). */
  effectiveFrom: string | null;
  todayStart: string;
  todayEnd: string;
  /** Davr ish vaqti; davr biriktirilmagan → null. */
  periodStart: string | null;
  periodEnd: string | null;
}

/** PUT /api/student/profile/work-hours — ikkalasi null = davr vaqtiga qaytarish. */
export interface UpdateWorkHoursRequest {
  start: string | null;
  end: string | null;
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
