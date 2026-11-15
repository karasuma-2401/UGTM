import type {
  CriterionScoreInputDto,
  GradingCriterionDto,
  StudentCriterionScoresInputDto,
  StudentEvaluationScoreDto,
} from '../services/lecturerGradingApi';

export interface RubricCriterionView {
  id: string;
  code: string;
  name: string;
  max: number;
  elo: string;
}

export interface RubricSectionView {
  code: string;
  name: string;
  order: number;
  max: number;
  criteria: RubricCriterionView[];
}

export const DEFAULT_UIT_SECTIONS: RubricSectionView[] = [
  {
    code: '1',
    name: 'Ý nghĩa khoa học, giá trị thực tiễn',
    order: 1,
    max: 3,
    criteria: [
      { id: '1.1', code: '1.1', name: 'Tính mới và độ phức tạp', max: 1.5, elo: 'LO2' },
      { id: '1.2', code: '1.2', name: 'Tính thực tiễn và đóng góp', max: 1.5, elo: 'LO3' },
    ],
  },
  {
    code: '2',
    name: 'Vận dụng kiến thức nền tảng và chuyên sâu ngành KTPM',
    order: 2,
    max: 3,
    criteria: [
      {
        id: '2.1',
        code: '2.1',
        name: 'Khả năng tìm hiểu, phân tích yêu cầu và thiết kế hệ thống',
        max: 1,
        elo: 'LO3',
      },
      {
        id: '2.2',
        code: '2.2',
        name: 'Khả năng hiện thực hóa, kiểm thử và vận hành hệ thống, đánh giá giải pháp',
        max: 1,
        elo: 'LO4',
      },
      {
        id: '2.3',
        code: '2.3',
        name: 'Học hỏi và cập nhật kiến thức, công nghệ mới, khả năng học tập suốt đời',
        max: 1,
        elo: 'LO3',
      },
    ],
  },
  {
    code: '3',
    name: 'Trình bày kết quả',
    order: 3,
    max: 3,
    criteria: [
      {
        id: '3.1',
        code: '3.1',
        name: 'Nội dung trình bày cuốn báo và sản phẩm (viết)',
        max: 1,
        elo: 'LO6, LO8',
      },
      { id: '3.2', code: '3.2', name: 'Kỹ năng trình bày (nói)', max: 1, elo: 'LO6' },
      { id: '3.3', code: '3.3', name: 'Trả lời các câu hỏi', max: 1, elo: 'LO8' },
    ],
  },
  {
    code: '4',
    name: 'Kỹ năng mềm',
    order: 4,
    max: 1,
    criteria: [
      {
        id: '4.1',
        code: '4.1',
        name: 'Lập kế hoạch và quản lý thời gian, công việc của cá nhân hoặc đội nhóm',
        max: 0.5,
        elo: 'LO5, LO7',
      },
      {
        id: '4.2',
        code: '4.2',
        name: 'Kỹ năng tìm hiểu, đọc hiểu tài liệu ngoại ngữ và khả năng tư duy, giải quyết vấn đề',
        max: 0.5,
        elo: 'LO4, LO6',
      },
    ],
  },
];

const DEFAULT_SECTION_BY_PREFIX: Record<string, { name: string; order: number }> = {
  '1': { name: 'Ý nghĩa khoa học, giá trị thực tiễn', order: 1 },
  '2': { name: 'Vận dụng kiến thức nền tảng và chuyên sâu ngành KTPM', order: 2 },
  '3': { name: 'Trình bày kết quả', order: 3 },
  '4': { name: 'Kỹ năng mềm', order: 4 },
};

const DEFAULT_ELO_BY_CODE: Record<string, string> = {
  '1.1': 'LO2',
  '1.2': 'LO3',
  '2.1': 'LO3',
  '2.2': 'LO4',
  '2.3': 'LO3',
  '3.1': 'LO6, LO8',
  '3.2': 'LO6',
  '3.3': 'LO8',
  '4.1': 'LO5, LO7',
  '4.2': 'LO4, LO6',
};

