import { createBrowserRouter } from 'react-router-dom';
import { ModulePingPage } from '../features/modules/ModulePingPage';
import { DashboardPage } from '../pages/DashboardPage';
import { AppLayout } from '../shared/layout/AppLayout';

export const router = createBrowserRouter([
  {
    path: '/',
    element: <AppLayout />,
    children: [
      { index: true, element: <DashboardPage /> },
      { path: 'modules/:modulePath', element: <ModulePingPage /> },
    ],
  },
]);
