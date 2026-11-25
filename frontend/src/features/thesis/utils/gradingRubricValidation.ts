import type { GradingCriterionDto, GradingRubricDto } from '../services/lecturerGradingApi';

const GUID = /^(?!00000000-0000-0000-0000-000000000000$)[\da-f]{8}-[\da-f]{4}-[\da-f]{4}-[\da-f]{4}-[\da-f]{12}$/i;
export const isGradingCriterionId = (id: string): boolean => GUID.test(id);

const STANDARD_MAX_SCORES: Record<string, number> = {
  '1.1': 1.5, '1.2': 1.5,
  '2.1': 1, '2.2': 1, '2.3': 1,
  '3.1': 1, '3.2': 1, '3.3': 1,
  '4.1': 0.5, '4.2': 0.5,
};

export function requireGradingRubric(
  rubric: GradingRubricDto | null,
  batchId: string,
): GradingCriterionDto[] {
  if (!rubric || !rubric.criteria?.length) {
    throw new Error('Chưa tải được barem chấm điểm chuẩn. Vui lòng thử tải lại.');
  }
  const ids = rubric.criteria.map((criterion) => String(criterion.id ?? '').toLowerCase());
  if (
    rubric.batchId?.toLowerCase() !== batchId.toLowerCase() ||
    ids.some((id) => !GUID.test(id)) ||
    new Set(ids).size !== ids.length
  ) {
    throw new Error('Dữ liệu barem không hợp lệ hoặc không thuộc đợt đang chọn. Vui lòng tải lại.');
  }
  if (
    rubric.criteria.length !== Object.keys(STANDARD_MAX_SCORES).length ||
    Object.entries(STANDARD_MAX_SCORES).some(([code, max]) =>
      rubric.criteria.filter((criterion) => criterion.code === code && criterion.maxScore === max).length !== 1)
  ) {
    throw new Error('Dữ liệu tiêu chí không khớp barem chấm điểm chuẩn. Vui lòng liên hệ quản trị viên để kiểm tra dữ liệu.');
  }
  return rubric.criteria;
}
