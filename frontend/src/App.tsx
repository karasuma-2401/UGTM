import { QueryClientProvider } from '@tanstack/react-query';
import { ConfigProvider, theme as antdTheme } from 'antd';
import { RouterProvider } from 'react-router-dom';
import { queryClient } from './app/queryClient';
import { router } from './app/router';
import { AuthProvider } from './shared/auth';
import { CampusPresenceProvider } from './features/landing/CampusPresence';
import { NotificationProvider } from './shared/notifications';
import { ThemeProvider, useTheme } from './shared/theme';

function ThemedApp() {
  const { resolvedTheme } = useTheme();
  const isDark = resolvedTheme === 'dark';

  return (
    <ConfigProvider
      theme={{
        algorithm: isDark ? antdTheme.darkAlgorithm : antdTheme.defaultAlgorithm,
        token: isDark
          ? {
              colorPrimary: '#3b82f6',
              colorBgBase: '#090d16',
              colorBgContainer: '#0f172a',
              colorBgElevated: '#162033',
              colorBorder: '#1e293b',
              colorText: '#f1f5f9',
              colorTextSecondary: '#94a3b8',
            }
          : {
              colorPrimary: '#2563eb',
            },
      }}
    >
      <QueryClientProvider client={queryClient}>
        <NotificationProvider>
          <AuthProvider>
            <CampusPresenceProvider>
              <RouterProvider router={router} />
            </CampusPresenceProvider>
          </AuthProvider>
        </NotificationProvider>
      </QueryClientProvider>
    </ConfigProvider>
  );
}

function App() {
  return (
    <ThemeProvider>
      <ThemedApp />
    </ThemeProvider>
  );
}

export default App;
