import type { SubmissionFileDto } from './syllabus.types';

export type ProgressReportStatus =
  | 'NotSubmitted'
  | 'Submitted'
  | 'NeedRevision'
  | 'Accepted'
  | 'Rejected';

export const PROGRESS_REPORT_STATUS_LABELS: Record<ProgressReportStatus, string> = {
  NotSubmitted: 'Chưa nộp',
  Submitted: 'Chờ đánh giá',
  NeedRevision: 'Cần chỉnh sửa',
  Accepted: 'Đạt',
  Rejected: 'Không đạt',
};

export interface ProgressMilestoneDto {
  id: string;
  groupId: string;
  title: string;
  description?: string | null;
  deadlineUtc: string;
  createdBy: string;
  createdAtUtc: string;
}

export interface CreateProgressMilestoneRequest {
  title: string;
  description?: string;
  deadlineUtc: string;
}

export interface ProgressReportReviewDto {
  versionNo: number;
  decision: string;
  feedback?: string | null;
  reviewedBy: string;
  reviewedAtUtc: string;
}

export interface ProgressReportVersionDto {
  versionNo: number;
  gitUrl: string;
  studentNote?: string | null;
  submittedBy: string;
  submittedAtUtc: string;
  isLate: boolean;
  status: string;
  file?: SubmissionFileDto | null;
  review?: ProgressReportReviewDto | null;
}

export interface ProgressReportHistoryDto {
  progressReportId: string;
  milestoneId: string;
  groupId: string;
  currentVersionNo: number;
  currentStatus: string;
  versions: ProgressReportVersionDto[];
}

export interface MilestoneWithReportDto {
  milestone: ProgressMilestoneDto;
  report?: ProgressReportHistoryDto | null;
}

export interface AdvisedGroupOption {
  groupId: string;
  groupCode: string;
  topicId: string;
  topicCode: string;
  topicTitleVi: string;
  studentNames: string;
  students: {
    studentId: string;
    fullName: string;
    userCode: string | null;
  }[];
}