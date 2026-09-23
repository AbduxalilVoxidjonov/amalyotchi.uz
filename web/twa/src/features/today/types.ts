import type { GeoPoint } from '@/shared/lib/geolocation';
import type { FactItem } from '@/shared/ui';

/**
 * Kontrakt v2 — `StudentContracts.cs` (TodayDto). Enum'lar camelCase string, soatlar "HH:mm" (Toshkent),
 * `checkInAt/checkOutAt` — ISO 8601 (DateTimeOffset). Formatlash — frontend'da (`ATTENDANCE_STATUS`, formatTime).
 */

/** Domain `AttendanceStatus`: pending/absent/dayOff o'qishda hisoblanadi, present/late/excused — bazada. */
export type AttendanceStatus = 'pending' | 'present' | 'late' | 'absent' | 'excused' | 'dayOff';

export interface TodayWindowDto {
  /** Check-in ochiladigan vaqt (09:00). */
  start: string;
  /** Shu vaqtdan boshlab "kech keldi" (09:15). */
  end: string;
  /** Shu vaqtdan check-in qabul qilinmaydi (10:30). */
  closesAt: string;
  /** Check-out ochiladigan vaqt (17:00). */
  checkoutAt: string;
  /** Hozirgi bosqich uchun amal (check-in yoki check-out) server vaqti bo'yicha mumkinmi. */
  isOpen: boolean;
}

export interface TodayCheckInDto {
  status: AttendanceStatus;
  checkInAt: string | null;
  checkOutAt: string | null;
  /** Oxirgi urinishdagi masofa (m). */
  distanceM: number | null;
  /** Korxona radiusi; ariza yo'q bo'lsa null. */
  radiusM: number | null;
  gpsAccuracyM: number | null;
  /** Shubhali belgilanish (tez ko'chish, bir xil koordinata …) — tyutor tekshiradi. */
  suspicious: boolean;
  /** Check-out qilinmagan — server 18:00 da avtomatik yopgan. */
  autoClosed: boolean;
  /** Amal hozir mumkin bo'lmasa — o'zbekcha sabab (ariza yo'q, ish kuni emas, oyna yopiq …). */
  note: string | null;
}

export interface TodayPlaceDto {
  company: string;
  address: string;
  radiusM: number;
  /** 0–100 */
  attendancePct: number;
  daysPresent: number;
  daysTotal: number;
  reports: number;
  /** 1–5, masalan 4.2 */
  avgScore: number;
}

export interface TodayDiaryDto {
  submittedToday: boolean;
  minChars: number;
  maxFiles: number;
  /** Sozlama `diaryPdfRequired`: true → hisobotga kamida bitta PDF shart (aks holda POST 400 `errors.Files`). */
  pdfRequired: boolean;
}

/** GET /api/student/today. Ariza yo'q → `place: null`, `checkin.status = pending`, `checkin.note` da sabab. */
export interface TodayDto {
  /** DateOnly "2026-10-12" */
  date: string;
  window: TodayWindowDto;
  checkin: TodayCheckInDto;
  place: TodayPlaceDto | null;
  diary: TodayDiaryDto;
}

/**
 * POST /api/student/checkin | /checkout — **multipart/form-data** (kontrakt §1.3):
 * `lat`, `lng`, `accuracy`, `occurredAt` + ixtiyoriy `photo` (selfie).
 * `photo` majburiyligi server sozlamasiga bog'liq (`checkinPhotoRequired`) — rasmsiz yuborilsa
 * 400 + `errors.Photo` qaytishi mumkin.
 */
export interface CheckinRequest extends GeoPoint {
  photo: File | null;
}

/** Holat → o'zbekcha yorliq va rang (SPEC-TOKENS 1.5). */
export const ATTENDANCE_STATUS: Record<
  AttendanceStatus,
  { label: string; tone: NonNullable<FactItem['tone']> }
> = {
  pending: { label: 'Kutilmoqda', tone: 'default' },
  present: { label: 'Keldi', tone: 'ok' },
  late: { label: 'Kech keldi', tone: 'late' },
  absent: { label: 'Kelmadi', tone: 'bad' },
  excused: { label: 'Sababli', tone: 'default' },
  dayOff: { label: 'Dam olish', tone: 'default' },
};

/** Talaba korxonada (check-in bor, check-out hali yo'q, avtomatik yopilmagan) — KETDIM bosqichi. */
export function isCheckedIn(c: TodayCheckInDto): boolean {
  return c.checkInAt !== null && c.checkOutAt === null && !c.autoClosed;
}

/** Kun yakunlangan: check-out qilingan yoki server avtomatik yopgan. */
export function isFinished(c: TodayCheckInDto): boolean {
  return c.checkInAt !== null && (c.checkOutAt !== null || c.autoClosed);
}
