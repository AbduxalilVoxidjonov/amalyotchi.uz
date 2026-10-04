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
      /** v3.28 — bosh admin ("Adminlar" bo'limi). */
      isSuperAdmin: s.user?.role === UserRole.Admin && s.user.isSuperAdmin,
    })),
  );
}

/**
 * Rolga qarab foydalanuvchining "uy" sahifasi. v3.28: dashboard faqat admin uchun —
 * boshqa rol (talaba, eski tyutor sessiyasi) kirish sahifasiga qaytadi.
 */
export function homePathForRole(role: UserRole | undefined): string {
  return role === UserRole.Admin ? '/admin' : '/login';
}
