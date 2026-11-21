import type { AcademicBatch, AcademicBatchTimeline } from '@/shared/academic-periods/types';

export type DefenseStage = AcademicBatchTimeline['stages'][number];
export type CouncilDateWindow = { startDate: string; endDate: string; source: 'stage' | 'batch'; stageName?: string };

export function defenseStageOf(timeline?: AcademicBatchTimeline): DefenseStage | undefined {
  return timeline?.stages.find((stage) => stage.stageCode === 'DEFENSE');
}

export function councilDateWindow(timeline?: AcademicBatchTimeline, batch?: AcademicBatch): CouncilDateWindow | undefined {
  const stage = defenseStageOf(timeline);
  if (stage?.startDate && stage.endDate && stage.startDate <= stage.endDate) {
    return { startDate: stage.startDate, endDate: stage.endDate, source: 'stage', stageName: stage.stageName };
  }
  if (batch?.startDate && batch.endDate && batch.startDate <= batch.endDate) {
    return { startDate: batch.startDate.slice(0, 10), endDate: batch.endDate.slice(0, 10), source: 'batch' };
  }
  return undefined;
}

export function isOutsideCouncilWindow(date: string, window?: CouncilDateWindow): boolean {
  return Boolean(date && window && (date < window.startDate || date > window.endDate));
}

export function suggestedDefenseDate(window?: CouncilDateWindow, currentLocalDate?: string): string {
  if (!window || !currentLocalDate || currentLocalDate > window.endDate) return '';
  return currentLocalDate < window.startDate ? window.startDate : currentLocalDate;
}

export function batchLocalDate(batch?: AcademicBatch, now = new Date()): string {
  try {
    const parts = new Intl.DateTimeFormat('en-US', {
      timeZone: batch?.timeZoneId,
      year: 'numeric',
      month: '2-digit',
      day: '2-digit',
    }).formatToParts(now);
    const value = (type: string) => parts.find((part) => part.type === type)?.value ?? '';
    return `${value('year')}-${value('month')}-${value('day')}`;
  } catch {
    return `${now.getFullYear()}-${String(now.getMonth() + 1).padStart(2, '0')}-${String(now.getDate()).padStart(2, '0')}`;
  }
}
