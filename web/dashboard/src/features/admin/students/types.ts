import type { TutorStudentDetail } from '@/features/tutor/students/types';
import type { ListParams } from '../shared/types';

export type { ActiveCompanyRef } from '@/features/tutor/students/types';

/** Kontrakt v2 `Student` (backend `StudentRow`). Holat: Faol (ok) · Qizil bayroq (bad) · Ulanmagan (neu). */
export type StudentStatus = 'active' | 'flagged' | 'unlinked';

export interface Student {
  /** Talabaning `User.Id`. */
  id: string;
  fullName: string;
  hemisId: string;
  groupId: string;
  /** "412-22" */
  group: string;
  course: number;
  faculty: string;
  /**
   * Faqat HOZIR aktiv amaliyot o'tayotgan korxona (profildagi `activeCompany.name` bilan bir xil);
   * yopilgan/tugagan davrdagi eski korxona kirmaydi — yo'q bo'lsa null ("—").
   */
  company: string | null;
  attendancePct: number;
  suspiciousDays: number;
  telegramLinked: boolean;
  status: StudentStatus;
}

export const STUDENT_STATUS_LABEL: Record<
  StudentStatus,
  { label: string; kind: 'ok' | 'bad' | 'neu' }
> = {
  active: { label: 'Faol', kind: 'ok' },
  flagged: { label: 'Qizil bayroq', kind: 'bad' },
  unlinked: { label: 'Ulanmagan', kind: 'neu' },
};

/* ────────────────────────────────────────────────────────────────────────────
 * Talaba profili (`/admin/students/:studentId`) — `GET /api/admin/students/{id}`.
 * Bloklar tyutor profilidagi bilan bir xil (ayni backend handler), shuning uchun
 * turlar `features/tutor/students/types` dan olinadi; bu yerda faqat admin qo'shimchalari.
 * ──────────────────────────────────────────────────────────────────────────── */

/** Talaba guruhiga biriktirilgan tyutor (bir nechtasi bo'lsa — FISH bo'yicha birinchisi). */
export interface AdminStudentTutor {
  id: string;
  fullName: string;
  phone: string | null;
}

/** `GET /api/admin/students/{id}` → tyutor profili maydonlari + adminga xoslari. */
export interface AdminStudentDetail extends TutorStudentDetail {
  groupId: string;
  /** Kafedra nomi (tyutor profilida yo'q). */
  department: string;
  /** Ro'yxatdagi holat bilan bir xil qoida (faol · qizil bayroq · ulanmagan). */
  adminStatus: StudentStatus;
  telegramLinked: boolean;
  tutor: AdminStudentTutor | null;
}

/* ────────────────────────────────────────────────────────────────────────────
 * Ommaviy biriktirish — `POST /api/admin/students/assign-company`.
 * Admin jadvalda bir nechta talabani belgilaydi va bitta korxonaga biriktiradi.
 * ──────────────────────────────────────────────────────────────────────────── */

/** So'rov tanasi (backend bir marta 200 tagacha talabani qabul qiladi). */
export interface AssignCompanyInput {
  studentIds: string[];
  companyId: string;
}

/** Biriktirilmagan talaba va sababi (masalan "Allaqachon shu korxonaga biriktirilgan."). */
export interface AssignCompanyError {
  studentId: string;
  studentName: string;
  message: string;
}

/** Hisobot: `assigned + skipped = total`; xato qatorlar tashlanadi, qolganlari biriktiriladi. */
export interface AssignCompanyResult {
  total: number;
  assigned: number;
  skipped: number;
  /** Tanlangan korxona nomi (hisobot sarlavhasida ko'rsatiladi). */
  companyName: string;
  errors: AssignCompanyError[];
}

/* ────────────────────────────────────────────────────────────────────────────
 * Talaba profilidan biriktirish / o'tkazish — `POST /api/admin/students/{id}/company`.
 * Ochiq ariza bo'lsa u `transferred` holatiga o'tadi va yangi korxonaga `approved` ariza
 * yaratiladi; ariza bo'lmasa — birinchi biriktirish. Javob: yangilangan `AdminStudentDetail`
 * (yangi `activeCompany` bilan).
 * ──────────────────────────────────────────────────────────────────────────── */

/** Izoh uzunligi chegarasi (backend validatori bilan bir xil). */
export const STUDENT_COMPANY_COMMENT_MAX = 500;

export interface SetStudentCompanyInput {
  companyId: string;
  /** Ixtiyoriy izoh (≤ 500) — bo'sh bo'lsa yuborilmaydi. */
  comment?: string;
}

/* ────────────────────────────────────────────────────────────────────────────
 * Ro'yxat filtrlari — `GET /api/admin/students/filters` (variantlar admin "Yo'nalishlar"
 * bo'limidagi ma'lumotlardan dinamik) va `GET /api/admin/students?facultyId=&directionId=&course=`.
 * ──────────────────────────────────────────────────────────────────────────── */

export interface StudentFilterFaculty {
  id: string;
  name: string;
}

export interface StudentFilterDirection {
  id: string;
  name: string;
  facultyId: string;
}

export interface StudentFilters {
  faculties: StudentFilterFaculty[];
  directions: StudentFilterDirection[];
  /** O'sish tartibida (1, 2, 3, ...). */
  courses: number[];
}

/** Ro'yxat so'rovi: umumiy `q/page/pageSize` + ixtiyoriy filtrlar (AND). */
export interface StudentListParams extends ListParams {
  facultyId?: string;
  directionId?: string;
  course?: number;
}

/* ────────────────────────────────────────────────────────────────────────────
 * Bitta talabani qo'lda yaratish — `POST /api/admin/students` → 201 `Student` (ro'yxat qatori).
 * Parol yaratilmaydi: talaba Telegram orqali kiradi (brauzer uchun parol — profildagi
 * "Parol o'rnatish"). Xatolar: 400 (`errors`: fullName/hemisId/groupId/phoneNumber) · 409
 * (HEMIS ID yoki telefon band).
 * ──────────────────────────────────────────────────────────────────────────── */

export interface StudentCreateInput {
  fullName: string;
  /** Faqat raqamlar, 5–20 belgi. */
  hemisId: string;
  groupId: string;
  /** E.164 "+998901234567" yoki null (ixtiyoriy). */
  phoneNumber?: string | null;
}

/** `GET /api/admin/students/group-options` — faqat faol guruhlar (yaratish formasidagi tanlov). */
export interface StudentGroupOption {
  id: string;
  /** "412-22" */
  name: string;
  course: number | null;
  directionName: string;
  facultyName: string;
}

/** Guruh variantlarini toraytirish (hammasi ixtiyoriy, AND). */
export interface StudentGroupOptionParams {
  facultyId?: string;
  directionId?: string;
  course?: number;
}
