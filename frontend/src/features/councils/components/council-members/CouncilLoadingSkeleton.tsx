import { Card } from '@/components/ui/card';
import { Skeleton } from '@/components/ui/skeleton';

export function CouncilListSkeleton({ label, rows = 3 }: { label: string; rows?: number }) {
  return (
    <div role="status" aria-label={label} className="mt-4 space-y-2">
      <span className="sr-only">{label}</span>
      {Array.from({ length: rows }, (_, index) => (
        <div key={index} className="flex items-center gap-3 rounded-xl border border-line/80 p-3">
          <div className="min-w-0 flex-1 space-y-2">
            <Skeleton className="h-4 w-2/5" />
            <Skeleton className="h-3 w-4/5" />
            <Skeleton className="h-3 w-1/2" />
          </div>
          <Skeleton className="h-7 w-20 shrink-0" />
        </div>
      ))}
    </div>
  );
}

export function CouncilWorkspaceSkeleton() {
  return (
    <div role="status" aria-label="Đang tải thông tin hội đồng" className="space-y-5">
      <span className="sr-only">Đang tải thông tin hội đồng…</span>
      {[0, 1, 2].map((index) => (
        <Card key={index} className="space-y-4 p-4 sm:p-5">
          <Skeleton className="h-5 w-1/2 max-w-56" />
          <Skeleton className="h-4 w-3/4" />
          <Skeleton className="h-14 w-full rounded-xl" />
        </Card>
      ))}
    </div>
  );
}
