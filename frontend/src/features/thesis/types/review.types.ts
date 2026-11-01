import type { SyllabusStatus, SubmissionFileDownloadDto, ReviewDecision } from './syllabus.types';

export type { ReviewDecision };

export interface ReviewSyllabusRequest {
  decision: ReviewDecision;
  feedback?: string;
}

export interface SyllabusReviewStudentDto {
  studentId: string;
  userCode?: string | null;
  fullName?: string | null;
}

export interface SyllabusReviewItemDto {
  syllabusId: string;
  groupId: string;
  topicId: string;
  topicTitleVi: string;
  topicTitleEn?: string | null;
  batchId: string;
  batchName?: string | null;
  students: SyllabusReviewStudentDto[];
  advisor: { advisorId: string; fullName?: string | null };
  currentVersionNo: number;
  currentStatus: string;
  latestFile?: {
    id: string;
    fileName: string;
    fileSize: number;
    fileExtension: string;
    uploadedAtUtc: string;
  } | null;
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
