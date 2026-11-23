import type { AcademicBatchTimeline } from '@/shared/academic-periods/types';

export type SubmissionGateStageKind = 'syllabus' | 'midterm' | 'final-report';

export interface SubmissionStageWindowResult {
  state: 'open' | 'closed' | 'unknown';
  reason:
    | 'open'
    | 'batch_closed'
    | 'before_start'
    | 'after_deadline'
    | 'missing_stage'
    | 'unknown';
  message: string | null;
  stageName?: string;
  startDate?: string;
  deadlineAtUtc?: string;
}

const STAGE_CONFIG: Record<
  SubmissionGateStageKind,
  { stageCode: string; orderIndex: number; label: string }
> = {
  syllabus: {
    stageCode: 'REGISTRATION_AND_SYLLABUS',
    orderIndex: 2,
    label: 'Đề cương chi tiết (Giai đoạn 2)',
  },
  midterm: {
    stageCode: 'MIDTERM_GATE',
    orderIndex: 4,
    label: 'Báo cáo giữa kỳ (Giai đoạn 4)',
  },
  'final-report': {
    stageCode: 'FINAL_EVALUATION',
    orderIndex: 5,
    label: 'Báo cáo cuối kỳ (Giai đoạn 5)',
  },
};

function formatLocalDate(dateOnly: string): string {
  const parts = dateOnly.split('-');
  if (parts.length !== 3) return dateOnly;
  return `${parts[2]}/${parts[1]}/${parts[0]}`;
}

function formatDeadline(isoUtc: string, timeZone = 'Asia/Ho_Chi_Minh'): string {
  if (!Number.isFinite(Date.parse(isoUtc))) return isoUtc;
  try {
    return new Intl.DateTimeFormat('vi-VN', {
      dateStyle: 'short',
      timeStyle: 'short',
      timeZone,
    }).format(new Date(isoUtc));
  } catch {
    return new Intl.DateTimeFormat('vi-VN', {
      dateStyle: 'short',
      timeStyle: 'short',
      timeZone: 'Asia/Ho_Chi_Minh',
    }).format(new Date(isoUtc));
  }
}

export function evaluateSubmissionStageWindow(
  timeline: AcademicBatchTimeline | undefined,
  kind: SubmissionGateStageKind,
  now = Date.now(),
): SubmissionStageWindowResult {
  if (!timeline) {
    return {
      state: 'unknown',
      reason: 'unknown',
      message: null,
    };
  }

  const config = STAGE_CONFIG[kind];

  if (timeline.status && timeline.status !== 'Open') {
    return {
      state: 'closed',
      reason: 'batch_closed',
      message: `Đợt khóa luận hiện không ở trạng thái mở. Cổng nộp ${config.label} đang tạm khóa.`,
    };
  }

  const stage = timeline.stages.find(
    (item) => item.stageCode === config.stageCode || item.orderIndex === config.orderIndex,
  );
  if (!stage) {
    return {
      state: 'closed',
      reason: 'missing_stage',
      message: `Đợt khóa luận chưa có cấu hình lịch cho ${config.label}.`,
    };
  }

  const deadlineMs = Date.parse(stage.deadlineAtUtc);
  if (!Number.isFinite(deadlineMs)) {
    return {
      state: 'unknown',
      reason: 'unknown',
      message: null,
      stageName: stage.stageName,
      startDate: stage.startDate,
      deadlineAtUtc: stage.deadlineAtUtc,
    };
  }

  if (timeline.currentLocalDate < stage.startDate) {
    return {
      state: 'closed',
      reason: 'before_start',
      message: `Chưa đến giai đoạn nộp ${config.label} (mở từ ngày ${formatLocalDate(stage.startDate)}).`,
      stageName: stage.stageName,
      startDate: stage.startDate,
      deadlineAtUtc: stage.deadlineAtUtc,
    };
  }

  if (now > deadlineMs) {
    return {
      state: 'closed',
      reason: 'after_deadline',
      message: `Đã hết thời gian nộp ${config.label} (hạn chót ${formatDeadline(stage.deadlineAtUtc, timeline.timeZoneId || 'Asia/Ho_Chi_Minh')}).`,
      stageName: stage.stageName,
      startDate: stage.startDate,
      deadlineAtUtc: stage.deadlineAtUtc,
    };
  }

  return {
    state: 'open',
    reason: 'open',
    message: null,
    stageName: stage.stageName,
    startDate: stage.startDate,
    deadlineAtUtc: stage.deadlineAtUtc,
  };
}
