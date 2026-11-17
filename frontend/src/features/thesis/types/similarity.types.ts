export interface SimilaritySummary {
  similarityCoverage: number;
  maxRerankerScore: number;
  scannedChunks: number;
  scannedCharacters: number;
  matchedCharacters: number;
  citedFindings: number;
  uncitedFindings: number;
  suspectedFindings: number | null;
}

export interface SimilarityFinding {
  passageGroupId: string | null;
  submissionChunkNo: number | null;
  submissionPage: number | null;
  submissionText: string;
  sourceKey: string | null;
  sourceFilename: string | null;
  sourceVersion: number | null;
  sourcePage: number | null;
  sourceText: string;
  rerankerScore: number | null;
  classifierScore: number | null;
  citationStatus: string | null;
  citationExplanation: string | null;
  potentialMisattribution: boolean;
  reviewRequired: boolean;
}

interface SimilarityBase {
  scanId: string;
  finalReportId: string;
  versionNo: number;
  status: string;
  statusDescription: string;
  completedAt: string | null;
}

export interface SimilarityReceipt extends SimilarityBase {
  fileName: string;
  checksumSha256: string | null;
  fileSize: number;
  submittedAt: string;
  notice: string;
}

export interface SimilarityReport extends SimilarityBase {
  createdAt: string;
  summary: SimilaritySummary | null;
  findings: SimilarityFinding[];
  disclaimer: string | null;
  canRescan: boolean;
}

export type SimilarityResult = SimilarityReceipt | SimilarityReport;

export interface AssignedCouncilStudent {
  id: string;
  fullName: string;
  userCode: string | null;
}

export interface AssignedCouncilReport {
  groupId: string;
  finalReportId: string;
  versionNo: number;
  topicTitle: string;
  topicTitleEn?: string | null;
  batchId?: string | null;
  students?: AssignedCouncilStudent[] | null;
  submittedAt?: string | null;
  scanStatus?: string | null;
  similarityCoverage?: number | null;
  maxRerankerScore?: number | null;
  citedFindings?: number;
  uncitedFindings?: number;
  suspectedFindings?: number;
  totalFindings?: number;
  scanCompletedAt?: string | null;
}
