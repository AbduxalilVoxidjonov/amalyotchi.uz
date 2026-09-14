import { Navigate } from 'react-router-dom';
import { homePathForRole, useAuth } from '@/shared/auth/useAuth';

/** `/` → rolga qarab /admin, /tutor yoki /login. */
export function RootRedirect() {
  const { status, user } = useAuth();
  if (status === 'restoring') return null;
  return <Navigate to={homePathForRole(user?.role)} replace />;
}

export default RootRedirect;
