export type ScoreCalculationMethod = 'Percentage' | 'IntegerCoefficient';

export type ScoreComponentSource =
  | 'Advisor'
  | 'Reviewer'
  | 'CouncilChairman'
  | 'CouncilSecretary'
  | 'CouncilMember'
  | 'CouncilAverage'
  | 'Custom';

export type ScoreComponentApplicability =
  | 'Always'
  | 'ReviewerInCouncil'
  | 'ReviewerOutsideCouncil';

export type ScoreConfigurationStatus = 'Draft' | 'Active' | 'Retired';

export interface ScoreComponentInput {
  componentCode: string;
  componentName: string;
  source: ScoreComponentSource;
  applicability: ScoreComponentApplicability;
  weight: number;
  isRequired: boolean;
  minimumScore: number | null;
  requiresApproval: boolean;
  orderIndex: number;
}

export interface ScoreComponentDto {
  id: string;
  componentCode: string;
  componentName: string;
  source: ScoreComponentSource;
  applicability: ScoreComponentApplicability;
  weight: number;
  isRequired: boolean;
  minimumScore: number | null;
  requiresApproval: boolean;
  orderIndex: number;
}

export interface ScoreConfigurationDto {
  id: string;
  batchId: string;
  versionNo: number;
  status: ScoreConfigurationStatus;
  calculationMethod: ScoreCalculationMethod;
  changeReason?: string | null;
  createdBy?: string | null;
  createdAt: string;
  updatedAt?: string | null;
  publishedBy?: string | null;
  publishedAt?: string | null;
  retiredAt?: string | null;
  components: ScoreComponentDto[];
}

export interface CreateScoreConfigurationDraftRequest {
  calculationMethod?: ScoreCalculationMethod;
  components?: ScoreComponentInput[];
  changeReason?: string | null;
}

export interface UpdateScoreConfigurationDraftRequest {
  calculationMethod: ScoreCalculationMethod;
  components: ScoreComponentInput[];
  changeReason?: string | null;
}

export interface StageDto {
  id: string;
  orderIndex: number;
  stageCode: string;
  stageName: string;
  startDate: string;
  endDate: string;
  deadlineAtUtc: string;
  isOverridden: boolean;
}

export interface KltnBatchSummaryDto {
  id: string;
  batchName: string;
  academicYear: string;
  semester: number;
  startDate: string;
  endDate: string;
  timeZoneId: string;
  status: 'Draft' | 'Open' | 'Closed' | 'Archived' | string;
  createdAt: string;
  updatedAt?: string | null;
}

export interface KltnBatchDetailDto {
  id: string;
  batchName: string;
  academicYear: string;
  semester: number;
  startDate: string;
  endDate: string;
  timeZoneId: string;
  scheduleTemplateVersionId?: string | null;
  status: 'Draft' | 'Open' | 'Closed' | 'Archived' | string;
  createdAt: string;
  updatedAt?: string | null;
  stages: StageDto[];
}

export const CALCULATION_METHOD_LABELS: Record<ScoreCalculationMethod, string> = {
  IntegerCoefficient: 'Hệ số nguyên',
  Percentage: 'Tỷ lệ phần trăm',
};

export const COMPONENT_SOURCE_LABELS: Record<ScoreComponentSource, string> = {
  Advisor: 'GVHD',
  Reviewer: 'GVPB',
  CouncilChairman: 'Chủ tịch HĐ',
  CouncilSecretary: 'Thư ký HĐ',
  CouncilMember: 'Ủy viên HĐ',
  CouncilAverage: 'Điểm TB Hội đồng',
  Custom: 'Đầu điểm tùy chọn',
};

export const APPLICABILITY_LABELS: Record<ScoreComponentApplicability, string> = {
  Always: 'Luôn áp dụng',
  ReviewerInCouncil: 'GVPB kiêm UV trong HĐ',
  ReviewerOutsideCouncil: 'GVPB ngoài HĐ (không chấm UV)',
};

export const CONFIG_STATUS_LABELS: Record<
  ScoreConfigurationStatus,
  { label: string; badgeVariant: 'default' | 'success' | 'warning' | 'secondary' }
> = {
  Draft: { label: 'Bản nháp', badgeVariant: 'warning' },
  Active: { label: 'Đang áp dụng', badgeVariant: 'success' },
  Retired: { label: 'Đã lưu trữ', badgeVariant: 'secondary' },
};

export const UIT_INTEGER_PRESET_COMPONENTS: ScoreComponentInput[] = [
  {
    componentCode: 'ADVISOR',
    componentName: 'Điểm GVHD',
    source: 'Advisor',
    applicability: 'Always',
    weight: 2,
    isRequired: true,
    minimumScore: null,
    requiresApproval: false,
    orderIndex: 1,
  },
  {
    componentCode: 'REVIEWER',
    componentName: 'Điểm GVPB',
    source: 'Reviewer',
    applicability: 'Always',
    weight: 2,
    isRequired: true,
    minimumScore: 5.0,
    requiresApproval: true,
    orderIndex: 2,
  },
  {
    componentCode: 'COUNCIL_CHAIRMAN',
    componentName: 'Chủ tịch hội đồng',
    source: 'CouncilChairman',
    applicability: 'Always',
    weight: 1,
    isRequired: true,
    minimumScore: null,
    requiresApproval: false,
    orderIndex: 3,
  },
  {
    componentCode: 'COUNCIL_SECRETARY',
    componentName: 'Thư ký hội đồng',
    source: 'CouncilSecretary',
    applicability: 'Always',
    weight: 1,
    isRequired: true,
    minimumScore: null,
    requiresApproval: false,
    orderIndex: 4,
  },
  {
    componentCode: 'COUNCIL_MEMBER',
    componentName: 'Ủy viên hội đồng',
    source: 'CouncilMember',
    applicability: 'ReviewerOutsideCouncil',
    weight: 1,
    isRequired: false,
    minimumScore: null,
    requiresApproval: false,
    orderIndex: 5,
  },
];

export const UIT_PERCENTAGE_PRESET_COMPONENTS: ScoreComponentInput[] = [
  {
    componentCode: 'ADVISOR',
    componentName: 'Điểm GVHD',
    source: 'Advisor',
    applicability: 'Always',
    weight: 40,
    isRequired: true,
    minimumScore: null,
    requiresApproval: false,
    orderIndex: 1,
  },
  {
    componentCode: 'REVIEWER',
    componentName: 'Điểm GVPB',
    source: 'Reviewer',
    applicability: 'Always',
    weight: 30,
    isRequired: true,
    minimumScore: 5.0,
    requiresApproval: true,
    orderIndex: 2,
  },
  {
    componentCode: 'COUNCIL_AVG',
    componentName: 'Điểm hội đồng',
    source: 'CouncilAverage',
    applicability: 'Always',
    weight: 30,
    isRequired: true,
    minimumScore: null,
    requiresApproval: false,
    orderIndex: 3,
  },
];

