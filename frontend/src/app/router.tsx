import { createBrowserRouter } from 'react-router-dom';
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

import { ProtectedRoute } from '../shared/auth';

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

