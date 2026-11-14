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
      detailLabel: 'Đã hoàn thành',
      statusLabel: 'Đạt',
      statusTone: 'success',
      nextLabel: '—',
    };
  }

  switch (finalSubmission?.status) {
    case 'AdvisorConfirmed':
      return {
        key: 'archive',
        stepLabel: 'Khoa xác nhận lưu chiểu',
        detailLabel: 'GVHD đã duyệt, chờ Khoa',
        statusLabel: 'Chờ Khoa duyệt',
        statusTone: 'info',
        nextLabel: '—',
      };
    case 'Submitted':
      return {
        key: 'archive',
        stepLabel: 'Khoa xác nhận lưu chiểu',
        detailLabel: 'Đã nộp, chờ kiểm tra',
        statusLabel: 'Đang kiểm tra',
        statusTone: 'info',
        nextLabel: '—',
      };
    case 'NeedRevision':
      return {
        key: 'archive',
        stepLabel: 'Khoa xác nhận lưu chiểu',
        detailLabel: 'Cần chỉnh sửa hồ sơ',
        statusLabel: 'Cần bổ sung',
        statusTone: 'warning',
        nextLabel: '—',
      };
    default:
      return {
        key: 'archive',
        stepLabel: 'Khoa xác nhận lưu chiểu',
        detailLabel: 'Chưa hoàn thành',
        statusLabel: 'Điểm còn khóa',
        statusTone: 'warning',
        nextLabel: '—',
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

  const publicationRow: StudentConditionRow = isFacultyPublished
    ? {
        key: 'publication',
        stepLabel: 'Khoa công bố',
        detailLabel: 'Đã công bố',
        statusLabel: 'Đạt',
        statusTone: 'success',
        nextLabel: '—',
      }
    : {
        key: 'publication',
        stepLabel: 'Khoa công bố',
        detailLabel: 'Chưa công bố',
        statusLabel: 'Chờ',
        statusTone: 'warning',
        nextLabel: '—',
      };

  return [publicationRow, resolveArchiveRow(scoreResult, finalSubmission)];
}
