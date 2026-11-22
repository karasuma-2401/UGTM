import type { AcademicBatch } from './types';

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

/** Open batches whose inclusive schedule contains today in their own timezone. */
export function getCurrentBatches(
  batches: readonly AcademicBatch[],
  now = new Date(),
): AcademicBatch[] {
  return batches
    .filter((batch) => {
      if (batch.status !== 'Open') return false;
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
    })
    .sort((a, b) => b.startDate.localeCompare(a.startDate) || a.id.localeCompare(b.id));
}

/**
 * Returns the single currently Open academic batch (preferring one covering today),
 * and never falls back to a Closed, Archived, or Draft batch.
 */
export function getActiveOpenBatch(
  batches: readonly AcademicBatch[],
  now = new Date(),
): AcademicBatch | null {
  return getCurrentBatches(batches, now)[0] ?? batches.find((batch) => batch.status === 'Open') ?? null;
}
