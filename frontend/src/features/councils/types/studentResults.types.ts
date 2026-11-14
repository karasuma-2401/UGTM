import type { StatusTone } from '@/components/ui/status-badge';

export interface StudentScoreComponentDto {
  componentName: string;
  score: number;
  weight: number;
}

export interface StudentScoreSummaryDto {
  groupId: string;
  topicTitle: string;
  status: 'PublishedToStudent';
  publishedAt: string;
  finalScore: number;
  calculationFormula: string;
  components: StudentScoreComponentDto[];
}

export type StudentScoreLookupResult =
  | {
      kind: 'published';
      data: StudentScoreSummaryDto;
    }
  | {
      kind: 'locked';
      code: string;
      message: string;
    };

export interface StudentFinalSubmissionDto {
  submissionId: string;
  groupId: string;
  status: string;
  currentVersionNo: number;
  submittedAt: string | null;
  advisorConfirmedAt: string | null;
  archivedConfirmedAt: string | null;
  facultyFeedback: string | null;
}

export interface StudentConditionRow {
  key: 'publication' | 'archive';
  stepLabel: string;
  detailLabel: string;
  statusLabel: string;
  statusTone: StatusTone;
  nextLabel: string;
}
