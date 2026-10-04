import { lazy, Suspense } from 'react';
import {
  createBrowserRouter,
  createMemoryRouter,
  Outlet,
  type RouteObject,
} from 'react-router-dom';
import { UserRole } from '@amaliyotchi/shared/auth';
import { RequireRole } from '@/shared/auth/RequireRole';
import { RequireSuperAdmin } from '@/shared/auth/RequireSuperAdmin';
import { useSessionBootstrap } from '@/features/auth/hooks';
import { ComingSoonPage } from '@/pages/ComingSoonPage';
import { NotFoundPage } from '@/pages/NotFoundPage';
import { RootRedirect } from '@/pages/RootRedirect';
import { AppShell } from './layout/AppShell';
import { RouteError } from './RouteError';

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
const StudentsPage = lazy(() => import('@/features/admin/students/StudentsPage'));
const AdminStudentDetailPage = lazy(() => import('@/features/admin/students/StudentDetailPage'));
const CompaniesPage = lazy(() => import('@/features/admin/companies/CompaniesPage'));
const CompanyDetailPage = lazy(() => import('@/features/admin/companies/CompanyDetailPage'));
const PracticePeriodsPage = lazy(
  () => import('@/features/admin/practice-periods/PracticePeriodsPage'),
);
const PracticePeriodCreatePage = lazy(
  () => import('@/features/admin/practice-periods/PracticePeriodCreatePage'),
);
const PracticePeriodDetailPage = lazy(
  () => import('@/features/admin/practice-periods/PracticePeriodDetailPage'),
);
const PeriodGroupPage = lazy(() => import('@/features/admin/practice-periods/PeriodGroupPage'));
const MessagesPage = lazy(() => import('@/features/admin/messages/MessagesPage'));
const MessageDetailPage = lazy(() => import('@/features/admin/messages/MessageDetailPage'));
const AuditPage = lazy(() => import('@/features/admin/audit/AuditPage'));
const SettingsPage = lazy(() => import('@/features/admin/settings/SettingsPage'));
const ReportsPage = lazy(() => import('@/features/reports/ReportsPage'));
const TodayPage = lazy(() => import('@/features/admin/today/TodayPage'));
const ApplicationsPage = lazy(() => import('@/features/admin/applications/ApplicationsPage'));
const FaceEnrollmentsPage = lazy(() => import('@/features/admin/face/FaceEnrollmentsPage'));
const DiariesPage = lazy(() => import('@/features/admin/diaries/DiariesPage'));
const CalendarPage = lazy(() => import('@/features/admin/calendar/CalendarPage'));
const MapPage = lazy(() => import('@/features/admin/map/MapPage'));
const GradingPage = lazy(() => import('@/features/admin/grading/GradingPage'));
const AdminsPage = lazy(() => import('@/features/admin/admins/AdminsPage'));
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
    // Render/lazy-chunk xatosi → inglizcha "Unexpected Application Error!" o'rniga o'zbekcha ekran.
    errorElement: <RouteError scope="root" />,
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
          {
            // Sahifa xatosi AppShell ichida ko'rsatiladi (sidebar/topbar ishlaydi).
            errorElement: <RouteError scope="page" />,
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
              // v3.28: sobiq tyutor bo'limi sahifalari (kundalik nazorat) — endi adminda.
              { path: 'today', element: <TodayPage /> },
              { path: 'applications', element: <ApplicationsPage /> },
              { path: 'face', element: <FaceEnrollmentsPage /> },
              { path: 'diaries', element: <DiariesPage /> },
              { path: 'grading', element: <GradingPage /> },
              { path: 'calendar', element: <CalendarPage /> },
              { path: 'map', element: <MapPage /> },
              { path: 'students', element: <StudentsPage /> },
              { path: 'students/:studentId', element: <AdminStudentDetailPage /> },
              { path: 'companies', element: <CompaniesPage /> },
              { path: 'companies/:companyId', element: <CompanyDetailPage /> },
              { path: 'practice-periods', element: <PracticePeriodsPage /> },
              { path: 'practice-periods/new', element: <PracticePeriodCreatePage /> },
              { path: 'practice-periods/:periodId', element: <PracticePeriodDetailPage /> },
              {
                path: 'practice-periods/:periodId/groups/:groupId',
                element: <PeriodGroupPage />,
              },
              { path: 'messages', element: <MessagesPage /> },
              { path: 'messages/:messageId', element: <MessageDetailPage /> },
              { path: 'reports', element: <ReportsPage /> },
              {
                // Faqat bosh admin (v3.28); oddiy admin → /admin.
                path: 'admins',
                element: (
                  <RequireSuperAdmin>
                    <AdminsPage />
                  </RequireSuperAdmin>
                ),
              },
              { path: 'audit', element: <AuditPage /> },
              { path: 'settings', element: <SettingsPage /> },
              { path: '*', element: <ComingSoonPage /> },
            ],
          },
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
