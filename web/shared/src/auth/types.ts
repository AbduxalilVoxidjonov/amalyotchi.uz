import type { UserRole } from './roles';

/**
 * Backend DTO'lari — src/Amaliyotchi.Application/Features/Auth/AuthContracts.cs
 * Maydonlar System.Text.Json default'i bilan camelCase da keladi.
 */

/**
 * Serverdan kelgan xom ko'rinish (role — raqam yoki string bo'lishi mumkin; v2 backend camelCase
 * string beradi: "student"). Kontrakt v2: talaba uchun guruh/kurs/HEMIS ID ham keladi — admin va
 * tyutorda bu maydonlar `null` (eski javoblarda umuman bo'lmasligi mumkin → ixtiyoriy).
 */
export interface UserSummaryDto {
  id: string;
  fullName: string;
  role: number | string;
  facultyId: string | null;
  phoneNumber: string | null;
  groupId?: string | null;
  /** "412-22" */
  groupName?: string | null;
  /** 1–6 */
  course?: number | null;
  /** Login identifikatori (5–20 raqam). Barcha rollar uchun kelishi mumkin. */
  hemisId?: string | null;
}

export interface AuthResultDto {
  accessToken: string;
  /** ISO 8601 (DateTimeOffset). */
  accessTokenExpiresAt: string;
  /** Body'da keladi (cookie EMAS); rotatsiya qilinadi — har refresh'da yangisi. */
  refreshToken: string;
  user: UserSummaryDto;
}

/** POST /api/auth/login */
export interface LoginRequest {
  hemisId: string;
  password: string;
}

/** POST /api/auth/refresh */
export interface RefreshRequest {
  refreshToken: string;
}

/** POST /api/auth/logout (Bearer talab qiladi, 204 qaytaradi) */
export interface LogoutRequest {
  refreshToken: string;
}

/** Frontend ichida ishlatiladigan normallashtirilgan foydalanuvchi. */
export interface AuthUser {
  id: string;
  fullName: string;
  role: UserRole;
  facultyId: string | null;
  phoneNumber: string | null;
  /** Faqat talabada (TWA "Yana" varag'i: `412-22 · 3-kurs`); admin/tyutorda null. */
  groupId: string | null;
  groupName: string | null;
  course: number | null;
  hemisId: string | null;
}
