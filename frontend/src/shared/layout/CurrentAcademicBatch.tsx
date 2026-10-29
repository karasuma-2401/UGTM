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
      className="mx-3 my-2 rounded-lg border border-slate-200/80 bg-slate-50 p-3"
    >
      <h2 className="mb-1 text-[11px] font-semibold uppercase tracking-wider text-slate-500">
        Đợt hiện hành
      </h2>
      {!canRead ? (
        <p className="text-xs text-slate-500">Bạn chưa có quyền xem đợt KLTN.</p>
      ) : query.isPending ? (
        <p role="status" className="text-xs text-slate-500">
          Đang tải thông tin đợt...
        </p>
      ) : query.isError ? (
        <div role="alert" className="space-y-2">
          <p className="text-xs text-rose-700">Chưa tải được thông tin đợt KLTN.</p>
          <Button
            type="button"
            variant="outline"
            size="sm"
            className="h-7 text-[11px]"
            disabled={query.isFetching}
            onClick={() => void query.refetch()}
          >
            Thử lại
          </Button>
        </div>
      ) : currentBatches.length === 0 ? (
        <p className="text-xs text-slate-500">Chưa có đợt hiện hành.</p>
      ) : (
        <>
          {currentBatches.length > 1 && (
            <p className="mb-2 text-[11px] text-slate-500">
              Có {currentBatches.length} đợt đang diễn ra
            </p>
          )}
          <ul className="max-h-44 space-y-2 overflow-y-auto">
            {currentBatches.map((batch) => (
              <li key={batch.id}>
                <p className="break-words text-xs font-semibold text-slate-800">
                  {batch.batchName}
                </p>
                <p className="mt-1 text-[11px] text-slate-500">
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
