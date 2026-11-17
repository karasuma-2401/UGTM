import type {
  ReviewDecision,
  SubmissionFileDownloadDto,
  SubmissionFileDto,
  SubmissionReviewDto,
  SyllabusStatus,
} from './syllabus.types';

export type { ReviewDecision };

export type ReviewGateKind = 'syllabus' | 'midterm';
export type ReviewGateStatus = 'Draft' | 'Submitted' | 'NeedRevision' | 'Approved' | 'Rejected';
export type ReviewGateFilterKey = 'ALL' | 'Submitted' | 'NeedRevision' | 'Approved' | 'Rejected';
export type ReviewGateSortField =
  | 'groupCode'
  | 'topicTitle'
  | 'version'
  | 'submittedAt'
  | 'status';

export interface ReviewGateSortState {
  field: ReviewGateSortField;
  direction: 'asc' | 'desc';
}

export interface ReviewGateStats {
  all: number;
  submitted: number;
  needRevision: number;
  approved: number;
  rejected: number;
}

export interface ReviewGateStudentDto {
  studentId: string;
  userCode?: string | null;
  fullName?: string | null;
}

export interface ReviewGateItem {
  submissionId?: string;
  groupId: string;
  groupCode: string;
  topicCode: string;
  topicTitleVi: string;
  topicTitleEn?: string | null;
  batchId?: string;
  batchName: string;
  studentNames: string;
  students?: ReviewGateStudentDto[];
  advisorId?: string;
  advisorName: string;
  latestFileName: string;
  latestVersionNo: number;
  currentStatus: ReviewGateStatus;
  submittedAtUtc?: string | null;
  fileId?: string | null;
  fileSize?: number | null;
  fileExtension?: string | null;
}

export interface ReviewGateHistoryVersionItem {
  versionNo: number;
  file: SubmissionFileDto;
  review?: SubmissionReviewDto | null;
}

export interface ReviewGateHistoryDto {
  groupId: string;
  currentVersionNo: number;
  currentStatus: ReviewGateStatus;
  versions: ReviewGateHistoryVersionItem[];
}

export interface ReviewSyllabusRequest {
  decision: ReviewDecision;
  feedback?: string;
}

export type SyllabusReviewStudentDto = ReviewGateStudentDto;

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

export interface ReviewGroupItemDto extends ReviewGateItem {
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
