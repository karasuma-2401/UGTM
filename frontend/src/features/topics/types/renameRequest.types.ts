export type RenameRequestStatus =
  | 'Created'
  | 'AdvisorConfirmed'
  | 'FacultyApproved'
  | 'Completed'
  | 'RejectedByAdvisor'
  | 'RejectedByFaculty'
  | 'RejectedByAA'
  | 'Closed';

export interface RenameRequest {
  id: string;
  groupId: string;
  requestedByStudentId: string;
  oldTitleVi: string;
  newTitleVi: string;
  oldTitleEn: string | null;
  newTitleEn: string | null;
  reason: string;
  status: RenameRequestStatus;
  rejectionReason: string | null;
  rejectedBy: string | null;
  rejectedAt: string | null;
  advisorConfirmedAt: string | null;
  facultyApprovedAt: string | null;
  aaApprovedAt: string | null;
  createdAt: string;
}

export const renameStatusLabels: Record<RenameRequestStatus, string> = {
  Created: 'Chờ giảng viên duyệt',
  AdvisorConfirmed: 'Chờ Khoa duyệt',
  FacultyApproved: 'Chờ Phòng đào tạo duyệt',
  Completed: 'Đã hoàn tất',
  RejectedByAdvisor: 'Giảng viên từ chối',
  RejectedByFaculty: 'Khoa từ chối',
  RejectedByAA: 'Phòng đào tạo từ chối',
  Closed: 'Đã đóng',
};
