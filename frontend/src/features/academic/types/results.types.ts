export type ScoreSummaryStatus = 'Pending' | 'Calculated' | 'PublishedToStudent';

export type AcademicResultStatusCode =
  | 'PUBLISHED'
  | 'READY_TO_PUBLISH'
  | 'MISSING_ARCHIVE'
  | 'MISSING_SCORE';

export type ResultFilterKey =
  | 'all'
  | 'readyToPublish'
  | 'needsCalculation'
  | 'needsAction'
  | 'published';

export type ResultSortField = 'groupCode' | 'topicTitleVi' | 'finalScore' | 'status';

export interface ScoreSummaryComponentDto {
  componentCode: string;
  componentName: string;
  source: string;
  score: number;
  weight: number;
  isIgnoredInCalculation: boolean;
  exclusionReason: string | null;
  sourceReferences: string;
}

export interface ScoreRevisionDto {
  revisionId: string;
  revisionNo: number;
  configId: string;
  configVersionNo: number;
  finalScore: number;
  finalScoreRaw: number;
  totalWeight: number;
  calculatedBy: string;
  calculatedAt: string;
  inputFingerprint: string;
  sourceSnapshot: string;
  configSnapshot: string;
}

export interface ScoreSummaryDto {
  summaryId: string;
  groupId: string;
  batchId: string;
  status: ScoreSummaryStatus;
  finalScore: number | null;
  totalWeight: number | null;
  calculationFormula: string | null;
  configVersionNo: number | null;
  calculatedAt: string | null;
  calculatedBy: string | null;
  isStale: boolean;
  summaryVersion: number;
  isLegacy: boolean;
  activeRevisionId: string | null;
  publishedRevisionId: string | null;
  publishedBy: string | null;
  publishedAt: string | null;
  components: ScoreSummaryComponentDto[];
  revisions: ScoreRevisionDto[];
  sourceSnapshot: string | null;
  configSnapshot: string | null;
}

export interface ScoreReadinessDto {
  groupId: string;
  isReadyForCalculation: boolean;
  isReadyForPublication: boolean;
  isCouncilMinuteLocked: boolean;
  hasAllRequiredScores: boolean;
  isArchiveConfirmed: boolean;
  hasActiveScoreConfig: boolean;
  summaryVersion: number | null;
  blockingReasons: string[];
}

export interface ScoreListItemDto {
  groupId: string;
  topicTitle: string;
  summaryId: string | null;
  summaryVersion: number | null;
  status: ScoreSummaryStatus;
  finalScore: number | null;
  isLegacy: boolean;
  isStale: boolean;
  isReadyForCalculation: boolean;
  isReadyForPublication: boolean;
  calculatedAt: string | null;
  publishedAt: string | null;
}

export interface ScorePageDto {
  items: ScoreListItemDto[];
  totalCount: number;
  pageNumber: number;
  pageSize: number;
}

export interface CalculateScoreSummaryRequest {
  expectedSummaryVersion: number | null;
  expectedActiveConfigVersion?: number | null;
}

export interface ScorePublicationSelection {
  groupId: string;
  expectedSummaryVersion: number;
}

export interface PublishScoreSummaryRequest {
  batchId: string;
  selections: ScorePublicationSelection[];
}

export interface ScorePublicationDto {
  batchId: string;
  publishedCount: number;
  publishedAt: string;
  publishedGroupIds: string[];
}

export interface AcademicResultListItemDto {
  groupId: string;
  groupCode: string;
  topicId: string;
  topicTitleVi: string;
  topicTitleEn: string | null;
  studentNames: string[];
  studentCodes: string[];
  facultyName: string;
  phase: string;
  statusCode: AcademicResultStatusCode;
  statusLabel: string;
  detailLabel: string;
  finalScore: number | null;
}

export interface AcademicResultStudentDto {
  studentId: string;
  studentCode: string;
  fullName: string;
  email: string;
}

export interface AcademicResultScoreSourceDto {
  sourceCode: string;
  sourceLabel: string;
  evaluatorId: string | null;
  evaluatorName: string | null;
  score: number | null;
  weight: number | null;
  isIgnored: boolean;
  status: string;
  comment: string | null;
  submittedAtUtc: string | null;
}

export interface AcademicResultMidtermDetailDto {
  status: string;
  statusLabel: string;
  currentVersionNo: number;
  facultyFeedback: string | null;
  reviewedBy: string | null;
  reviewerName: string | null;
  reviewedAtUtc: string | null;
}

export interface AcademicResultDetailDto {
  groupId: string;
  groupCode: string;
  batchId: string;
  phase: string;
  facultyName: string;
  topicId: string;
  topicCode: string | null;
  topicTitleVi: string;
  topicTitleEn: string | null;
  advisorId: string;
  advisorName: string;
  students: AcademicResultStudentDto[];
  statusCode: AcademicResultStatusCode;
  statusLabel: string;
  detailLabel: string;
  midtermDetail: AcademicResultMidtermDetailDto | null;
  scoreSources: AcademicResultScoreSourceDto[];
  advisorScore: number | null;
  reviewerScore: number | null;
  councilScore: number | null;
  finalScore: number | null;
  formulaSnapshot: string | null;
  archiveStatus: string;
  archiveStatusLabel: string;
  publicationStatus: string;
  publicationStatusLabel: string;
  publishedAtUtc: string | null;
}

export type MergedResultStatusBadgeTone =
  | 'success'
  | 'info'
  | 'warning'
  | 'error'
  | 'neutral'
  | 'orange';

export interface MergedResultRow {
  groupId: string;
  groupCode: string;
  topicId: string | null;
  topicTitleVi: string;
  topicTitleEn: string | null;
  studentNames: string[];
  studentCodes: string[];
  facultyName: string;
  summaryId: string | null;
  summaryVersion: number | null;
  summaryStatus: ScoreSummaryStatus;
  academicStatusCode: AcademicResultStatusCode | null;
  detailLabel: string;
  finalScore: number | null;
  isLegacy: boolean;
  isStale: boolean;
  isReadyForCalculation: boolean;
  isReadyForPublication: boolean;
  calculatedAt: string | null;
  publishedAt: string | null;
  filterKey: Exclude<ResultFilterKey, 'all'>;
  displayStatusLabel: string;
  displayStatusTone: MergedResultStatusBadgeTone;
  conditionSummary: string;
}

export type ResultsExportFormat = 'xlsx' | 'pdf';
