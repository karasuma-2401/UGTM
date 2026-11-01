export type ReviewDecision = 'Approved' | 'NeedRevision' | 'Rejected';

export type SyllabusStatus =
  | 'Draft'
  | 'Submitted'
  | 'NeedRevision'
  | 'Approved'
  | 'Rejected';

export const SYLLABUS_STATUS_LABELS: Record<SyllabusStatus, string> = {
  Draft: 'Bản nháp',
  Submitted: 'Chờ thẩm định',
  NeedRevision: 'Yêu cầu chỉnh sửa',
  Approved: 'Đạt',
  Rejected: 'Không đạt',
};

export interface SubmissionFileDto {
  id: string;
  fileName: string;
  contentType: string;
  fileSize: number;
  fileExtension: string;
  checksumSha256: string;
  uploadedBy: string;
  uploadedAtUtc: string;
}

export interface SubmissionReviewDto {
  id: string;
  versionNo: number;
  decision: ReviewDecision;
  feedback?: string | null;
  reviewedBy: string;
  reviewedAtUtc: string;
}

export interface SyllabusSubmissionDto {
  syllabusId: string;
  groupId: string;
  versionNo: number;
  status: SyllabusStatus;
  file: SubmissionFileDto;
}

export interface SyllabusSubmissionHistoryItemDto {
  versionNo: number;
  file: SubmissionFileDto;
  review?: SubmissionReviewDto | null;
}

export interface SyllabusSubmissionHistoryDto {
  syllabusId: string;
  groupId: string;
  currentVersionNo: number;
  currentStatus: SyllabusStatus;
  versions: SyllabusSubmissionHistoryItemDto[];
}

export interface SubmissionFileDownloadDto {
  fileId: string;
  downloadUrl: string;
  expiresAtUtc: string;
}

export interface StudentMemberDetail {
  studentId: string;
  userCode: string;
  fullName: string;
  email: string;
  confirmationStatus: string;
}

export interface AdvisorDetail {
  advisorId: string;
  fullName: string;
  academicTitle?: string;
  email?: string;
}

export interface StudentGroupDetailDto {
  groupId: string;
  groupCode?: string;
  topicId: string;
  topicCode?: string;
  topicTitleVi: string;
  topicTitleEn?: string;
  batchId?: string;
  batchName?: string;
  advisor?: AdvisorDetail;
  members: StudentMemberDetail[];
  groupStatus?: string;
}