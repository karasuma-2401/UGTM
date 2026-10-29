import { createBrowserRouter, Navigate } from 'react-router-dom';
import { ModulePingPage } from '../features/modules/ModulePingPage';
import { DashboardPage } from '../pages/DashboardPage';
import { ErrorPage } from '../pages/ErrorPage';
import { UiTestPage } from '../pages/UiTestPage';
import { AppLayout } from '../shared/layout/AppLayout';
import { AuthLayout } from '../features/auth/layouts/AuthLayout';
import { ActivatePage } from '../features/auth/pages/ActivatePage';
import { ForgotPasswordPage } from '../features/auth/pages/ForgotPasswordPage';
import { LoginPage } from '../features/auth/pages/LoginPage';
import { OtpPage } from '../features/auth/pages/OtpPage';
import { ResetPasswordPage } from '../features/auth/pages/ResetPasswordPage';
import { UsersPage } from '../features/identity';
import { TopicReviewPage } from '../features/topics/pages/TopicReviewPage';
import { TopicGuidancePage } from '../features/topics/pages/TopicGuidancePage';
import { StudentTopicCatalogPage } from '../features/topics/pages/StudentTopicCatalogPage';
import { SyllabusSubmissionPage, SyllabusReviewPage } from '../features/thesis';
import { IdentityLayout, IdentityIndexRedirect } from '../features/identity';

import { ProtectedRoute } from '../shared/auth';
import { RolePermissionPage } from '../features/identity';
import { AuditLogPage } from '../features/audit';
import { ScoreConfigPage, PeriodPage, SchoolRosterPage } from '../features/academic';

export const router = createBrowserRouter([
  {
    element: <AuthLayout />,
    children: [
      { path: '/login', element: <LoginPage /> },
      { path: '/activate', element: <ActivatePage /> },
      { path: '/forgot-password', element: <ForgotPasswordPage /> },
      { path: '/verify-otp', element: <OtpPage /> },
      { path: '/reset-password', element: <ResetPasswordPage /> },
    ],
  },
  {
    path: '/',
    element: (
      <ProtectedRoute>
        <AppLayout />
      </ProtectedRoute>
    ),
    children: [
      { index: true, element: <DashboardPage /> },
      { path: 'ui-test', element: <UiTestPage /> },
      {
        path: 'topics/catalog',
        element: (
          <ProtectedRoute allowedRoles={['Student', 'Admin']}>
            <StudentTopicCatalogPage />
          </ProtectedRoute>
        ),
      },
      {
        path: 'thesis/proposal',
        element: (
          <ProtectedRoute allowedRoles={['Student', 'Admin']}>
            <SyllabusSubmissionPage />
          </ProtectedRoute>
        ),
      },
      {
        path: 'thesis/review/syllabus',
        element: (
          <ProtectedRoute allowedRoles={['Faculty', 'Admin', 'AcademicAffairs']}>
            <SyllabusReviewPage />
          </ProtectedRoute>
        ),
      },
      {
        path: 'thesis/progress',
        element: <Navigate to="/thesis/proposal" replace />,
      },
      {
        path: 'topics/advising',
        element: (
          <ProtectedRoute allowedRoles={['Lecturer', 'Admin']}>
            <TopicGuidancePage />
          </ProtectedRoute>
        ),
      },
      {
        path: 'topics/propose',
        element: (
          <ProtectedRoute allowedRoles={['Lecturer', 'Admin']}>
            <Navigate to="/topics/advising" replace />
          </ProtectedRoute>
        ),
      },
      {
        path: 'topics/review',
        element: (
          <ProtectedRoute allowedRoles={['Faculty', 'Admin']}>
            <TopicReviewPage />
          </ProtectedRoute>
        ),
      },
      {
        path: 'admin/audit-logs',
        element: (
          <ProtectedRoute allowedRoles={['Admin']}>
            <AuditLogPage />
          </ProtectedRoute>
        ),
      },
      {
        path: 'admin/grade-config',
        element: (
          <ProtectedRoute allowedRoles={['Admin', 'AcademicAffairs']}>
            <ScoreConfigPage />
          </ProtectedRoute>
        ),
      },
      {
        path: 'admin/periods',
        element: (
          <ProtectedRoute>
            <PeriodPage />
          </ProtectedRoute>
        ),
      },
      {
        path: 'academic/periods',
        element: (
          <ProtectedRoute>
            <PeriodPage />
          </ProtectedRoute>
        ),
      },
      {
        path: 'academic/roster',
        element: (
          <ProtectedRoute>
            <SchoolRosterPage />
          </ProtectedRoute>
        ),
      },
      {
        path: 'academic/school-roster',
        element: <Navigate to="/academic/roster" replace />,
      },
      {
        path: 'admin/school-roster',
        element: <Navigate to="/academic/roster" replace />,
      },
      {
        path: 'modules/identity',
        element: <IdentityLayout />,
        children: [
          { index: true, element: <IdentityIndexRedirect /> },
          {
            path: 'accounts',
            element: <Navigate to="../students" replace />,
          },
          {
            path: 'students',
            element: (
              <ProtectedRoute requiredPermissions={['identity.users.read']}>
                <UsersPage key="students" accountGroup="Student" />
              </ProtectedRoute>
            ),
          },
          {
            path: 'staff',
            element: (
              <ProtectedRoute requiredPermissions={['identity.users.read']}>
                <UsersPage key="staff" accountGroup="Staff" />
              </ProtectedRoute>
            ),
          },
          {
            path: 'permissions',
            element: (
              <ProtectedRoute requiredPermissions={['identity.roles.manage']}>
                <ProtectedRoute requiredPermissions={['identity.permissions.read']}>
                  <RolePermissionPage />
                </ProtectedRoute>
              </ProtectedRoute>
            ),
          },
          { path: '*', element: <Navigate to="/404" replace /> },
        ],
      },
      { path: 'modules/:modulePath', element: <ModulePingPage /> },
      { path: '403', element: <ErrorPage code={403} /> },
      { path: '404', element: <ErrorPage code={404} /> },
      { path: '*', element: <ErrorPage code={404} /> },
    ],
  },
  {
    path: '*',
    element: <ErrorPage code={404} variant="full" />,
  },
]);
