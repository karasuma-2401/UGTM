import { createBrowserRouter } from 'react-router-dom';
import { ModulePingPage } from '../features/modules/ModulePingPage';
import { DashboardPage } from '../pages/DashboardPage';
import { MainLayout } from '../shared/layout/MainLayout';

export const router = createBrowserRouter([
  {
    path: '/',
    element: <MainLayout />,
    children: [
      { index: true, element: <DashboardPage /> },
      { path: 'modules/:modulePath', element: <ModulePingPage /> },
    ],
  },
]);
