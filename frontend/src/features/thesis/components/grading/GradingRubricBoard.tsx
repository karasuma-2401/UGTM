import { useMemo, useRef, useState, type KeyboardEvent, type ReactNode } from 'react';
import { Filter } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { cn } from '@/lib/utils';
import {
  isValidCriterionScore,
  type RubricCriterionView,
  type RubricSectionView,
} from '../../utils/gradingRubricHelper';
import {
  getCriterionScoreValue,
  getStudentAccent,
  type GradingStudentScoresMap,
  type StudentProgressSummary,
} from './gradingShared';

export interface GradingRubricBoardProps {
  idPrefix: string;
  groupTitle: string;
  sections: readonly RubricSectionView[];
  criteriaCount: number;
  students: readonly StudentProgressSummary[];
  scores: GradingStudentScoresMap;
  isLocked: boolean;
  onUpdateScore: (studentId: string, criterionId: string, value: string) => void;
  children?: ReactNode;
}

/**
 * Per-student rubric grading board with sticky student headers, color-coded columns,
 * live section subtotals, Excel-style keyboard navigation, and missing-only filter.
 */
export function GradingRubricBoard({
  idPrefix,
  groupTitle,
  sections,
  criteriaCount,
  students,
  scores,
  isLocked,
  onUpdateScore,
  children,
}: GradingRubricBoardProps) {
  const [onlyMissing, setOnlyMissing] = useState(false);
  const inputRefs = useRef<Record<string, HTMLInputElement | null>>({});

  const allCriteria = useMemo(
    () => sections.flatMap((section) => section.criteria),
    [sections],
  );

  const missingCriterionIds = useMemo(() => {
    const missing = new Set<string>();
    for (const item of allCriteria) {
      const anyIncomplete = students.some(
        (student) =>
          !isValidCriterionScore(getCriterionScoreValue(scores, student.id, item), item.max),
      );
      if (anyIncomplete) missing.add(item.id);
    }
    return missing;
  }, [allCriteria, scores, students]);

  const missingCellCount = useMemo(() => {
    let missing = 0;
    for (const item of allCriteria) {
      for (const student of students) {
        if (!isValidCriterionScore(getCriterionScoreValue(scores, student.id, item), item.max)) {
          missing += 1;
        }
      }
    }
    return missing;
  }, [allCriteria, scores, students]);

  const visibleSections = useMemo(() => {
    if (!onlyMissing) return sections;
    return sections
      .map((section) => ({
        ...section,
        criteria: section.criteria.filter((item) => missingCriterionIds.has(item.id)),
      }))
      .filter((section) => section.criteria.length > 0);
  }, [missingCriterionIds, onlyMissing, sections]);

  const flatVisibleCriteria = useMemo(
    () => visibleSections.flatMap((section) => section.criteria),
    [visibleSections],
  );

  function handleInputKeyDown(
    event: KeyboardEvent<HTMLInputElement>,
    rowIdx: number,
    colIdx: number,
  ) {
    const target = event.currentTarget;
    const focusCell = (nextRow: number, nextCol: number) => {
      const next = inputRefs.current[`${nextRow}:${nextCol}`];
      if (next) {
        event.preventDefault();
        next.focus();
        next.select();
      }
    };

    if (event.key === 'Enter') {
      if (rowIdx + 1 < flatVisibleCriteria.length) {
        focusCell(rowIdx + 1, colIdx);
      } else if (colIdx + 1 < students.length) {
        focusCell(0, colIdx + 1);
      }
      return;
    }

    if (event.key === 'ArrowDown' && rowIdx + 1 < flatVisibleCriteria.length) {
      focusCell(rowIdx + 1, colIdx);
      return;
    }

    if (event.key === 'ArrowUp' && rowIdx > 0) {
      focusCell(rowIdx - 1, colIdx);
      return;
    }

    if (
      event.key === 'ArrowRight' &&
      colIdx + 1 < students.length &&
      target.selectionStart === target.value.length &&
      target.selectionEnd === target.value.length
    ) {
      focusCell(rowIdx, colIdx + 1);
      return;
    }

    if (
      event.key === 'ArrowLeft' &&
      colIdx > 0 &&
      target.selectionStart === 0 &&
      target.selectionEnd === 0
    ) {
      focusCell(rowIdx, colIdx - 1);
    }
  }

  function computeSectionSubtotal(
    sectionCriteria: readonly RubricCriterionView[],
    studentId: string,
  ): number {
    const sum = sectionCriteria.reduce((acc, item) => {
      const raw = getCriterionScoreValue(scores, studentId, item);
      if (!isValidCriterionScore(raw, item.max)) return acc;
      return acc + Number(raw.replace(',', '.'));
    }, 0);
    return Math.round(sum * 100) / 100;
  }

  let runningRowIndex = -1;
  const desktopScoreColWidth = students.length >= 3 ? 'md:w-28 lg:w-32' : 'md:w-32 lg:w-36';

  return (
    <section
      className="min-w-0 overflow-x-clip rounded-2xl border border-line/80 bg-surface-card shadow-sm"
      aria-labelledby={`${idPrefix}-rubric-title`}
    >
      <div className="flex flex-wrap items-end justify-between gap-3 border-b border-line px-5 py-5 sm:px-6">
        <div className="min-w-0">
          <h2 id={`${idPrefix}-rubric-title`} className="text-base font-semibold text-ink-primary">
            Bảng điểm chi tiết
          </h2>
          <p className="mt-1 text-sm text-ink-body">{groupTitle}</p>
        </div>
        <span className="text-xs font-medium text-ink-muted">
          {criteriaCount} tiêu chí · Thang điểm 10
        </span>
      </div>

      {!isLocked && (
        <div className="flex flex-wrap items-center justify-between gap-2.5 border-b border-line-subtle bg-surface-subtle/45 px-5 py-3 sm:px-6">
          <div className="flex flex-wrap items-center gap-2">
            <button
              type="button"
              aria-pressed={onlyMissing}
              onClick={() => setOnlyMissing((prev) => !prev)}
              className={cn(
                'inline-flex items-center gap-1.5 rounded-lg border px-2.5 py-1.5 text-xs font-medium transition',
                onlyMissing
                  ? 'border-tone-info-border bg-tone-info-bg text-tone-info-text shadow-2xs'
                  : 'border-line-input bg-surface-card text-ink-secondary hover:bg-surface-hover',
              )}
            >
              <Filter className="size-3.5" aria-hidden="true" />
              Chỉ hiện ô chưa nhập
              <span
                className={cn(
                  'inline-flex min-w-5 items-center justify-center rounded-full px-1.5 py-0.5 text-[11px] font-semibold tabular-nums',
                  onlyMissing ? 'bg-tone-info-bg text-tone-info-strong' : 'bg-surface-muted text-ink-body',
                )}
              >
                {missingCellCount}
              </span>
            </button>
          </div>
        </div>
      )}

      {students.length > 0 && (
        <div
          className="sticky top-0 z-10 hidden border-b border-line bg-surface-card/95 px-5 py-2.5 backdrop-blur-xs md:grid md:grid-cols-[minmax(0,1fr)_auto] md:items-center md:gap-x-5 sm:px-6"
          aria-label="Cột sinh viên đang chấm"
        >
          <span className="text-xs font-semibold tracking-wide text-ink-muted">
            Tiêu chí đánh giá
          </span>
          <div className="flex shrink-0 justify-end gap-3">
            {students.map((student, studentIdx) => {
              const accent = getStudentAccent(studentIdx);
              return (
                <div
                  key={student.id}
                  className={cn(
                    'flex items-center justify-between gap-2 rounded-lg border px-2.5 py-1.5 text-xs',
                    desktopScoreColWidth,
                    accent.headerTintClass,
                  )}
                >
                  <div className="flex min-w-0 items-center gap-1.5">
                    <span
                      className={cn('size-2 shrink-0 rounded-full', accent.dotClass)}
                      aria-hidden="true"
                    />
                    <span className="truncate font-semibold text-ink-heading" title={student.name}>
                      SV{studentIdx + 1}: {student.name}
                    </span>
                  </div>
                  <span className="shrink-0 font-bold tabular-nums text-ink-primary">
                    {student.total.toFixed(1)}
                  </span>
                </div>
              );
            })}
          </div>
        </div>
      )}

      {onlyMissing && visibleSections.length === 0 ? (
        <div className="px-6 py-10 text-center">
          <p className="text-sm font-medium text-tone-success-text">
            Tất cả tiêu chí cho mọi sinh viên đã được nhập hợp lệ.
          </p>
          <Button
            type="button"
            variant="outline"
            size="sm"
            onClick={() => setOnlyMissing(false)}
            className="mt-3 rounded-lg"
          >
            Hiện lại toàn bộ {criteriaCount} tiêu chí
          </Button>
        </div>
      ) : (
        visibleSections.map((section, index) => (
          <div key={section.code || section.name}>
            <div className="flex flex-wrap items-center justify-between gap-2 border-b border-line-subtle bg-surface-subtle/70 px-5 py-3 sm:px-6">
              <h3 className="text-sm font-semibold text-ink-heading">
                {section.order || index + 1}. {section.name}
              </h3>
              <div className="flex flex-wrap items-center gap-2 text-xs">
                {students.map((student, studentIdx) => {
                  const accent = getStudentAccent(studentIdx);
                  const subtotal = computeSectionSubtotal(section.criteria, student.id);
                  return (
                    <span
                      key={student.id}
                      className="inline-flex items-center gap-1 rounded-md border border-line-input/80 bg-surface-card px-2 py-0.5 font-medium tabular-nums text-ink-secondary"
                    >
                      <span
                        className={cn('size-1.5 rounded-full', accent.dotClass)}
                        aria-hidden="true"
                      />
                      <span>SV{studentIdx + 1}:</span>
                      <span className="font-semibold text-ink-primary">{subtotal.toFixed(2)}</span>
                    </span>
                  );
                })}
                <span className="shrink-0 font-medium tabular-nums text-ink-muted">
                  / {section.max.toFixed(1)} điểm
                </span>
              </div>
            </div>

            {section.criteria.map((item) => {
              runningRowIndex += 1;
              const currentRowIdx = runningRowIndex;
              return (
                <div
                  key={item.id}
                  className="flex flex-col gap-3 border-b border-line-subtle px-5 py-4 last:border-b-0 sm:px-6 md:grid md:grid-cols-[minmax(0,1fr)_auto] md:items-center md:gap-x-5"
                >
                  <div className="min-w-0">
                    <p className="text-sm leading-relaxed text-ink-heading">
                      <span className="mr-2 font-medium text-ink-muted">{item.code}</span>
                      {item.name}
                    </p>
                    <p className="mt-1 text-xs text-ink-muted">
                      {item.elo} <span aria-hidden="true">·</span> Tối đa {item.max.toFixed(1)} điểm
                    </p>
                  </div>

                  <div className="flex flex-wrap gap-3 md:shrink-0 md:flex-nowrap md:justify-end">
                    {students.map((student, colIdx) => {
                      const accent = getStudentAccent(colIdx);
                      const value = getCriterionScoreValue(scores, student.id, item);
                      const invalid = value !== '' && !isValidCriterionScore(value, item.max);
                      const id = `${idPrefix}-${student.id}-${item.id}`;
                      return (
                        <div
                          key={student.id}
                          className={cn(
                            'max-w-full',
                            students.length >= 2 ? 'w-[calc(50%-0.375rem)] sm:w-36' : 'w-44 sm:w-36',
                            desktopScoreColWidth,
                          )}
                        >
                          <label
                            htmlFor={id}
                            className="mb-1.5 flex items-center gap-1.5 text-xs font-medium text-ink-body md:sr-only"
                          >
                            <span
                              className={cn(
                                'flex size-5 shrink-0 items-center justify-center rounded-full border text-[10px] font-semibold',
                                accent.badgeClass,
                              )}
                            >
                              {student.initials}
                            </span>
                            <span className="truncate">{student.name}</span>
                            <span className="sr-only"> · {item.name}</span>
                          </label>
                          <div className="relative">
                            <input
                              ref={(el) => {
                                inputRefs.current[`${currentRowIdx}:${colIdx}`] = el;
                              }}
                              id={id}
                              type="text"
                              inputMode="decimal"
                              disabled={isLocked}
                              value={value}
                              onChange={(event) =>
                                onUpdateScore(student.id, item.id, event.target.value)
                              }
                              onKeyDown={(event) =>
                                handleInputKeyDown(event, currentRowIdx, colIdx)
                              }
                              placeholder="Nhập điểm"
                              aria-label={`${student.name} — ${item.code} ${item.name}`}
                              aria-invalid={invalid}
                              aria-describedby={invalid ? `${id}-error` : undefined}
                              className={cn(
                                'h-11 w-full rounded-lg border bg-surface-card pl-3 pr-11 text-sm font-medium tabular-nums text-ink-primary outline-none placeholder:text-ink-muted focus:ring-2 disabled:bg-surface-subtle disabled:text-ink-muted sm:h-10',
                                invalid
                                  ? 'border-red-500 focus:ring-red-100 dark:focus:ring-red-900/40'
                                  : cn('border-line-strong', accent.ringFocusClass),
                              )}
                            />
                            <span className="pointer-events-none absolute right-3 top-1/2 -translate-y-1/2 text-sm text-ink-muted">
                              / {item.max}
                            </span>
                          </div>
                          {invalid && (
                            <p id={`${id}-error`} className="mt-1 text-xs text-tone-danger-text">
                              Nhập từ 0 đến {item.max}.
                            </p>
                          )}
                        </div>
                      );
                    })}
                  </div>
                </div>
              );
            })}
          </div>
        ))
      )}

      {children}
    </section>
  );
}
