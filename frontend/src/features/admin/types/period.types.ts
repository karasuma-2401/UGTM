import type {
  KltnBatchSummaryDto,
  KltnBatchDetailDto,
  StageDto,
} from './scoreConfig.types';

export type { KltnBatchSummaryDto, KltnBatchDetailDto, StageDto };

export type ReviewGateType = 'Syllabus' | 'Midterm';

export interface ReviewGateConfigurationDto {
  id: string;
  batchId: string;
  gateType: ReviewGateType | string;
  initialSubmissionDeadlineUtc: string;
  facultyResponseDeadlineUtc: string;
  finalClosureDeadlineUtc: string;
  createdBy: string;
  createdAtUtc: string;
  updatedBy: string;
  updatedAtUtc: string;
}

export interface UpsertReviewGateConfigurationRequest {
  initialSubmissionDeadlineUtc: string;
  facultyResponseDeadlineUtc: string;
  finalClosureDeadlineUtc: string;
}

export interface CreateKltnBatchRequest {
  batchName: string;
  academicYear: string;
  semester: number;
  startDate: string;
  templateCode?: string;
}

export interface UpdateStageScheduleItem {
  orderIndex: number;
  stageCode: string;
  stageName: string;
  startDate: string;
  endDate: string;
  deadlineAtUtc: string;
}

export interface UpdateKltnBatchScheduleRequest {
  stages: UpdateStageScheduleItem[];
  changeReason?: string | null;
}

export interface StandardStageMetadata {
  orderIndex: number;
  stageCode: string;
  stageName: string;
  shortName: string;
  durationLabel: string;
  actor: string;
  startOffsetDays: number;
  endOffsetDays: number;
}

export const STANDARD_7_STAGES: readonly StandardStageMetadata[] = [
  {
    orderIndex: 1,
    stageCode: 'TOPIC_PREPARATION',
    stageName: 'Chuẩn bị và công bố đề tài',
    shortName: 'Đề xuất',
    durationLabel: 'Tuần 0–1',
    actor: 'GVHD / Khoa',
    startOffsetDays: 0,
    endOffsetDays: 6,
  },
  {
    orderIndex: 2,
    stageCode: 'REGISTRATION_AND_SYLLABUS',
    stageName: 'Đăng ký và đề cương',
    shortName: 'Đăng ký',
    durationLabel: 'Tuần 2',
    actor: 'SV / Khoa',
    startOffsetDays: 7,
    endOffsetDays: 13,
  },
  {
    orderIndex: 3,
    stageCode: 'PROGRESS_EXECUTION',
    stageName: 'Thực hiện và theo dõi tiến độ',
    shortName: 'Thực hiện',
    durationLabel: 'Tuần 3–10',
    actor: 'SV / GVHD',
    startOffsetDays: 14,
    endOffsetDays: 62,
  },
  {
    orderIndex: 4,
    stageCode: 'MIDTERM_GATE',
    stageName: 'Đánh giá giữa kỳ',
    shortName: 'Giữa kỳ',
    durationLabel: 'Tuần 10',
    actor: 'GVHD / Khoa',
    startOffsetDays: 63,
    endOffsetDays: 69,
  },
  {
    orderIndex: 5,
    stageCode: 'FINAL_EVALUATION',
    stageName: 'Báo cáo cuối kỳ và phản biện',
    shortName: 'Phản biện',
    durationLabel: 'Tuần 11–18',
    actor: 'GVHD / GVPB',
    startOffsetDays: 70,
    endOffsetDays: 125,
  },
  {
    orderIndex: 6,
    stageCode: 'DEFENSE',
    stageName: 'Hội đồng và bảo vệ',
    shortName: 'Bảo vệ',
    durationLabel: 'Tuần 19–21',
    actor: 'Hội đồng',
    startOffsetDays: 126,
    endOffsetDays: 139,
  },
  {
    orderIndex: 7,
    stageCode: 'ARCHIVAL_AND_PUBLICATION',
    stageName: 'Lưu chiểu và công bố kết quả',
    shortName: 'Công bố',
    durationLabel: 'Tuần 21',
    actor: 'Khoa / PĐT',
    startOffsetDays: 140,
    endOffsetDays: 146,
  },
];

