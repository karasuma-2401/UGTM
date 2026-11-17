import type { AcademicBatch } from './types';

/** Open batches whose inclusive schedule contains today in their own timezone.
 * There can be more than one: no backend rule guarantees one global current batch.
 */
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
