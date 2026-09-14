import { useShallow } from 'zustand/react/shallow';
import { UserRole } from '@amaliyotchi/shared/auth';
import { useAuthStore } from './store';

/** Komponentlar uchun qulay auth ko'rinishi. Re-render faqat kerakli maydonlar o'zgarganda. */
export function useAuth() {
  return useAuthStore(
    useShallow((s) => ({
      status: s.status,
      user: s.user,
      isAuthenticated: s.status === 'authenticated',
      isRestoring: s.status === 'restoring',
      isAdmin: s.user?.role === UserRole.Admin,
      isTutor: s.user?.role === UserRole.Tutor,
    })),
  );
}

/** Rolga qarab foydalanuvchining "uy" sahifasi. */
export function homePathForRole(role: UserRole | undefined): string {
  switch (role) {
    case UserRole.Admin:
      return '/admin';
    case UserRole.Tutor:
      return '/tutor';
    default:
      return '/login';
  }
}
