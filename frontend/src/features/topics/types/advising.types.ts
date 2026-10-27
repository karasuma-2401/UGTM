import type { Topic, TopicGroup } from './topic.types';
export type AdvisingTab = 'topics' | 'registrations' | 'advising';

// Match the backend tracking DTOs. JoinedAt is not email confirmation.
export type { TopicStudent as TrackingStudent, TopicGroup as TrackingGroup } from './topic.types';

export interface TrackingTopic extends Topic {
  confirmedStudentsCount?: number;
  pendingConfirmationsCount?: number;
  registeredStudentsCount: number;
  remainingSlots: number;
  registrationStatus: 'Empty' | 'Registered' | 'Full';
  hasRegistration: boolean;
  isAvailable: boolean;
  group: TopicGroup | null;
}

export const GROUP_STATUS_LABELS: Record<string, string> = {
  Registered: 'Đã ghi nhận nhóm',
  SyllabusSubmitted: 'Đã nộp đề cương',
  SyllabusRevisionRequired: 'Cần sửa đề cương',
  SyllabusApproved: 'Đã duyệt đề cương',
  InProgress: 'Đang thực hiện',
  MidtermSubmitted: 'Đã nộp báo cáo giữa kỳ',
  Stopped: 'Đã dừng thực hiện',
  FinalReportSubmitted: 'Đã nộp báo cáo cuối kỳ',
  Defended: 'Đã bảo vệ',
  ScoreLocked: 'Đã khóa điểm',
  Completed: 'Đã hoàn thành',
};
