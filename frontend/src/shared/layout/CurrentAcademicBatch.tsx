import { CalendarRange } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { getCurrentBatches } from '@/shared/academic-periods/currentBatches';
import { useAcademicBatches } from '@/shared/academic-periods/useAcademicBatches';
import { useAuth } from '@/shared/auth';

function formatShortDateVi(dateStr?: string | null): string | null {
  if (!dateStr) return null;
  const parts = /^(\d{4})-(\d{2})-(\d{2})/.exec(dateStr.trim());
  if (!parts) return null;
  return `${parts[3]}/${parts[2]}/${parts[1]}`;
}

export function CurrentAcademicBatch() {
  const { hasPermission } = useAuth();
  const query = useAcademicBatches();
  const canRead = hasPermission('academic.batches.read');
  const currentBatches = getCurrentBatches(query.data ?? []);

  return (
    <section
      aria-label="Đợt hiện hành"
      aria-busy={query.isFetching}
      className="mx-5 border-y border-line py-4 text-xs text-ink-body"
    >
      {!canRead ? (
        <p className="leading-relaxed">Bạn chưa có quyền xem đợt KLTN.</p>
      ) : query.isPending ? (
        <p role="status">Đang tải thông tin đợt...</p>
      ) : query.isError ? (
        <div role="alert" className="space-y-2">
          <p className="text-xs text-tone-danger-text">Chưa tải được thông tin đợt KLTN.</p>
          <Button
            type="button"
            variant="outline"
            size="sm"
            className="min-h-11 text-xs lg:min-h-8"
            disabled={query.isFetching}
            onClick={() => void query.refetch()}
          >
            Thử lại
          </Button>
        </div>
      ) : currentBatches.length === 0 ? (
        <p>Chưa có đợt hiện hành.</p>
      ) : (
        <>
          {currentBatches.length > 1 && (
            <p className="mb-2 text-[11px] text-ink-muted">
              Có {currentBatches.length} đợt đang diễn ra
            </p>
          )}
          <ul className="max-h-32 space-y-2.5 overflow-y-auto">
            {currentBatches.map((batch) => {
              const startText = formatShortDateVi(batch.startDate);
              const endText = formatShortDateVi(batch.endDate);
              return (
                <li key={batch.id} className="space-y-1">
                  <p className="break-words text-[13px] font-semibold leading-snug text-ink-primary">
                    {batch.batchName}
                  </p>
                  <div className="flex flex-wrap items-center gap-1.5 text-xs text-ink-muted tabular-nums">
                    <CalendarRange className="size-3.5 text-ink-subtle shrink-0" aria-hidden="true" />
                    <span className="font-medium text-ink-body">
                      HK{batch.semester} · {batch.academicYear}
                    </span>
                  </div>
                  {startText && endText && (
                    <p className="text-[11px] text-ink-subtle tabular-nums pl-5">
                      {startText} – {endText}
                    </p>
                  )}
                </li>
              );
            })}
          </ul>
        </>
      )}
    </section>
  );
}
