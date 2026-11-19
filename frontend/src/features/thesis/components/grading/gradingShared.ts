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
    dotClass: 'bg-blue-600 dark:bg-blue-500',
    badgeClass: 'border-tone-info-border bg-tone-info-bg text-tone-info-text',
    barClass: 'bg-blue-600 dark:bg-blue-500',
    ringFocusClass: 'focus:border-blue-600 focus:ring-blue-100 dark:focus:border-blue-400 dark:focus:ring-blue-900/50',
    headerTintClass: 'border-tone-info-border bg-tone-info-bg',
  },
  {
    dotClass: 'bg-violet-600 dark:bg-violet-500',
    badgeClass: 'border-tone-violet-border bg-tone-violet-bg text-tone-violet-text',
    barClass: 'bg-violet-600 dark:bg-violet-500',
    ringFocusClass: 'focus:border-violet-600 focus:ring-violet-100 dark:focus:border-violet-400 dark:focus:ring-violet-900/50',
    headerTintClass: 'border-tone-violet-border bg-tone-violet-bg',
  },
  {
    dotClass: 'bg-emerald-600 dark:bg-emerald-500',
    badgeClass: 'border-tone-success-border bg-tone-success-bg text-tone-success-text',
    barClass: 'bg-emerald-600 dark:bg-emerald-500',
    ringFocusClass: 'focus:border-emerald-600 focus:ring-emerald-100 dark:focus:border-emerald-400 dark:focus:ring-emerald-900/50',
    headerTintClass: 'border-tone-success-border bg-tone-success-bg',
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
