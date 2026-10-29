import { STANDARD_7_STAGES } from '../types/period.types';

export const DEFAULT_STAGE_WEEKS: readonly number[] = [1, 1, 7, 1, 8, 2, 1];

export function getTodayDateString(): string {
  const today = new Date();
  const pad = (n: number) => String(n).padStart(2, '0');
  return `${today.getFullYear()}-${pad(today.getMonth() + 1)}-${pad(today.getDate())}`;
}

export function addDaysToDateString(baseDateStr: string, daysToAdd: number): string {
  try {
    const [y, m, d] = baseDateStr.split('-').map(Number);
    if (!y || !m || !d) return baseDateStr;
    const date = new Date(y, m - 1, d + daysToAdd);
    const pad = (n: number) => String(n).padStart(2, '0');
    return `${date.getFullYear()}-${pad(date.getMonth() + 1)}-${pad(date.getDate())}`;
  } catch {
    return baseDateStr;
  }
}

export function toUtcDeadlineIso(dateInputStr: string, time: string = '23:59:00'): string {
  if (!dateInputStr) return '';
  try {
    const dateOnly = dateInputStr.includes('T') ? dateInputStr.split('T')[0] : dateInputStr;
    const [y, m, d] = dateOnly.split('-').map(Number);
    if (!y || !m || !d) return '';
    const [hh, mm, ss] = time.split(':').map(Number);
    const localDate = new Date(y, m - 1, d, hh || 23, mm || 59, ss || 0, 0);
    return localDate.toISOString();
  } catch {
    return '';
  }
}

export function computeStageWeeksFromDetail(
  stages?: Array<{ startDate: string; endDate: string; orderIndex: number }> | null,
): number[] {
  if (!stages || stages.length !== 7) {
    return [...DEFAULT_STAGE_WEEKS];
  }
  const sorted = [...stages].sort((a, b) => a.orderIndex - b.orderIndex);
  return sorted.map((s) => {
    const [sy, sm, sd] = s.startDate.split('-').map(Number);
    const [ey, em, ed] = s.endDate.split('-').map(Number);
    const start = new Date(sy, sm - 1, sd).getTime();
    const end = new Date(ey, em - 1, ed).getTime();
    const days = Math.round((end - start) / (1000 * 60 * 60 * 24)) + 1;
    return Math.max(1, Math.round(days / 7));
  });
}

export function calculateWeeksFromDateDiff(stageStart: string, targetDate: string): number {
  const diffDays = Math.round((new Date(targetDate).getTime() - new Date(stageStart).getTime()) / 86400000) + 1;
  return Math.max(1, Math.round(diffDays / 7));
}

export function computeStagesFromWeeks(startDate: string, stageWeeks: number[]) {
  const result = [];
  let currentStart = startDate || getTodayDateString();
  let cumulativeWeek = 0;

  for (let index = 0; index < STANDARD_7_STAGES.length; index++) {
    const meta = STANDARD_7_STAGES[index];
    const weeks = stageWeeks[index] || 1;
    const startWeek = cumulativeWeek + 1;
    const endWeek = cumulativeWeek + weeks;
    cumulativeWeek = endWeek;

    const stageStartDate = currentStart;
    const stageEndDate = addDaysToDateString(stageStartDate, weeks * 7 - 1);
    currentStart = addDaysToDateString(stageEndDate, 1);

    result.push({
      orderIndex: meta.orderIndex,
      stageCode: meta.stageCode,
      stageName: meta.stageName,
      shortName: meta.shortName,
      actor: meta.actor,
      weeks,
      startWeek,
      endWeek,
      weekLabel: weeks === 1 ? `Tuần ${startWeek}` : `Tuần ${startWeek}–${endWeek}`,
      startDate: stageStartDate,
      endDate: stageEndDate,
    });
  }

  return result;
}