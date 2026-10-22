import { Skeleton } from '@/components/ui/skeleton';

export function TopicDetailSkeleton() {
  return (
    <div role="status" className="space-y-6 p-5 sm:p-6">
      <span className="sr-only">Đang tải nội dung đề tài...</span>
      <div aria-hidden="true" className="space-y-6">
        <div className="space-y-3">
          <Skeleton className="h-6 w-28 rounded-full" />
          <Skeleton className="h-7 w-4/5" />
          <Skeleton className="h-4 w-3/5" />
        </div>
        <div className="grid gap-6 rounded-xl bg-slate-50 p-4 sm:grid-cols-2">
          {[0, 1].map((index) => (
            <div key={index} className="space-y-2">
              <Skeleton className="h-3 w-24" />
              <Skeleton className="h-4 w-40 max-w-full" />
            </div>
          ))}
        </div>
        {[0, 1].map((index) => (
          <div key={index} className="space-y-3">
            <Skeleton className="h-4 w-48" />
            <Skeleton className="h-4 w-full" />
            <Skeleton className="h-4 w-5/6" />
            <Skeleton className="h-4 w-2/3" />
          </div>
        ))}
      </div>
    </div>
  );
}
