import type {
  SubmissionFileDto,
  SubmissionReviewDto,
} from './syllabus.types';

export type MidtermReportStatus =
  | 'Draft'
  | 'Submitted'
  | 'NeedRevision'
  | 'Approved'
  | 'Rejected';

export const MIDTERM_STATUS_LABELS: Record<MidtermReportStatus, string> = {
  Draft: 'Bản nháp',
  Submitted: 'Chờ thẩm định',
  NeedRevision: 'Yêu cầu chỉnh sửa',
  Approved: 'Đạt',
  Rejected: 'Không đạt',
};

export interface MidtermReportSubmissionDto {
  midtermReportId: string;
  groupId: string;
  versionNo: number;
  status: MidtermReportStatus;
  file: SubmissionFileDto;
}

export interface MidtermReportSubmissionHistoryItemDto {
  versionNo: number;
  file: SubmissionFileDto;
  review?: SubmissionReviewDto | null;
}

export interface MidtermReportSubmissionHistoryDto {
  midtermReportId: string;
  groupId: string;
  currentVersionNo: number;
  currentStatus: MidtermReportStatus;
  versions: MidtermReportSubmissionHistoryItemDto[];
}

export interface MidtermReportReviewStudentDto {
  studentId: string;
  userCode?: string | null;
  fullName?: string | null;
}

export interface MidtermReportReviewItemDto {
  midtermReportId: string;
  groupId: string;
  topicId: string;
  topicTitleVi: string;
  topicTitleEn?: string | null;
  batchId: string;
  batchName?: string | null;
  students: MidtermReportReviewStudentDto[];
  advisor: { advisorId: string; fullName?: string | null };
  currentVersionNo: number;
  currentStatus: string;
  latestFile?: SubmissionFileDto | null;
}

export interface ReviewMidtermGroupItemDto {
  groupId: string;
  groupCode: string;
  topicCode: string;
  topicTitleVi: string;
  studentNames: string;
  advisorName: string;
  batchName: string;
  currentStatus: MidtermReportStatus;
  latestVersionNo: number;
  latestFileName: string;
  fileId?: string | null;
  submittedAtUtc?: string | null;
}
