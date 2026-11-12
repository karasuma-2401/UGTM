import { Button } from '@/components/ui/button';
import { useAuth } from '@/shared/auth';
import { getCurrentBatches } from '@/shared/academic-periods/currentBatches';
import { useAcademicBatches } from '@/shared/academic-periods/useAcademicBatches';

export function CurrentAcademicBatch() {
  const { hasPermission } = useAuth();
  const query = useAcademicBatches();
  const canRead = hasPermission('academic.batches.read');
  const currentBatches = getCurrentBatches(query.data ?? []);

  return (
    <section
      aria-label="Đợt hiện hành"
      aria-busy={query.isFetching}
      className="mx-5 border-b border-slate-200 pb-4 text-xs text-slate-600"
    >
      {canRead && !query.isPending && !query.isError && currentBatches.length > 0 && (
        <div className="mb-2 flex items-center justify-between gap-2">
          <h2 className="text-xs font-medium text-slate-600">Đợt hiện hành</h2>
          <span className="inline-flex items-center gap-1.5 text-xs font-medium text-emerald-700">
            <span className="size-1.5 rounded-full bg-emerald-500" aria-hidden="true" />
            Đang mở
          </span>
        </div>
      )}
      {!canRead ? (
        <p className="leading-relaxed">Bạn chưa có quyền xem đợt KLTN.</p>
      ) : query.isPending ? (
        <p role="status">Đang tải thông tin đợt...</p>
      ) : query.isError ? (
        <div role="alert" className="space-y-2">
          <p className="text-xs text-rose-700">Chưa tải được thông tin đợt KLTN.</p>
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
            <p className="mb-2">Có {currentBatches.length} đợt đang diễn ra</p>
          )}
          <ul className="max-h-32 space-y-3 overflow-y-auto">
            {currentBatches.map((batch) => (
              <li key={batch.id} className="space-y-1">
                <p className="break-words font-medium leading-snug text-slate-800">
                  {batch.batchName}
                </p>
                <p>
                  HK{batch.semester} - {batch.academicYear}
                </p>
              </li>
            ))}
          </ul>
        </>
      )}
    </section>
  );
}
