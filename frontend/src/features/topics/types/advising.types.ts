import type { PagedResult, Topic } from './topic.types';
import type { StudentGroupDetailDto } from '@/features/thesis/types/syllabus.types';

// The milestone selector still consumes the topic tracking contract.
export interface TrackingTopic extends Topic {
  confirmedStudentsCount?: number;
  pendingConfirmationsCount?: number;
  registeredStudentsCount: number;
  remainingSlots: number;
  registrationStatus: 'Empty' | 'Registered' | 'Full';
  hasRegistration: boolean;
  isAvailable: boolean;
}

export const GROUP_STATUS_LABELS = {
  Registered: 'Đã ghi nhận nhóm',
  SyllabusSubmitted: 'Đã nộp đề cương',
  SyllabusRevisionRequired: 'Cần sửa đề cương',
  SyllabusApproved: 'Đã duyệt đề cương',
  InProgress: 'Đang thực hiện',
  MidtermSubmitted: 'Đã nộp báo cáo giữa kỳ',
  Stopped: 'Đã dừng thực hiện',
  FinalReportSubmitted: 'Đã nộp báo cáo cuối kỳ',
  ReadyForReviewerAssignment: 'Chờ phân công phản biện',
  Defended: 'Đã bảo vệ',
  ScoreLocked: 'Đã khóa điểm',
  Completed: 'Đã hoàn thành',
} as const;
export type GroupStatus = keyof typeof GROUP_STATUS_LABELS;
export type GroupScope = 'active' | 'all' | 'completed' | 'stopped';
export interface AdvisingFilters {
  batchId: string;
  keyword: string;
  scope: GroupScope;
  status: GroupStatus | '';
  confirmation: '' | 'pending' | 'expired';
}
export interface AdvisingStudent {
  studentId: string;
  userCode: string | null;
  fullName: string | null;
  confirmationStatus: string;
  joinedAt: string;
  confirmedAtUtc: string | null;
  confirmationExpiresAtUtc: string | null;
}
export interface AdvisingGroup {
  groupId: string;
  topicId: string;
  titleVi: string;
  titleEn: string | null;
  batchId: string;
  batchName: string | null;
  status: GroupStatus;
  createdAt: string;
  confirmedStudentsCount: number;
  pendingConfirmationsCount: number;
  expiredInvitationsCount: number;
  students: AdvisingStudent[];
}
export interface AdvisingGroupsResult {
  page: PagedResult<AdvisingGroup>;
  statistics: {
    total: number;
    active: number;
    completed: number;
    stopped: number;
    activeConfirmedStudents: number;
    pendingInvitations: number;
    expiredInvitations: number;
  };
}
export interface AdvisingGroupDetail extends StudentGroupDetailDto {
  description: string | null;
  requirements: string | null;
  studentNumber: number | null;
  createdAt: string;
}
