/**
 * Yuzni tasdiqlash (kontrakt v3.27 §6.33) — talabaning etalon (namuna) selfisi.
 * Oqim: talaba etalon yuboradi (rozilik bilan) → tyutor tasdiqlaydi/rad etadi → har check-in selfisi
 * server tomonida etalon bilan solishtiriladi (moslik `faceMatchThreshold` dan past → rad).
 */
export type FaceStatus = 'none' | 'pending' | 'approved' | 'rejected';

/** GET/POST /api/student/face. */
export interface StudentFaceDto {
  status: FaceStatus;
  /** Sozlama `faceVerificationEnabled` — yoqilgan bo'lsa check-in uchun tasdiqlangan etalon kerak. */
  required: boolean;
  /** `/api/files/{id}` — etalon rasmi (Bearer bilan ochiladi); yuborilmagan → null. */
  photoUrl: string | null;
  /** ISO 8601 */
  submittedAt: string | null;
  reviewedAt: string | null;
  /** Tyutor rad etgan bo'lsa — sabab. */
  rejectReason: string | null;
}

export const FACE_STATUS: Record<
  FaceStatus,
  { label: string; badge: 'ok' | 'late' | 'bad' | 'neu' }
> = {
  none: { label: 'Yuborilmagan', badge: 'neu' },
  pending: { label: 'Tyutor tekshirmoqda', badge: 'late' },
  approved: { label: 'Tasdiqlangan', badge: 'ok' },
  rejected: { label: 'Rad etilgan', badge: 'bad' },
};

/** Rozilik matni (etalon formasi). */
export const FACE_CONSENT_TEXT =
  'Roziman: rasmim faqat davomatda shaxsimni tasdiqlash uchun ishlatiladi va uni tyutorim ko‘radi.';

/** Etalon sahifasi marshruti. */
export const FACE_PATH = '/face';

/**
 * Darvoza: tekshiruv yoqilgan va ishlatsa bo'ladigan etalon yo'q (yuborilmagan yoki rad etilgan) —
 * talaba ilovadan foydalanishdan oldin `/face` sahifasida etalon yuboradi. `pending`/`approved` — oddiy ilova.
 */
export function needsFaceEnrollment(face: StudentFaceDto | undefined): boolean {
  return Boolean(face?.required && (face.status === 'none' || face.status === 'rejected'));
}
