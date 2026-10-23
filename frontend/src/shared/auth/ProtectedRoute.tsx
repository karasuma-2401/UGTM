import React from 'react';
import { Navigate, Outlet, useLocation } from 'react-router-dom';
import { useAuth } from './useAuth';

export interface ProtectedRouteProps {
  allowedRoles?: string[];
  requiredPermissions?: string[];
  redirectTo?: string;
  children?: React.ReactNode;
}

export const ProtectedRoute: React.FC<ProtectedRouteProps> = ({
  allowedRoles,
  requiredPermissions,
  redirectTo = '/login',
  children,
}) => {
  const {
    isAuthenticated,
    isLoading,
    isLoggingOut,
    sessionError,
    retrySession,
    hasRole,
    hasPermission,
  } = useAuth();
  const location = useLocation();

  // An explicit logout starts a new login flow, without remembering the page
  // belonging to the previous session (or a different account).
  if (isLoggingOut) {
    return <Navigate to={redirectTo} state={null} replace />;
  }

  if (isLoading) {
    return (
      <div className="min-h-screen flex items-center justify-center bg-slate-50">
        <div role="status" className="flex items-center gap-3 px-6">
          <svg
            aria-hidden="true"
            viewBox="0 0 40 40"
            fill="none"
            className="size-10 shrink-0 text-primary motion-safe:animate-[access-check-scan_1.8s_ease-in-out_infinite]"
          >
            <circle
              cx="16"
              cy="16"
              r="10"
              fill="currentColor"
              fillOpacity="0.08"
              stroke="currentColor"
              strokeWidth="2.5"
            />
            <path d="m23.5 23.5 9 9" stroke="currentColor" strokeWidth="4" strokeLinecap="round" />
            <path
              d="M11 16a5 5 0 0 1 5-5"
              stroke="currentColor"
              strokeWidth="2"
              strokeLinecap="round"
              opacity="0.45"
            />
          </svg>
          <p className="text-xs font-medium text-slate-500">Đang kiểm tra quyền truy cập...</p>
        </div>
      </div>
    );
  }

  if (sessionError) {
    return (
      <div className="min-h-screen flex items-center justify-center bg-slate-50 p-6">
        <div className="max-w-md text-center space-y-4" role="alert">
          <p>{sessionError}</p>
          <button
            type="button"
            onClick={retrySession}
            className="rounded-md bg-primary px-4 py-2 text-white"
          >
            Thử lại
          </button>
        </div>
      </div>
    );
  }

  if (!isAuthenticated) {
    return <Navigate to={redirectTo} state={{ from: location }} replace />;
  }

  if (allowedRoles && allowedRoles.length > 0 && !hasRole(allowedRoles)) {
    return <Navigate to="/403" replace />;
  }

  if (
    requiredPermissions &&
    requiredPermissions.length > 0 &&
    !hasPermission(requiredPermissions)
  ) {
    return <Navigate to="/403" replace />;
  }

  return children ? <>{children}</> : <Outlet />;
};
