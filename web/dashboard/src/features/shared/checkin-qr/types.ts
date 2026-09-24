/**
 * Korxona check-in QR kodi (kontrakt: `CompanyCheckInQr`, JSON camelCase).
 * Talaba kelish/ketishda korxonaga osilgan shu QR ni skanerlaydi; `payload` — QR ichidagi matn
 * (`"AMLQR:1:<32 hex>"`), rasm client tomonda chiziladi.
 */
export interface CompanyCheckInQr {
  companyId: string;
  companyName: string;
  payload: string;
  /** ISO 8601 — oxirgi yangilanish (rotatsiya) vaqti. */
  rotatedAt: string;
}

/** Qaysi rol endpoint'i: `/api/admin/companies/...` yoki `/api/tutor/companies/...`. */
export type CheckInQrArea = 'admin' | 'tutor';
