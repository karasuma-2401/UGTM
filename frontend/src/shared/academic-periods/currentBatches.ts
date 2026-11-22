import type { AcademicBatch } from './types';

export interface BatchLike {
  id: string;
  status: string;
  startDate?: string;
  endDate?: string;
  timeZoneId?: string;
}

export const BATCH_SELECTION_MANAGEMENT_ROLES = [
  'Admin',
  'AcademicAffairs',
  'Faculty',
] as const;

/** Only management roles (Admin, AcademicAffairs, Faculty) can switch or filter across historical batches. */
export function canSelectAcademicBatch(
  hasRole?: (roles: string | string[]) => boolean,
): boolean {
  if (!hasRole) return false;
  return BATCH_SELECTION_MANAGEMENT_ROLES.some((role) => hasRole(role));
}

function isBatchCoveringToday(batch: BatchLike, now: Date): boolean {
  if (!batch.startDate || !batch.endDate || !batch.timeZoneId) return false;
  try {
    const parts = new Intl.DateTimeFormat('en-US', {
      timeZone: batch.timeZoneId,
      year: 'numeric',
      month: '2-digit',
      day: '2-digit',
    }).formatToParts(now);
    const value = (type: Intl.DateTimeFormatPartTypes) =>
      parts.find((part) => part.type === type)?.value;
    const today = `${value('year')}-${value('month')}-${value('day')}`;
    return batch.startDate <= today && today <= batch.endDate;
  } catch {
    // Invalid legacy dates/timezones cannot establish that a batch is current.
    return false;
  }
}

/** Open batches whose inclusive schedule contains today in their own timezone. */
export function getCurrentBatches(
  batches: readonly AcademicBatch[],
  now = new Date(),
): AcademicBatch[] {
  return batches
    .filter((batch) => batch.status === 'Open' && isBatchCoveringToday(batch, now))
    .sort((a, b) => b.startDate.localeCompare(a.startDate) || a.id.localeCompare(b.id));
}

/**
 * Returns the single currently Open academic batch (preferring one covering today),
 * and never falls back to a Closed, Archived, or Draft batch.
 */
export function getActiveOpenBatch<T extends BatchLike>(
  batches: readonly T[],
  now = new Date(),
): T | null {
  const openBatches = batches.filter((batch) => batch.status === 'Open');
  if (openBatches.length === 0) return null;
  if (openBatches.length === 1) return openBatches[0];

  const coveringToday = openBatches
    .filter((batch) => isBatchCoveringToday(batch, now))
    .sort(
      (a, b) => (b.startDate ?? '').localeCompare(a.startDate ?? '') || a.id.localeCompare(b.id),
    );

  return coveringToday[0] ?? openBatches[0];
}

/**
 * Default batch for management pages (Admin, AcademicAffairs, Faculty):
 * prefers the currently Open batch, otherwise falls back to the first batch in the list.
 */
export function getPreferredDefaultBatch<T extends BatchLike>(
  batches: readonly T[],
  now = new Date(),
): T | null {
  return getActiveOpenBatch(batches, now) ?? batches[0] ?? null;
}
