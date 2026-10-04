import type { ReactNode } from 'react';
import { Navigate } from 'react-router-dom';
import { useAuth } from './useAuth';

/**
 * v3.28 — faqat bosh admin uchun sahifalar ("Adminlar"). `RequireRole` (Admin) ichida ishlatiladi;
 * oddiy admin admin bosh sahifasiga qaytariladi (server ham 403 beradi).
 */
export function RequireSuperAdmin({ children }: { children: ReactNode }) {
  const { isSuperAdmin } = useAuth();
  if (!isSuperAdmin) return <Navigate to="/admin" replace />;
  return <>{children}</>;
}
