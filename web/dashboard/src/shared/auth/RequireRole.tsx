import type { ReactNode } from 'react';
import { Navigate, Outlet, useLocation } from 'react-router-dom';
import type { UserRole } from '@amaliyotchi/shared/auth';
import { homePathForRole, useAuth } from './useAuth';

interface RequireRoleProps {
  /** Ruxsat etilgan rollar. Bo'sh → faqat kirgan bo'lishi kifoya. */
  roles?: readonly UserRole[];
  /** Layout route sifatida ishlatilsa <Outlet/>, aks holda children. */
  children?: ReactNode;
  /** Sessiya tiklanayotganda ko'rsatiladigan element (dizayn keyin). */
  fallback?: ReactNode;
}

/**
 * Route guard: kirilmagan → /login (qaytish manzili state'da),
 * rol mos kelmasa → o'z rolining uy sahifasiga.
 */
export function RequireRole({ roles, children, fallback = null }: RequireRoleProps) {
  const { status, user } = useAuth();
  const location = useLocation();

  if (status === 'restoring') return <>{fallback}</>;

  if (status !== 'authenticated' || !user) {
    return <Navigate to="/login" replace state={{ from: location.pathname + location.search }} />;
  }

  if (roles && roles.length > 0 && !roles.includes(user.role)) {
    return <Navigate to={homePathForRole(user.role)} replace />;
  }

  return children !== undefined ? <>{children}</> : <Outlet />;
}
