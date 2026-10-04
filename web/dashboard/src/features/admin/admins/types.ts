/**
 * v3.28 — "Adminlar" bo'limi (faqat bosh admin; boshqalar 403).
 *   GET   /api/admin/admins                 → { items: AdminAccount[] }
 *   POST  /api/admin/admins                 { fullName, login, phoneNumber?, password? }
 *                                           → 201 AdminAccount (+ `temporaryPassword` — parol berilmasa)
 *   PUT   /api/admin/admins/{id}            { fullName, phoneNumber? } → AdminAccount
 *   PATCH /api/admin/admins/{id}/status     { isActive } → AdminAccount
 *   POST  /api/admin/admins/{id}/password   → { temporaryPassword }
 * Xatolar: 400 (`errors`: fullName/login/phoneNumber/password) · 403 · 404 ·
 *          409 (login band; bosh adminni yoki o'zini bloklab bo'lmaydi).
 */
export interface AdminAccount {
  id: string;
  fullName: string;
  login: string;
  /** E.164 "+998901234567" yoki null. */
  phoneNumber: string | null;
  isActive: boolean;
  isSuperAdmin: boolean;
  /** ISO 8601 yoki null (hali kirmagan). */
  lastLoginAt: string | null;
  createdAt: string;
}

export interface AdminListResponse {
  items: AdminAccount[];
}

export interface AdminCreateInput {
  fullName: string;
  login: string;
  phoneNumber?: string | null;
  /** Berilmasa server vaqtinchalik parol yaratadi (javobda `temporaryPassword`). */
  password?: string;
}

/** 201 javobi — vaqtinchalik parol faqat shu yerda bir marta keladi. */
export interface AdminCreateResult extends AdminAccount {
  temporaryPassword?: string | null;
}

export interface AdminUpdateInput {
  fullName: string;
  phoneNumber?: string | null;
}

export interface AdminPasswordResetResult {
  temporaryPassword: string;
}
