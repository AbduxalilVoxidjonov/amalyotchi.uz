import type { AdminAccount } from './types';

/** Bloklash tugmasi nega o'chiq — bosh admin yoki o'zi (server ham 409 beradi). */
export function blockDisabledReason(row: AdminAccount, currentUserId: string | undefined) {
  if (row.isSuperAdmin) return "Bosh adminni bloklab bo'lmaydi";
  if (row.id === currentUserId) return "O'zingizni bloklab bo'lmaydi";
  return null;
}
