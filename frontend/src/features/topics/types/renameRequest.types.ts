import type { StatusTone } from '@/components/ui/status-badge';

export type RenameRequestStatus =
  | 'Created'
  | 'AdvisorConfirmed'
  | 'FacultyApproved'
  | 'Completed'
  | 'RejectedByAdvisor'
  | 'RejectedByFaculty'
  | 'RejectedByAA'
  | 'Closed';

export interface RenameRequestMember {
  studentId: string;
  userCode?: string | null;
  fullName?: string | null;
}

export interface RenameRequest {
  id: string;
  groupId: string;
  requestedByStudentId: string;
  oldTitleVi: string;
  newTitleVi: string;
  oldTitleEn?: string | null;
  newTitleEn?: string | null;
  reason: string;
  status: RenameRequestStatus;
  rejectionReason?: string | null;
  rejectedBy?: string | null;
  rejectedAt?: string | null;
  advisorConfirmedAt?: string | null;
  facultyApprovedAt?: string | null;
  aaApprovedAt?: string | null;
  createdAt: string;
  batchId?: string | null;
  batchName?: string | null;
  advisorId?: string | null;
  advisorName?: string | null;
  members?: RenameRequestMember[] | null;
}

export const renameStatusLabels: Record<RenameRequestStatus, string> = {
  Created: 'Chờ GVHD duyệt',
  AdvisorConfirmed: 'Chờ Khoa duyệt',
  FacultyApproved: 'Chờ PĐT duyệt',
  Completed: 'Đã hoàn tất',
  RejectedByAdvisor: 'GVHD từ chối',
  RejectedByFaculty: 'Khoa từ chối',
  RejectedByAA: 'PĐT từ chối',
  Closed: 'Đã đóng',
};

export const renameStatusTones: Record<RenameRequestStatus, StatusTone> = {
  Created: 'info',
  AdvisorConfirmed: 'info',
  FacultyApproved: 'warning',
  Completed: 'success',
  RejectedByAdvisor: 'error',
  RejectedByFaculty: 'error',
  RejectedByAA: 'error',
  Closed: 'neutral',
};