export function buildRubricSections(
  apiCriteria: GradingCriterionDto[] | null | undefined,
): RubricSectionView[] {
  if (!apiCriteria || apiCriteria.length === 0) {
    return DEFAULT_UIT_SECTIONS;
  }

  const sectionMap = new Map<string, RubricSectionView>();
  const sortedCriteria = [...apiCriteria].sort((a, b) => a.order - b.order);

  for (const item of sortedCriteria) {
    const prefix = item.code.split('.')[0] ?? '1';
    const fallbackSection = DEFAULT_SECTION_BY_PREFIX[prefix];
    const sectionCode = item.sectionCode?.trim() || prefix;
    const sectionName =
      item.sectionName?.trim() || fallbackSection?.name || 'Tiêu chí đánh giá khóa luận';
    const sectionOrder =
      item.sectionOrder && item.sectionOrder > 0
        ? item.sectionOrder
        : (fallbackSection?.order ?? 1);

    let section = sectionMap.get(sectionCode);
    if (!section) {
      section = {
        code: sectionCode,
        name: sectionName,
        order: sectionOrder,
        max: 0,
        criteria: [],
      };
      sectionMap.set(sectionCode, section);
    }

    section.max = Math.round((section.max + item.maxScore) * 100) / 100;
    section.criteria.push({
      id: item.id,
      code: item.code,
      name: item.name,
      max: item.maxScore,
      elo: item.elo?.trim() || DEFAULT_ELO_BY_CODE[item.code] || 'ELO',
    });
  }

  return Array.from(sectionMap.values()).sort((a, b) => a.order - b.order);
}

export function getStudentInitials(fullName: string): string {
  const parts = fullName.trim().split(/\s+/).filter(Boolean);
  if (parts.length === 0) return 'SV';
  if (parts.length === 1) return parts[0].slice(0, 2).toUpperCase();
  return `${parts[parts.length - 2]?.[0] ?? ''}${parts[parts.length - 1]?.[0] ?? ''}`.toUpperCase();
}

export function isValidCriterionScore(value: string | undefined, max: number): boolean {
  if (!value?.trim()) return false;
  const numeric = Number(value.replace(',', '.'));
  return Number.isFinite(numeric) && numeric >= 0 && numeric <= max;
}

export function parseCriterionScore(value: string | undefined): number | null {
  if (!value?.trim()) return null;
  const numeric = Number(value.replace(',', '.'));
  return Number.isFinite(numeric) ? Math.round(numeric * 100) / 100 : null;
}

export function buildStudentScoresPayload(
  studentIds: string[],
  criteria: RubricCriterionView[],
  scoresForGroup: Record<string, Record<string, string>> | undefined,
): StudentCriterionScoresInputDto[] {
  const result: StudentCriterionScoresInputDto[] = [];

  for (const studentId of studentIds) {
    const studentMap = scoresForGroup?.[studentId] ?? {};
    const details: CriterionScoreInputDto[] = [];

    for (const criterion of criteria) {
      const rawValue = studentMap[criterion.id] ?? studentMap[criterion.code];
      if (!isValidCriterionScore(rawValue, criterion.max)) continue;
      const numeric = parseCriterionScore(rawValue);
      if (numeric === null) continue;
      details.push({
        criteriaId: criterion.id,
        score: numeric,
        studentId,
      });
    }

    if (details.length > 0) {
      result.push({
        studentId,
        details,
      });
    }
  }

  return result;
}

export function hydrateGroupScoresFromDto(
  studentIds: string[],
  criteria: RubricCriterionView[],
  studentScores?: StudentEvaluationScoreDto[] | null,
  flatDetails?: Array<{
    criteriaId: string;
    criteriaCode: string;
    score: number;
    studentId?: string | null;
  }> | null,
): Record<string, Record<string, string>> {
  const result: Record<string, Record<string, string>> = {};
  const criterionByCode = new Map(criteria.map((c) => [c.code, c.id]));
  const criterionById = new Set(criteria.map((c) => c.id));

  const resolveKey = (criteriaId: string, criteriaCode: string) => {
    if (criterionById.has(criteriaId)) return criteriaId;
    const mapped = criterionByCode.get(criteriaCode);
    return mapped ?? criteriaId;
  };

  if (studentScores && studentScores.length > 0) {
    for (const studentScore of studentScores) {
      const studentMap: Record<string, string> = {};
      for (const detail of studentScore.details ?? []) {
        const key = resolveKey(detail.criteriaId, detail.criteriaCode);
        studentMap[key] = String(detail.score);
      }
      result[studentScore.studentId] = studentMap;
    }
    return result;
  }

  if (flatDetails && flatDetails.length > 0) {
    for (const detail of flatDetails) {
      const key = resolveKey(detail.criteriaId, detail.criteriaCode);
      if (detail.studentId) {
        result[detail.studentId] = result[detail.studentId] ?? {};
        result[detail.studentId][key] = String(detail.score);
      } else {
        for (const studentId of studentIds) {
          result[studentId] = result[studentId] ?? {};
          result[studentId][key] = String(detail.score);
        }
      }
    }
  }

  return result;
}
