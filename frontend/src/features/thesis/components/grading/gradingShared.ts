import {
  isValidCriterionScore,
  type RubricCriterionView,
} from '../../utils/gradingRubricHelper';

export interface GradingStudentInfo {
  id: string;
  name: string;
  code: string;
  initials: string;
}

export interface StudentProgressSummary extends GradingStudentInfo {
  count: number;
  total: number;
}

export type GradingStudentScoresMap = Record<string, Record<string, string>>;

export interface StudentAccentStyle {
  dotClass: string;
  badgeClass: string;
  barClass: string;
  ringFocusClass: string;
  headerTintClass: string;
}

const STUDENT_ACCENTS: readonly StudentAccentStyle[] = [
  {
    dotClass: 'bg-blue-600',
    badgeClass: 'border-blue-200 bg-blue-50 text-blue-700',
    barClass: 'bg-blue-600',
    ringFocusClass: 'focus:border-blue-600 focus:ring-blue-100',
    headerTintClass: 'border-blue-200/80 bg-blue-50/70',
  },
  {
    dotClass: 'bg-violet-600',
    badgeClass: 'border-violet-200 bg-violet-50 text-violet-700',
    barClass: 'bg-violet-600',
    ringFocusClass: 'focus:border-violet-600 focus:ring-violet-100',
    headerTintClass: 'border-violet-200/80 bg-violet-50/70',
  },
  {
    dotClass: 'bg-emerald-600',
    badgeClass: 'border-emerald-200 bg-emerald-50 text-emerald-700',
    barClass: 'bg-emerald-600',
    ringFocusClass: 'focus:border-emerald-600 focus:ring-emerald-100',
    headerTintClass: 'border-emerald-200/80 bg-emerald-50/70',
  },
];

/** Return a deterministic color token per student index within a group so columns stay distinct. */
export function getStudentAccent(index: number): StudentAccentStyle {
  return STUDENT_ACCENTS[index % STUDENT_ACCENTS.length];
}

export function getCriterionScoreValue(
  groupScores: GradingStudentScoresMap | undefined,
  studentId: string,
  criterion: RubricCriterionView,
): string {
  return groupScores?.[studentId]?.[criterion.id] ?? groupScores?.[studentId]?.[criterion.code] ?? '';
}

export function computeStudentProgressList(
  students: readonly GradingStudentInfo[],
  criteria: readonly RubricCriterionView[],
  groupScores: GradingStudentScoresMap | undefined,
): StudentProgressSummary[] {
  return students.map((student) => {
    const completed = criteria.filter((criterion) =>
      isValidCriterionScore(getCriterionScoreValue(groupScores, student.id, criterion), criterion.max),
    );
    const total = completed.reduce((sum, criterion) => {
      const val = getCriterionScoreValue(groupScores, student.id, criterion) || '0';
      return sum + Number(val.replace(',', '.'));
    }, 0);
    return {
      ...student,
      count: completed.length,
      total: Math.round(total * 100) / 100,
    };
  });
}

export function countFilledGroupCriteria(
  students: readonly GradingStudentInfo[],
  criteria: readonly RubricCriterionView[],
  groupScores: GradingStudentScoresMap | undefined,
): number {
  return students.reduce(
    (total, student) =>
      total +
      criteria.filter((criterion) =>
        isValidCriterionScore(getCriterionScoreValue(groupScores, student.id, criterion), criterion.max),
      ).length,
    0,
  );
}
