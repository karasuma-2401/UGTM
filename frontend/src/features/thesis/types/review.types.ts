import type { SyllabusStatus, SubmissionFileDownloadDto, ReviewDecision } from './syllabus.types';

export type { ReviewDecision };

export interface ReviewSyllabusRequest {
  decision: ReviewDecision;
  feedback?: string;
}

export interface ReviewGroupItemDto {
  groupId: string;
  groupCode: string;
  topicCode: string;
  topicTitleVi: string;
  studentNames: string;
  advisorName: string;
  batchName: string;
  latestFileName: string;
  latestVersionNo: number;
  currentStatus: SyllabusStatus;
  submittedAtUtc?: string;
  fileId?: string;
}

export interface SubmissionReviewResultDto {
  id: string;
  decision: ReviewDecision;
  feedback?: string | null;
  reviewedBy: string;
  reviewedAtUtc: string;
}

export type { SubmissionFileDownloadDto };