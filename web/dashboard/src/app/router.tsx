import { lazy, Suspense } from 'react';
import {
  createBrowserRouter,
  createMemoryRouter,
  Outlet,
  type RouteObject,
} from 'react-router-dom';
import { UserRole } from '@amaliyotchi/shared/auth';
import { RequireRole } from '@/shared/auth/RequireRole';
import { useSessionBootstrap } from '@/features/auth/hooks';
import { ComingSoonPage } from '@/pages/ComingSoonPage';
import { NotFoundPage } from '@/pages/NotFoundPage';
import { RootRedirect } from '@/pages/RootRedirect';
import { AppShell } from './layout/AppShell';

// Lazy chunk'lar — har bo'lim alohida yuklanadi.
const LoginPage = lazy(() => import('@/features/auth/LoginPage'));
const AdminDashboardPage = lazy(() => import('@/features/admin/dashboard/AdminDashboardPage'));
const FacultiesPage = lazy(() => import('@/features/admin/faculties/FacultiesPage'));
const FacultyDepartmentsPage = lazy(
  () => import('@/features/admin/faculties/departments/FacultyDepartmentsPage'),
);
const DepartmentDirectionsPage = lazy(
  () => import('@/features/admin/faculties/directions/DepartmentDirectionsPage'),
);
const DirectionGroupsPage = lazy(
  () => import('@/features/admin/faculties/groups/DirectionGroupsPage'),
);
const TutorsPage = lazy(() => import('@/features/admin/tutors/TutorsPage'));
const StudentsPage = lazy(() => import('@/features/admin/students/StudentsPage'));
const CompaniesPage = lazy(() => import('@/features/admin/companies/CompaniesPage'));
const AuditPage = lazy(() => import('@/features/admin/audit/AuditPage'));
const SettingsPage = lazy(() => import('@/features/admin/settings/SettingsPage'));
const ReportsPage = lazy(() => import('@/features/reports/ReportsPage'));
const TodayPage = lazy(() => import('@/features/tutor/today/TodayPage'));
const ApplicationsPage = lazy(() => import('@/features/tutor/applications/ApplicationsPage'));
const MyStudentsPage = lazy(() => import('@/features/tutor/students/MyStudentsPage'));
const DiariesPage = lazy(() => import('@/features/tutor/diaries/DiariesPage'));
const CalendarPage = lazy(() => import('@/features/tutor/calendar/CalendarPage'));
const MapPage = lazy(() => import('@/features/tutor/map/MapPage'));
const LeaveRequestsPage = lazy(() => import('@/features/tutor/leave-requests/LeaveRequestsPage'));
const GradingPage = lazy(() => import('@/features/tutor/grading/GradingPage'));
// `import.meta.env.DEV` build'da `false` ga almashadi → chunk umuman yaratilmaydi.
const KitPage = import.meta.env.DEV ? lazy(() => import('@/dev/KitPage')) : null;

/** Ildiz layout: sessiyani tiklaydi (refresh token bo'lsa) va Suspense chegarasi. */
function RootLayout() {
  useSessionBootstrap();
  return (
    <Suspense fallback={null /* dizayn: global loader keyin */}>
      <Outlet />
    </Suspense>
  );
}

export const routes: RouteObject[] = [
  {
    element: <RootLayout />,
    children: [
      { path: '/', element: <RootRedirect /> },
      { path: '/login', element: <LoginPage /> },
      {
        // Admin bo'limi — AppShell (sidebar + topbar) ichida. SPEC-NAV §3.1 marshrutlari.
        path: '/admin',
        element: (
          <RequireRole roles={[UserRole.Admin]}>
            <AppShell />
          </RequireRole>
        ),
        children: [
          { index: true, element: <AdminDashboardPage /> },
          { path: 'faculties', element: <FacultiesPage /> },
          { path: 'faculties/:facultyId', element: <FacultyDepartmentsPage /> },
          {
            path: 'faculties/:facultyId/departments/:departmentId',
            element: <DepartmentDirectionsPage />,
          },
          {
            path: 'faculties/:facultyId/departments/:departmentId/directions/:directionId',
            element: <DirectionGroupsPage />,
          },
          { path: 'tutors', element: <TutorsPage /> },
          { path: 'students', element: <StudentsPage /> },
          { path: 'companies', element: <CompaniesPage /> },
          { path: 'reports', element: <ReportsPage /> },
          { path: 'audit', element: <AuditPage /> },
          { path: 'settings', element: <SettingsPage /> },
          { path: '*', element: <ComingSoonPage /> },
        ],
      },
      {
        // Tyutor bo'limi. "Bugun" — index (/tutor). SPEC-NAV §3.1.
        path: '/tutor',
        element: (
          <RequireRole roles={[UserRole.Tutor]}>
            <AppShell />
          </RequireRole>
        ),
        children: [
          { index: true, element: <TodayPage /> },
          { path: 'applications', element: <ApplicationsPage /> },
          { path: 'students', element: <MyStudentsPage /> },
          { path: 'diaries', element: <DiariesPage /> },
          { path: 'calendar', element: <CalendarPage /> },
          { path: 'map', element: <MapPage /> },
          { path: 'leave-requests', element: <LeaveRequestsPage /> },
          { path: 'grading', element: <GradingPage /> },
          { path: 'reports', element: <ReportsPage /> },
          { path: '*', element: <ComingSoonPage /> },
        ],
      },
      // Faqat dev: UI kit ko'rgazmasi (Storybook o'rniga). Production build'ga kirmaydi.
      ...(KitPage ? [{ path: '/dev/kit', element: <KitPage /> }] : []),
      { path: '*', element: <NotFoundPage /> },
    ],
  },
];

export function createAppRouter() {
  return createBrowserRouter(routes);
}

/** Testlar uchun. */
export function createTestRouter(initialEntries: string[] = ['/']) {
  return createMemoryRouter(routes, { initialEntries });
}
