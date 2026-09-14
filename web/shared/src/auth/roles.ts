/**
 * Rol nomlari — src/Amaliyotchi.Domain/Enums/UserRole.cs (Admin=1, Tutor=2, Student=3).
 * JWT `role` claim'ida STRING ("Admin"), DTO'da esa (JsonStringEnumConverter yo'q) RAQAM keladi.
 * `toUserRole` ikkalasini ham qabul qiladi.
 */
export const UserRole = {
  Admin: 'Admin',
  Tutor: 'Tutor',
  Student: 'Student',
} as const;

export type UserRole = (typeof UserRole)[keyof typeof UserRole];

const byNumber: Record<number, UserRole> = {
  1: UserRole.Admin,
  2: UserRole.Tutor,
  3: UserRole.Student,
};

export function toUserRole(value: unknown): UserRole | undefined {
  if (typeof value === 'number') return byNumber[value];
  if (typeof value === 'string') {
    const n = Number(value);
    if (!Number.isNaN(n) && value.trim() !== '') return byNumber[n];
    const match = (Object.values(UserRole) as string[]).find(
      (r) => r.toLowerCase() === value.toLowerCase(),
    );
    return match as UserRole | undefined;
  }
  return undefined;
}

export function isUserRole(value: unknown): value is UserRole {
  return toUserRole(value) !== undefined && typeof value === 'string';
}
