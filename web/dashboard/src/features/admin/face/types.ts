import type { StatusKind } from '@/shared/ui';

/**
 * Yuzni tasdiqlash (kontrakt v3.27, §6.33). Talaba etalon selfi yuboradi (`pending`), admin tasdiqlaydi
 * (`approved`) yoki sabab bilan rad etadi (`rejected`); `none` — hali yuborilmagan (yoki bekor qilingan).
 */
export type FaceStatus = 'none' | 'pending' | 'approved' | 'rejected';

/** `StudentFaceDto` — talaba profili (`detail.face`) va approve/reject/reset javobi. */
export interface StudentFace {
  status: FaceStatus;
  /**
   * Sozlama `faceVerificationEnabled` — check-in selfisi etalon bilan solishtiriladimi. Etalon
   * TOPSHIRISH majburiyligi bunga bog'liq emas (`enrollmentRequired`).
   */
  required: boolean;
  /** v3.34: etalon topshirish har doim majburiy (talaba ilovaga birinchi kirishda topshiradi). */
  enrollmentRequired?: boolean;
  /**
   * v3.34: talaba hozir etalon yubora oladimi (`none`/`rejected`). `pending`/`approved` — qulflangan,
   * qayta topshirish faqat admin rad etsa yoki "qayta olish" (reset) qilsa.
   */
  canSubmit?: boolean;
  /** `/api/files/{id}` — etalon rasm; yo'q bo'lsa null. */
  photoUrl: string | null;
  /** ISO 8601 */
  submittedAt: string | null;
  reviewedAt: string | null;
  rejectReason: string | null;
}

/** Ro'yxat tab'lari (`?status=`); `none` ro'yxatda bo'lmaydi. */
export type FaceEnrollmentTab = Exclude<FaceStatus, 'none'>;

/** `GET /api/admin/face-enrollments?status=` → `{ items }` elementi. */
export interface FaceEnrollment {
  studentId: string;
  fullName: string;
  hemisId: string;
  group: string;
  photoUrl: string | null;
  status: FaceStatus;
  submittedAt: string | null;
  reviewedAt: string | null;
  rejectReason: string | null;
}

export interface FaceEnrollmentListResponse {
  items: FaceEnrollment[];
}

export const FACE_TABS: readonly { value: FaceEnrollmentTab; label: string }[] = [
  { value: 'pending', label: 'Kutilmoqda' },
  { value: 'approved', label: 'Tasdiqlangan' },
  { value: 'rejected', label: 'Rad etilgan' },
];

export function isFaceTab(value: string | null): value is FaceEnrollmentTab {
  return FACE_TABS.some((t) => t.value === value);
}

export const FACE_STATUS_LABEL: Record<FaceStatus, { label: string; kind: StatusKind }> = {
  none: { label: 'Yuborilmagan', kind: 'neu' },
  pending: { label: 'Tekshirilmoqda', kind: 'late' },
  approved: { label: 'Tasdiqlangan', kind: 'ok' },
  rejected: { label: 'Rad etilgan', kind: 'bad' },
};

/** Rad etish sababi majburiy (backend 400 `errors.Reason`). */
export const FACE_REJECT_REASON_REQUIRED = 'Rad etish sababini yozing.';
