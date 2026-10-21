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
import { UsersPage } from '../features/admin/pages/UsersPage';
import { IdentityLayout, IdentityIndexRedirect } from '../features/admin/layouts/IdentityLayout';

import { ProtectedRoute } from '../shared/auth';
import { RolePermissionPage, AuditLogPage, ScoreConfigPage } from '../features/admin';

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
