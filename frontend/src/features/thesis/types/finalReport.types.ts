import type {
  SubmissionAttachmentDto,
  SubmissionFileDownloadDto,
  SubmissionFileDto,
  SubmissionProductLinkDto,
} from './syllabus.types';

export type FinalReportStatus =
  | 'Submitted'
  | 'UnderEvaluation'
  | 'Approved'
  | 'Rejected';

export const FINAL_REPORT_STATUS_LABELS: Record<FinalReportStatus, string> = {
  Submitted: 'Đã nộp · Chờ GVHD chấm',
  UnderEvaluation: 'GVHD đang chấm',
  Approved: 'GVHD đã đồng ý chuyển phản biện',
  Rejected: 'Cần chỉnh sửa lại',
};

export interface FinalReportSubmissionDto {
  finalReportId: string;
  groupId: string;
  versionType: string;
  versionNo: number;
  status: FinalReportStatus | string;
  submittedAtUtc: string;
  file: SubmissionFileDto;
  productLinks?: SubmissionProductLinkDto[];
  attachments?: SubmissionAttachmentDto[];
}

export interface FinalReportHistoryItemDto {
  versionNo: number;
  file: SubmissionFileDto;
  plagiarismStatus?: string | null;
  similarityCoverage?: number | null;
  productLinks?: SubmissionProductLinkDto[];
  attachments?: SubmissionAttachmentDto[];
}

export interface FinalReportHistoryDto {
  finalReportId: string;
  groupId: string;
  versionType: string;
  currentVersionNo: number;
  currentStatus: FinalReportStatus | string;
  submittedAtUtc: string;
  versions: FinalReportHistoryItemDto[];
}

export interface ReviewerFeedbackDto {
  comment: string | null;
}

export type { SubmissionFileDownloadDto };