import { Navigate } from 'react-router-dom';
import { homePathForRole, useAuth } from '@/shared/auth/useAuth';

/** `/` → admin bo'lsa /admin, aks holda /login. */
export function RootRedirect() {
  const { status, user } = useAuth();
  if (status === 'restoring') return null;
  return <Navigate to={homePathForRole(user?.role)} replace />;
}

export default RootRedirect;
