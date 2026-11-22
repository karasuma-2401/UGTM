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
import { StudentRenameRequestPage } from '../features/topics/pages/StudentRenameRequestPage';
import { RenameReviewEntryPage, RenameReviewPage } from '../features/topics/pages/RenameReviewPage';
import {
  SyllabusSubmissionPage,
  SyllabusReviewPage,
  MidtermSubmissionPage,
  MidtermReviewPage,
  StudentMilestoneSubmissionPage,
  AdvisorMilestoneSetupPage,
  AdvisorMilestoneReviewPage,
  FinalReportSubmissionPage,
  StudentArchivalPage,
  ArchivalReviewPage,
  AdvisorArchivalPage,
} from '../features/thesis';
import {
  ReviewerAssignmentPage,
  CouncilApprovalPage,
  CouncilMinutesPage,
  ResultsEntryPage,
} from '../features/councils';
import { CouncilSimilarityReportsPage } from '../features/thesis/pages/CouncilSimilarityReportsPage';
import { LecturerGradingPage } from '../features/thesis/pages/LecturerGradingPage';
import { CouncilGradingPage } from '../features/councils/pages/CouncilGradingPage';
import { CouncilMembersPage } from '../features/councils/pages/CouncilMembersPage';
import { IdentityLayout, IdentityIndexRedirect } from '../features/identity';

import { ProtectedRoute } from '../shared/auth';
import { PublicLayout } from '../features/landing/PublicLayout';
import { HomeEntry, LandingPage } from '../features/landing/LandingPage';
import { LibraryPage } from '../features/library/LibraryPage';
import { ThesisDetailPage } from '../features/library/ThesisDetailPage';
import { RolePermissionPage } from '../features/identity';
import { ProfilePage } from '../features/profile';
import { RoleManagementPage } from '../features/identity/pages/RoleManagementPage';
import { AuditLogPage } from '../features/audit';
import { ScoreConfigPage, PeriodPage, SchoolRosterPage } from '../features/academic';

