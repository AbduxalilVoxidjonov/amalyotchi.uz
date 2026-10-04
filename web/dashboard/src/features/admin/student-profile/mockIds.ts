/**
 * Admin talabalar ro'yxati mock id'si (`s1`…) → talaba profili mock id'si (`s-341030`…).
 * Bog'liqliksiz modul (aylanma import bo'lmasin): admin talaba mock'i va yuz mock'i ishlatadi.
 * Backend'da ikkalasi ham bitta `User.Id` — bu juftlik faqat mock ma'lumotlarini bog'laydi.
 */
export const ADMIN_TO_PROFILE_ID: Readonly<Record<string, string>> = {
  s1: 's-341030',
  s2: 's-341032',
  s3: 's-341031',
  s4: 's-341034',
  s5: 's-341033',
};

/** Admin id berilsa — profil id'si, aks holda o'zi. */
export function profileMockId(id: string): string {
  return ADMIN_TO_PROFILE_ID[id] ?? id;
}
