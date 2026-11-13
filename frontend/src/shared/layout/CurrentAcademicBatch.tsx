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
      className="mx-3 my-2.5 rounded-xl border border-slate-200/80 bg-gradient-to-b from-slate-50/90 to-white p-3.5 shadow-2xs"
    >
      <div className="mb-2 flex items-center justify-between gap-2">
        <h2 className="text-[11px] font-semibold uppercase tracking-wider text-slate-500">
          Đợt hiện hành
        </h2>
        {canRead && !query.isPending && !query.isError && currentBatches.length > 0 && (
          <span className="inline-flex items-center gap-1.5 rounded-full bg-emerald-50 px-2 py-0.5 text-[10px] font-semibold text-emerald-700 ring-1 ring-inset ring-emerald-500/20">
            <span className="size-1.5 rounded-full bg-emerald-500" aria-hidden="true" />
            Đang mở
          </span>
        )}
      </div>
      {!canRead ? (
        <p className="text-xs text-slate-500 leading-relaxed">Bạn chưa có quyền xem đợt KLTN.</p>
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
            <p className="mb-2 text-[11px] font-medium text-slate-500">
              Có {currentBatches.length} đợt đang diễn ra
            </p>
          )}
          <ul className="max-h-44 space-y-2 overflow-y-auto">
            {currentBatches.map((batch) => (
              <li
                key={batch.id}
                className="rounded-lg border border-slate-200/70 bg-white px-2.5 py-2 shadow-2xs"
              >
                <p className="break-words text-xs font-semibold text-slate-800 leading-snug">
                  {batch.batchName}
                </p>
                <p className="mt-1 inline-flex items-center rounded-md bg-slate-100 px-1.5 py-0.5 text-[11px] font-medium text-slate-600">
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
