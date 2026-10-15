/**
 * Yuzni tasdiqlash (kontrakt v3.27 §6.33) — talabaning etalon (namuna) selfisi.
 * Oqim: talaba etalon yuboradi (rozilik bilan) → admin tasdiqlaydi/rad etadi → har check-in selfisi
 * server tomonida etalon bilan solishtiriladi (moslik `faceMatchThreshold` dan past → rad).
 */
export type FaceStatus = 'none' | 'pending' | 'approved' | 'rejected';

/** GET/POST /api/student/face. */
export interface StudentFaceDto {
  status: FaceStatus;
  /** Sozlama `faceVerificationEnabled` — yoqilgan bo'lsa check-in uchun tasdiqlangan etalon kerak. */
  required: boolean;
  /** v3.34: etalon topshirish sozlamadan mustaqil majburiy (mobil ilova darvozasi). TWA darvozasi `required` da qoladi. */
  enrollmentRequired?: boolean;
  /**
   * v3.34: talaba hozir etalon yubora oladimi — faqat `none`/`rejected`. `pending`/`approved` da
   * POST → 409 (`code: faceAlreadyEnrolled`); qayta topshirish faqat admin rad etsa yoki reset qilsa.
   */
  canSubmit?: boolean;
  /** `/api/files/{id}` — etalon rasmi (Bearer bilan ochiladi); yuborilmagan → null. */
  photoUrl: string | null;
  /** ISO 8601 */
  submittedAt: string | null;
  reviewedAt: string | null;
  /** Admin rad etgan bo'lsa — sabab. */
  rejectReason: string | null;
}

export const FACE_STATUS: Record<
  FaceStatus,
  { label: string; badge: 'ok' | 'late' | 'bad' | 'neu' }
> = {
  none: { label: 'Yuborilmagan', badge: 'neu' },
  pending: { label: 'Admin tekshirmoqda', badge: 'late' },
  approved: { label: 'Tasdiqlangan', badge: 'ok' },
  rejected: { label: 'Rad etilgan', badge: 'bad' },
};

/** Rozilik matni (etalon formasi). */
export const FACE_CONSENT_TEXT =
  'Roziman: rasmim faqat davomatda shaxsimni tasdiqlash uchun ishlatiladi va uni administrator ko‘radi.';

/** Etalon sahifasi marshruti. */
export const FACE_PATH = '/face';

/**
 * Darvoza: tekshiruv yoqilgan va ishlatsa bo'ladigan etalon yo'q (yuborilmagan yoki rad etilgan) —
 * talaba ilovadan foydalanishdan oldin `/face` sahifasida etalon yuboradi. `pending`/`approved` — oddiy ilova.
 */
/** Talaba hozir etalon yubora oladimi (eski server `canSubmit` yubormasa — holatdan). */
export function canSubmitFace(face: StudentFaceDto): boolean {
  return face.canSubmit ?? (face.status === 'none' || face.status === 'rejected');
}

export function needsFaceEnrollment(face: StudentFaceDto | undefined): boolean {
  return Boolean(face?.required && (face.status === 'none' || face.status === 'rejected'));
}
