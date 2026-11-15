import type { StatusTone } from '@/components/ui/status-badge';

export type FinalSubmissionStatus =
  | 'NotSubmitted'
  | 'Submitted'
  | 'NeedRevision'
  | 'AdvisorConfirmed'
  | 'ArchiveConfirmed';

export type FinalSubmissionReviewAction =
  | 'AdvisorConfirm'
  | 'FacultyConfirm'
  | 'RequestRevision';

export type ArchivalFileRole = 'ThesisPdf' | 'ThesisDocx';

export type ArchivalFilterKey =
  | 'ALL'
  | 'AdvisorConfirmed'
  | 'Submitted'
  | 'NeedRevision'
  | 'ArchiveConfirmed';

export interface FinalSubmissionFileDto {
  fileId: string;
  fileRole: ArchivalFileRole | string;
  fileName: string;
  fileSize: number;
  contentType: string;
  checksumSha256: string | null;
  uploadedAt: string;
}

export interface FinalSubmissionReviewDto {
  reviewId: string;
  action: FinalSubmissionReviewAction | string;
  stage: 'Advisor' | 'Faculty' | string;
  actorId: string;
  decidedAtUtc: string;
  reason: string | null;
  previousStatus: string;
  resultStatus: string;
}

export interface FinalSubmissionPackageDto {
  packageId: string;
  versionNo: number;
  abstractVi: string;
  abstractEn: string;
  keywordsVi: string;
  keywordsEn: string;
  submittedBy: string;
  submittedAt: string;
  files: FinalSubmissionFileDto[];
  reviews: FinalSubmissionReviewDto[];
}

export interface FinalSubmissionDto {
  submissionId: string;
  groupId: string;
  status: FinalSubmissionStatus | string;
  currentVersionNo: number;
  abstractVi: string | null;
  abstractEn: string | null;
  keywordsVi: string | null;
  keywordsEn: string | null;
  submittedBy: string | null;
  submittedAt: string | null;
  submissionVersion: number;
  advisorConfirmedBy: string | null;
  advisorConfirmedAt: string | null;
  archivedConfirmedBy: string | null;
  archivedConfirmedAt: string | null;
  facultyFeedback: string | null;
  isLegacy: boolean;
  files: FinalSubmissionFileDto[];
}

export interface FinalSubmissionEligibilityDto {
  groupId: string;
  canSubmit: boolean;
  isBatchOpen: boolean;
  isWithinSubmissionWindow: boolean;
  submissionDeadline: string | null;
  isCouncilDefenseCompleted: boolean;
  councilMinuteLockStatus: string;
  currentSubmissionStatus: FinalSubmissionStatus | string;
  submissionVersion: number | null;
  blockingReasons: string[];
}

export interface FinalSubmissionDownloadDto {
  fileId: string;
  fileName: string;
  fileRole: string;
  fileSize: number;
  downloadUrl: string;
  expiresAt: string;
}

export interface FinalSubmissionSummaryDto {
  submissionId: string;
  groupId: string;
  batchId: string;
  topicTitle: string;
  status: FinalSubmissionStatus | string;
  currentVersionNo: number;
  submissionVersion: number;
  submittedAt: string | null;
  advisorConfirmedAt: string | null;
  archivedConfirmedAt: string | null;
}

export interface FinalSubmissionPageDto {
  items: FinalSubmissionSummaryDto[];
  totalCount: number;
  pageNumber: number;
  pageSize: number;
}

export interface FinalSubmissionConfirmationDto {
  submissionId: string;
  groupId: string;
  status: string;
  currentVersionNo: number;
  submissionVersion: number;
  advisorConfirmedBy: string | null;
  advisorConfirmedAt: string | null;
  archivedConfirmedBy: string | null;
  archivedConfirmedAt: string | null;
  facultyFeedback: string | null;
  updatedAt: string;
}

export interface ArchivalMetadataInput {
  abstractVi: string;
  abstractEn: string;
  keywordsVi: string;
  keywordsEn: string;
}

export interface ArchivalReviewRow extends FinalSubmissionSummaryDto {
  groupCode: string;
  topicTitleVi: string;
  topicTitleEn: string | null;
  studentNames: string[];
  studentCodes: string[];
  advisorName: string | null;
  statusLabel: string;
  statusTone: StatusTone;
}

export interface ArchivalStats {
  all: number;
  advisorConfirmed: number;
  submitted: number;
  needRevision: number;
  archiveConfirmed: number;
}