export const router = createBrowserRouter([
  {
    element: <PublicLayout />,
    children: [
      { path: '/', element: <HomeEntry /> },
      { path: '/welcome', element: <LandingPage /> },
      { path: '/library', element: <LibraryPage /> },
      { path: '/library/:slug', element: <ThesisDetailPage /> },
    ],
  },
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
      { path: 'dashboard', element: <DashboardPage /> },
      { path: 'profile', element: <ProfilePage /> },
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
        path: 'topics/rename',
        element: <ProtectedRoute allowedRoles={['Student']}><StudentRenameRequestPage /></ProtectedRoute>,
      },
      {
        path: 'topics/rename/review',
        element: <ProtectedRoute allowedRoles={['Lecturer', 'Faculty', 'AcademicAffairs', 'Admin']}><RenameReviewEntryPage /></ProtectedRoute>,
      },
      {
        path: 'topics/rename/review/advisor',
        element: <ProtectedRoute allowedRoles={['Lecturer', 'Admin']}><RenameReviewPage level="advisor" /></ProtectedRoute>,
      },
      {
        path: 'topics/rename/review/faculty',
        element: <ProtectedRoute allowedRoles={['Faculty', 'Admin']}><RenameReviewPage level="faculty" /></ProtectedRoute>,
      },
      {
        path: 'topics/rename/review/academic-affairs',
        element: <ProtectedRoute allowedRoles={['AcademicAffairs', 'Admin']}><RenameReviewPage level="academic-affairs" /></ProtectedRoute>,
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
        path: 'thesis/midterm',
        element: (
          <ProtectedRoute allowedRoles={['Student', 'Admin']}>
            <MidtermSubmissionPage />
          </ProtectedRoute>
        ),
      },
      {
        path: 'thesis/milestones',
        element: (
          <ProtectedRoute allowedRoles={['Student', 'Admin']}>
            <StudentMilestoneSubmissionPage />
          </ProtectedRoute>
        ),
      },
      {
        path: 'thesis/final-report',
        element: (
          <ProtectedRoute allowedRoles={['Student', 'Admin']}>
            <FinalReportSubmissionPage />
          </ProtectedRoute>
        ),
      },
      {
        path: 'thesis/archival',
        element: (
          <ProtectedRoute allowedRoles={['Student']}>
            <StudentArchivalPage />
          </ProtectedRoute>
        ),
      },
      {
        path: 'thesis/review/archival',
        element: (
          <ProtectedRoute allowedRoles={['Faculty', 'Admin']}>
            <ArchivalReviewPage />
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
        path: 'thesis/review/midterm',
        element: (
          <ProtectedRoute allowedRoles={['Faculty', 'Admin', 'AcademicAffairs']}>
            <MidtermReviewPage />
          </ProtectedRoute>
        ),
      },
      {
        path: 'thesis/review',
        element: <Navigate to="/thesis/review/syllabus" replace />,
      },
      {
        path: 'thesis/progress',
        element: <Navigate to="/thesis/milestones" replace />,
      },
      {
        path: 'councils/reviewer-assignments',
        element: (
          <ProtectedRoute allowedRoles={['Faculty', 'Admin', 'AcademicAffairs']}>
            <ReviewerAssignmentPage />
          </ProtectedRoute>
        ),
      },
      {
        path: 'councils/similarity-reports',
        element: <ProtectedRoute allowedRoles={['CouncilMember']}><CouncilSimilarityReportsPage /></ProtectedRoute>,
      },
      {
        path: 'councils/grading',
        element: <ProtectedRoute allowedRoles={['CouncilMember', 'Admin']}><CouncilGradingPage /></ProtectedRoute>,
      },
      {
        path: 'councils/minutes',
        element: (
          <ProtectedRoute allowedRoles={['CouncilMember', 'Lecturer', 'Faculty', 'AcademicAffairs', 'Admin']}>
            <CouncilMinutesPage />
          </ProtectedRoute>
        ),
      },
      {
        path: 'councils/members',
        element: <ProtectedRoute allowedRoles={['Faculty', 'Admin']}><CouncilMembersPage /></ProtectedRoute>,
      },
      {
        path: 'councils/approval',
        element: (
          <ProtectedRoute allowedRoles={['AcademicAffairs', 'Admin']}>
            <CouncilApprovalPage />
          </ProtectedRoute>
        ),
      },
      {
        path: 'councils/results',
        element: (
          <ProtectedRoute allowedRoles={['Faculty', 'AcademicAffairs', 'Admin', 'Student']}>
            <ResultsEntryPage />
          </ProtectedRoute>
        ),
      },
      {
        path: 'thesis/lecturer-grading',
        element: <ProtectedRoute allowedRoles={['Lecturer', 'Admin']}><LecturerGradingPage /></ProtectedRoute>,
      },
      {
        path: 'advisor/milestones',
        element: (
          <ProtectedRoute allowedRoles={['Lecturer', 'Admin']}>
            <AdvisorMilestoneSetupPage />
          </ProtectedRoute>
        ),
      },
      {
        path: 'advisor/milestones/:milestoneId/review',
        element: (
          <ProtectedRoute allowedRoles={['Lecturer', 'Admin']}>
            <AdvisorMilestoneReviewPage />
          </ProtectedRoute>
        ),
      },
      {
        path: 'advisor/archival',
        element: (
          <ProtectedRoute allowedRoles={['Lecturer', 'Admin']}>
            <AdvisorArchivalPage />
          </ProtectedRoute>
        ),
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
          <ProtectedRoute allowedRoles={['Admin', 'AcademicAffairs']}>
            <PeriodPage />
          </ProtectedRoute>
        ),
      },
      {
        path: 'academic/periods',
        element: (
          <ProtectedRoute allowedRoles={['Admin', 'AcademicAffairs']}>
            <PeriodPage />
          </ProtectedRoute>
        ),
      },
      {
        path: 'academic/roster',
        element: (
          <ProtectedRoute allowedRoles={['Admin', 'AcademicAffairs']}>
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
            path: 'roles',
            element: <ProtectedRoute requiredPermissions={['identity.roles.manage']}><RoleManagementPage /></ProtectedRoute>,
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
