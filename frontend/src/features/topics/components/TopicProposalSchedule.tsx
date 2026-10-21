import { useQuery } from '@tanstack/react-query';
import { CalendarDays } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { useAuth } from '@/shared/auth';
import { academicBatchApi } from '@/shared/academic-periods/academicBatchApi';

export function TopicProposalSchedule({ batchId }: { batchId: string }) {
  const { user, hasPermission } = useAuth();
  const canRead = hasPermission('academic.batches.read');
  const query = useQuery({
    queryKey: ['academic-batch-timeline', user?.id, batchId],
    queryFn: ({ signal }) => academicBatchApi.getTimeline(batchId, signal),
    enabled: Boolean(batchId && user?.id) && canRead,
    staleTime: 60_000,
    refetchInterval: 60_000,
  });
  const timeline = query.data;
  const stage = timeline?.stages.find((item) => item.stageCode === 'TOPIC_PREPARATION');
  const formatDate = (value: string) => value.split('-').reverse().join('/');
  let deadline = 'Chưa xác định';
  if (stage && timeline) {
    try {
      deadline = new Intl.DateTimeFormat('vi-VN', {
        timeZone: timeline.timeZoneId,
        dateStyle: 'short',
        timeStyle: 'short',
      }).format(new Date(stage.deadlineAtUtc));
    } catch {
      /* Keep the missing-date label. */
    }
  }
  return (
    <section
      className="rounded-xl border border-blue-100 bg-blue-50/50 p-3 text-xs leading-5"
      aria-label="Lịch chuẩn bị đề tài"
    >
      <h3 className="mb-2 flex items-center gap-2 font-semibold text-blue-800">
        <CalendarDays className="size-4 shrink-0" />
        Lịch chuẩn bị đề tài
      </h3>
      {!canRead ? (
        <p className="text-slate-500">Bạn chưa có quyền xem lịch của đợt.</p>
      ) : query.isPending ? (
        <p role="status" className="text-slate-500">
          Đang tải lịch...
        </p>
      ) : query.isError ? (
        <div role="alert">
          <p className="text-rose-700">Chưa tải được lịch của đợt.</p>
          <Button
            type="button"
            size="sm"
            variant="outline"
            className="mt-2"
            onClick={() => void query.refetch()}
          >
            Thử lại
          </Button>
        </div>
      ) : stage && timeline ? (
        <div className="space-y-1 text-slate-600">
          <p className="font-medium text-slate-800">{stage.stageName}</p>
          <p>
            {formatDate(stage.startDate)} – {formatDate(stage.endDate)}
          </p>
          <p>
            Mốc hạn: <strong className="font-semibold">{deadline}</strong>
          </p>
          <p className="font-medium text-blue-700">
            {timeline.currentLocalDate < stage.startDate
              ? 'Chưa đến giai đoạn'
              : timeline.currentLocalDate > stage.endDate
                ? 'Đã qua giai đoạn'
                : 'Đang trong giai đoạn'}
          </p>
        </div>
      ) : (
        <p className="text-slate-500">Đợt này chưa có lịch chuẩn bị đề tài.</p>
      )}
    </section>
  );
}
