import type { StatusTone } from '@/components/ui/status-badge';
import type {
  StudentConditionRow,
  StudentFinalSubmissionDto,
  StudentScoreLookupResult,
} from '../types/studentResults.types';

export function convertScoreScale10To4(score: number | null | undefined): number | null {
  if (score === null || score === undefined || Number.isNaN(score)) {
    return null;
  }
  return Math.round(((score / 10) * 4) * 100) / 100;
}

export function formatScale4Score(score: number | null | undefined): string {
  const converted = convertScoreScale10To4(score);
  if (converted === null) {
    return '—';
  }
  return converted.toFixed(2);
}

/**
 * Computes a reference academic classification on the 10-point scale (tham khảo).
 */
export function getScoreClassificationMeta(score: number | null | undefined): {
  label: string;
  tone: StatusTone;
} {
  if (score === null || score === undefined || Number.isNaN(score)) {
    return { label: '—', tone: 'neutral' };
  }
  if (score >= 9.0) return { label: 'Xuất sắc', tone: 'success' };
  if (score >= 8.0) return { label: 'Giỏi', tone: 'success' };
  if (score >= 7.0) return { label: 'Khá', tone: 'info' };
  if (score >= 5.0) return { label: 'Trung bình', tone: 'warning' };
  return { label: 'Chưa đạt', tone: 'error' };
}

/**
 * Computes percentage weight contribution of one score component relative to total positive weight.
 */
export function computeWeightSharePercent(weight: number, totalWeight: number): number {
  if (totalWeight <= 0 || weight <= 0 || Number.isNaN(weight) || Number.isNaN(totalWeight)) {
    return 0;
  }
  return Math.round((weight / totalWeight) * 1000) / 10;
}

export function replaceInterpunctWithHyphen(value: string | null | undefined): string {
  if (!value) {
    return '';
  }
  return value.replace(/\s*·\s*/g, ' - ');
}

function resolveArchiveRow(
  scoreResult: StudentScoreLookupResult | undefined,
  finalSubmission: StudentFinalSubmissionDto | null | undefined,
): StudentConditionRow {
  const isPublished = scoreResult?.kind === 'published';
  if (isPublished || finalSubmission?.status === 'ArchiveConfirmed') {
    return {
      key: 'archive',
      stepLabel: 'Khoa xác nhận lưu chiểu',
      detailLabel: 'Hồ sơ lưu chiểu sau bảo vệ đã được Khoa nghiệm thu hoàn tất.',
      statusLabel: 'Đã nghiệm thu',
      statusTone: 'success',
      nextLabel: 'Hoàn tất',
      done: true,
      actionLabel: 'Xem hồ sơ lưu chiểu',
      actionTo: '/thesis/archival',
    };
  }

  switch (finalSubmission?.status) {
    case 'AdvisorConfirmed':
      return {
        key: 'archive',
        stepLabel: 'Khoa xác nhận lưu chiểu',
        detailLabel: 'GVHD đã xác nhận bản chỉnh sửa, đang chờ Khoa nghiệm thu.',
        statusLabel: 'Chờ Khoa duyệt',
        statusTone: 'info',
        nextLabel: 'Chờ Khoa nghiệm thu',
        done: false,
        actionLabel: 'Theo dõi hồ sơ lưu chiểu',
        actionTo: '/thesis/archival',
      };
    case 'Submitted':
      return {
        key: 'archive',
        stepLabel: 'Khoa xác nhận lưu chiểu',
        detailLabel: 'Nhóm đã nộp hồ sơ lưu chiểu, đang chờ GVHD xác nhận.',
        statusLabel: 'Chờ GVHD duyệt',
        statusTone: 'info',
        nextLabel: 'Chờ GVHD xác nhận',
        done: false,
        actionLabel: 'Theo dõi hồ sơ lưu chiểu',
        actionTo: '/thesis/archival',
      };
    case 'NeedRevision':
      return {
        key: 'archive',
        stepLabel: 'Khoa xác nhận lưu chiểu',
        detailLabel: 'Hồ sơ lưu chiểu đang có yêu cầu chỉnh sửa và nộp lại.',
        statusLabel: 'Cần bổ sung',
        statusTone: 'error',
        nextLabel: 'Cập nhật hồ sơ lưu chiểu',
        done: false,
        actionLabel: 'Cập nhật hồ sơ lưu chiểu',
        actionTo: '/thesis/archival',
      };
    default:
      return {
        key: 'archive',
        stepLabel: 'Khoa xác nhận lưu chiểu',
        detailLabel: 'Nhóm cần hoàn tất nộp PDF/DOCX và được Khoa nghiệm thu lưu chiểu.',
        statusLabel: 'Chưa hoàn thành',
        statusTone: 'warning',
        nextLabel: 'Nộp hồ sơ lưu chiểu',
        done: false,
        actionLabel: 'Đến trang Hồ sơ lưu chiểu',
        actionTo: '/thesis/archival',
      };
  }
}

export function buildStudentConditionRows(
  scoreResult: StudentScoreLookupResult | undefined,
  finalSubmission: StudentFinalSubmissionDto | null | undefined,
): StudentConditionRow[] {
  const isFacultyPublished =
    scoreResult?.kind === 'published' ||
    (scoreResult?.kind === 'locked' && scoreResult.code === 'ScoreSummary.ArchivalNotConfirmed');

  const archiveRow = resolveArchiveRow(scoreResult, finalSubmission);

  const publicationRow: StudentConditionRow = isFacultyPublished
    ? {
        key: 'publication',
        stepLabel: 'Khoa công bố kết quả',
        detailLabel:
          scoreResult?.kind === 'published'
            ? 'Bảng điểm chính thức của đợt đã được Khoa công bố cho sinh viên.'
            : 'Khoa đã sẵn sàng công bố điểm ngay khi nhóm hoàn tất nghiệm thu lưu chiểu.',
        statusLabel: 'Đã công bố',
        statusTone: 'success',
        nextLabel: 'Hoàn tất',
        done: true,
      }
    : {
        key: 'publication',
        stepLabel: 'Khoa công bố kết quả',
        detailLabel: 'Khoa tổng hợp đầy đủ các đầu điểm sau bảo vệ và thực hiện công bố theo đợt.',
        statusLabel: 'Chờ công bố',
        statusTone: 'warning',
        nextLabel: 'Chờ Khoa công bố',
        done: false,
        actionLabel: 'Xem góp ý & tiến độ',
        actionTo: '/thesis/final-report',
      };

  return [archiveRow, publicationRow];
}
